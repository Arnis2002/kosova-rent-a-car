using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Npgsql;
namespace Kosova;
public interface IPaymentProvider { string Name {get;} string Sign(string body); bool Verify(string body,string signature); }
public sealed class TestPaymentProvider(string secret):IPaymentProvider
{
    public string Name=>"isolated_test";
    public string Sign(string body)=>Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),Encoding.UTF8.GetBytes(body)));
    public bool Verify(string body,string signature) { try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sign(body)),Convert.FromHexString(signature)); } catch(FormatException) { return false; } }
}
public static class Payments
{
    public static void Map(WebApplication app)
    {
        var secret=app.Configuration["TestPaymentSecret"]??Security.Token(); var provider=new TestPaymentProvider(secret);
        app.MapPost("/api/bookings/{id:guid}/payment",async(HttpContext ctx,Store db,RuntimeOptions opts,Guid id)=> {
            if(!opts.Demo) throw new RuleException("live_provider_unconfigured",503);
            await using var c=await db.Open(); await using var tx=await c.BeginTransactionAsync(); var b=await Security.Booking(ctx,c,id,true);
            if(b.S("status")!="awaiting_payment"||b["payment_due"]!.GetValue<DateTime>()<=DateTime.UtcNow) throw new RuleException("payment_not_due",409);
            var key="booking:"+id; var rows=await Store.Query(c,"SELECT id,state,amount FROM payments WHERE idempotency_key=$1",key);
            if(rows.Count>0) return Results.Ok(rows[0]); var pid=Guid.NewGuid(); var amount=b.Obj("snapshot")["payNow"]!.GetValue<long>();
            await Store.Exec(c,"INSERT INTO payments(id,booking_id,provider,amount,state,idempotency_key) VALUES($1,$2,$3,$4,'pending',$5)",pid,id,provider.Name,amount,key); await Store.Audit(c,"guest","payment.create",pid.ToString(),"Isolated test checkout",null,new JsonObject{["amount"]=amount}); await tx.CommitAsync(); return Results.Ok(new{id=pid,state="pending",amount});
        });
        app.MapPost("/api/test-payments/{id:guid}/complete",async(HttpContext ctx,Store db,RuntimeOptions opts,Guid id,JsonObject input)=> {
            if(!opts.Demo) throw new RuleException("not_found",404);
            await using(var c=await db.Open()) { var p=await Store.One(c,"SELECT booking_id FROM payments WHERE id=$1",id); await Security.Booking(ctx,c,p.G("booking_id")); }
            var state=input.S("state"); if(state is not("succeeded" or "failed")) throw new RuleException("invalid_status");
            return Results.Ok(await Process(db,new JsonObject{["eventId"]="test:"+id+":"+state,["paymentId"]=id,["state"]=state}));
        });
        app.MapPost("/api/payments/test/webhook",async(HttpContext ctx,Store db,RuntimeOptions opts)=> {
            if(!opts.Demo) throw new RuleException("not_found",404); if(ctx.Request.ContentLength>10000) throw new RuleException("payload_too_large",413);
            using var reader=new StreamReader(ctx.Request.Body); var body=await reader.ReadToEndAsync(); if(!provider.Verify(body,ctx.Request.Headers["X-Test-Signature"].ToString())) throw new RuleException("invalid_signature",401);
            return Results.Ok(await Process(db,JsonNode.Parse(body) as JsonObject??throw new RuleException("invalid_event")));
        });
        app.MapPost("/api/manage/bookings/{id:guid}/refund",async(HttpContext ctx,Store db,RuntimeOptions opts,Guid id,JsonObject input)=> {
            if(!opts.Demo) throw new RuleException("live_provider_unconfigured",503);
            await using var c=await db.Open(); var a=await Security.Actor(ctx,c); Security.Admin(a); var reason=input.Required("reason"); await using var tx=await c.BeginTransactionAsync();
            var b=await Store.One(c,"SELECT * FROM bookings WHERE id=$1 FOR UPDATE",id); if(b.S("status") is not("cancelled" or "expired")||b.S("payment_state")!="paid") throw new RuleException("refund_not_available",409);
            var p=await Store.One(c,"SELECT * FROM payments WHERE booking_id=$1 AND state='succeeded' FOR UPDATE",id);
            await Store.Exec(c,"INSERT INTO refunds(id,payment_id,amount,state,reason) VALUES($1,$2,$3,'refunded',$4) ON CONFLICT(payment_id) DO NOTHING",Guid.NewGuid(),p.G("id"),p["amount"]!.GetValue<long>(),reason);
            await Store.Exec(c,"UPDATE bookings SET refund_state='refunded' WHERE id=$1",id); await Store.Audit(c,a.Key,"payment.refund",id.ToString(),reason,new JsonObject{["refundState"]=b.S("refund_state")},new JsonObject{["refundState"]="refunded"}); await BookingService.Notify(c,b,"refunded",a.Key,reason); await tx.CommitAsync(); return Results.Ok(new{state="refunded",provider=provider.Name});
        });
    }
    public static async Task<JsonObject> Process(Store db,JsonObject ev)
    {
        var eventId=ev.Required("eventId"); var pid=ev.G("paymentId"); var state=ev.S("state"); if(state is not("succeeded" or "failed")) throw new RuleException("invalid_event");
        await using var c=await db.Open(); await using var tx=await c.BeginTransactionAsync();
        // All payment paths lock booking then payment to maintain a consistent lock order.
        var reference=await Store.One(c,"SELECT booking_id FROM payments WHERE id=$1",pid); var b=await Store.One(c,"SELECT * FROM bookings WHERE id=$1 FOR UPDATE",reference.G("booking_id")); var p=await Store.One(c,"SELECT * FROM payments WHERE id=$1 FOR UPDATE",pid);
        if((await Store.Query(c,"SELECT event_id FROM payment_events WHERE event_id=$1",eventId)).Count>0) return new JsonObject{["state"]=p.S("state"),["duplicate"]=true};
        await Store.Exec(c,"INSERT INTO payment_events(event_id,payment_id,state) VALUES($1,$2,$3)",eventId,pid,state);
        if(p.S("state")=="succeeded") { await tx.CommitAsync(); return new JsonObject{["state"]="succeeded",["ignored"]=true}; }
        if(state=="succeeded") {
            if(b.S("status")!="awaiting_payment"||b["payment_due"]!.GetValue<DateTime>()<=DateTime.UtcNow) {
                await Store.Exec(c,"UPDATE payments SET state='succeeded' WHERE id=$1",pid); await Store.Exec(c,"UPDATE bookings SET payment_state='paid',refund_state='requested' WHERE id=$1",b.G("id")); await Store.Audit(c,"test-provider","payment.late",pid.ToString(),"Late success requires operational refund",null,ev); await tx.CommitAsync(); return new JsonObject{["state"]="refund_required"};
            }
            await Store.Exec(c,"UPDATE allocations SET kind='booking',expires_at=null WHERE booking_id=$1",b.G("id")); await Store.Exec(c,"UPDATE bookings SET status='confirmed',payment_state='paid' WHERE id=$1",b.G("id")); await BookingService.Notify(c,b,"confirmed","test-provider","Test payment completed");
        }
        await Store.Exec(c,"UPDATE payments SET state=$2 WHERE id=$1",pid,state); await Store.Audit(c,"test-provider","payment.event",pid.ToString(),"Signed event or authorized local test checkout",new JsonObject{["state"]=p.S("state")},ev); await tx.CommitAsync(); return new JsonObject{["state"]=state};
    }
}
