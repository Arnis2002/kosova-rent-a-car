using System.Text.Json.Nodes;
namespace Kosova;
public static class CustomerOperations
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/manage/bookings/{id:guid}/replacement",async(HttpContext ctx,Store db,Guid id,JsonObject input)=> {
            await using var c=await db.Open(); var actor=await Security.Actor(ctx,c); await using var tx=await c.BeginTransactionAsync(); var b=await Security.Booking(ctx,c,id,true); Security.Scope(actor,b.G("supplier_id"));
            if(b.S("status")!="confirmed"||b.S("payment_state")=="paid") throw new RuleException("replacement_not_supported",409);
            var vehicle=await Store.One(c,BookingService.VehicleSelect+" WHERE v.id=$1",input.G("vehicleId")); if(vehicle.G("supplier_id")!=b.G("supplier_id")||vehicle.G("id")==b.G("vehicle_id")) throw new RuleException("invalid_replacement");
            var start=b["start_at"]!.GetValue<DateTime>();var end=b["end_at"]!.GetValue<DateTime>();await BookingService.Eligible(c,vehicle,end);await BookingService.Available(c,vehicle.G("id"),start,end.AddMinutes(vehicle.Obj("rate").I("bufferMinutes",60)));
            var snapshot=(JsonObject)b.Obj("snapshot").DeepClone(); snapshot["vehicleId"]=vehicle.S("id");snapshot["vehicleName"]=vehicle.S("name");snapshot["policy"]=vehicle.Obj("policy").DeepClone();snapshot["deposit"]=vehicle.Obj("policy").I("deposit");snapshot["bufferMinutes"]=vehicle.Obj("rate").I("bufferMinutes",60);
            var qid=Guid.NewGuid();var op=Guid.NewGuid();var reason=input.Required("reason",2000);
            await Store.Exec(c,"INSERT INTO quotes(id,vehicle_id,start_at,end_at,expires_at,snapshot,total) VALUES($1,$2,$3,$4,now()+interval '24 hours',$5::jsonb,$6)",qid,vehicle.G("id"),start,end,snapshot.ToJsonString(),snapshot["total"]!.GetValue<long>());
            await Store.Exec(c,"INSERT INTO operations(id,supplier_id,booking_id,kind,data) VALUES($1,$2,$3,'replacement',$4::jsonb)",op,b.G("supplier_id"),id,new JsonObject{["quoteId"]=qid,["description"]=reason}.ToJsonString());
            await Store.Audit(c,actor.Key,"replacement.propose",op.ToString(),reason,new JsonObject{["vehicleId"]=b.S("vehicle_id")},new JsonObject{["vehicleId"]=vehicle.S("id"),["quoteId"]=qid});await BookingService.Notify(c,b,"replacement_proposed",actor.Key,reason);await tx.CommitAsync();return Results.Ok(new{id=op});
        });
        app.MapPost("/api/bookings/{id:guid}/replacement/{proposal:guid}",async(HttpContext ctx,Store db,Guid id,Guid proposal,JsonObject input)=> {
            if(!ctx.Request.Headers.ContainsKey("X-Booking-Token")) throw new RuleException("guest_access_required",403);
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();var b=await Security.Booking(ctx,c,id,true);
            var op=await Store.One(c,"SELECT * FROM operations WHERE id=$1 AND booking_id=$2 AND kind='replacement' FOR UPDATE",proposal,id);if(op.S("state")!="open"||b.S("status")!="confirmed") throw new RuleException("invalid_transition",409);
            if(!input.B("accept")) {await Store.Exec(c,"UPDATE operations SET state='rejected' WHERE id=$1",proposal);await Store.Audit(c,"guest","replacement.reject",proposal.ToString(),"Customer declined proposal",null,null);await tx.CommitAsync();return Results.Ok(new{state="rejected"});}
            var q=await Store.One(c,"SELECT * FROM quotes WHERE id=$1",op.Obj("data").G("quoteId"));if(q["expires_at"]!.GetValue<DateTime>()<=DateTime.UtcNow) throw new RuleException("quote_expired",409);if(input.S("termsVersion")!=q.Obj("snapshot").S("termsVersion")) throw new RuleException("terms_required");
            var v=await Store.One(c,BookingService.VehicleSelect+" WHERE v.id=$1 FOR SHARE OF v,s",q.G("vehicle_id"));await BookingService.Eligible(c,v,q["end_at"]!.GetValue<DateTime>());
            await Store.Exec(c,"DELETE FROM allocations WHERE booking_id=$1",id);await Store.Exec(c,"INSERT INTO allocations(id,vehicle_id,booking_id,kind,period) VALUES($1,$2,$3,'booking',tstzrange($4,$5,'[)'))",Guid.NewGuid(),v.G("id"),id,q["start_at"]!.GetValue<DateTime>(),q["end_at"]!.GetValue<DateTime>().AddMinutes(q.Obj("snapshot").I("bufferMinutes")));
            await Store.Exec(c,"UPDATE bookings SET vehicle_id=$2,quote_id=$3,accepted_terms=$4 WHERE id=$1",id,v.G("id"),q.G("id"),input.S("termsVersion"));await Store.Exec(c,"UPDATE operations SET state='accepted' WHERE id=$1",proposal);await Store.Audit(c,"guest","replacement.accept",proposal.ToString(),"Customer explicitly accepted replacement and versioned conditions",b.Obj("snapshot"),q.Obj("snapshot"));await BookingService.Notify(c,b,"confirmed","guest","Customer accepted replacement");await tx.CommitAsync();return Results.Ok(new{state="accepted"});
        });
        app.MapPost("/api/bookings/{id:guid}/review",async(HttpContext ctx,Store db,Guid id,JsonObject input)=> {
            if(!ctx.Request.Headers.ContainsKey("X-Booking-Token")) throw new RuleException("guest_access_required",403);await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();var b=await Security.Booking(ctx,c,id,true);if(b.S("status")!="completed") throw new RuleException("completed_booking_required");
            if((await Store.Query(c,"SELECT id FROM operations WHERE booking_id=$1 AND kind='review'",id)).Count>0) throw new RuleException("review_exists",409);var rating=input.I("rating");if(rating<1||rating>5) throw new RuleException("invalid_rating");var body=input.Required("text",2000);var rid=Guid.NewGuid();await Store.Exec(c,"INSERT INTO operations(id,supplier_id,booking_id,kind,data) VALUES($1,$2,$3,'review',$4::jsonb)",rid,b.G("supplier_id"),id,new JsonObject{["rating"]=rating,["description"]=body,["verifiedBooking"]=true}.ToJsonString());await Store.Audit(c,"guest","review.submit",rid.ToString(),"Completed booking review awaiting moderation",null,new JsonObject{["rating"]=rating});await tx.CommitAsync();return Results.Ok(new{id=rid,state="open"});
        });
    }
}
