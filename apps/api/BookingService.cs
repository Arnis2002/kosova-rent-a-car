using System.Text.Json.Nodes;
using Npgsql;
namespace Kosova;
public sealed class BookingService(Store db,GuestSecrets secrets,IConfiguration configuration)
{
    public const string VehicleSelect="SELECT v.*,s.name AS supplier_name,s.demo,s.status AS supplier_status,s.profile,(SELECT avg((o.data->>'rating')::int)::numeric(3,2) FROM operations o JOIN bookings b ON b.id=o.booking_id WHERE o.supplier_id=s.id AND o.kind='review' AND o.state='published' AND b.status='completed') AS verified_rating,(SELECT count(*) FROM operations o JOIN bookings b ON b.id=o.booking_id WHERE o.supplier_id=s.id AND o.kind='review' AND o.state='published' AND b.status='completed') AS review_count FROM vehicles v JOIN suppliers s ON s.id=v.supplier_id";
    public static async Task Eligible(NpgsqlConnection c,JsonObject v,DateTime end)
    {
        if(v.S("status")!="published"||v.S("supplier_status")!="approved") throw new RuleException("vehicle_unavailable",409);
        var docs=await Store.Query(c,"SELECT DISTINCT kind FROM documents WHERE vehicle_id=$1 AND state='approved' AND expires_on >= $2 AND kind IN ('registration','insurance')",v.G("id"),DateOnly.FromDateTime(end));
        if(docs.Count!=2) throw new RuleException("documents_expired",409);
    }
    public static async Task Available(NpgsqlConnection c,Guid vehicle,DateTime start,DateTime end)
    {
        if((await Store.Query(c,"SELECT id FROM allocations WHERE vehicle_id=$1 AND period && tstzrange($2,$3,'[)')",vehicle,start,end)).Count>0) throw new RuleException("vehicle_unavailable",409);
    }
    public static async Task Notify(NpgsqlConnection c,JsonObject b,string status,string actor,string reason)
    {
        await Store.Exec(c,"INSERT INTO booking_history(booking_id,status,actor,reason) VALUES($1,$2,$3,$4)",b.G("id"),status,actor,reason);
        await Store.Exec(c,"INSERT INTO notification_jobs(id,booking_id,status,locale,payload) VALUES($1,$2,$3,$4,$5::jsonb)",Guid.NewGuid(),b.G("id"),status,b.S("locale"),new JsonObject{["status"]=status,["bookingId"]=b.S("id"),["responseDue"]=b["response_due"]?.DeepClone(),["message"]=NotificationDelivery.Text(status,b.S("locale"))}.ToJsonString());
    }
    public async Task<JsonObject> Quote(JsonObject input)
    {
        await using var c=await db.Open(); var v=await Store.One(c,VehicleSelect+" WHERE v.id=$1",input.G("vehicleId"));
        if(!configuration.GetValue<bool>("DemoMode")&&v.Obj("rate").B("prepay")) throw new RuleException("live_provider_unconfigured",503); var start=Rules.Instant(input.Required("start")); var end=Rules.Instant(input.Required("end"));
        await Eligible(c,v,end); var snapshot=Rules.Price(v,input,start,end); snapshot["termsVersion"]=configuration["TermsVersion"]??"draft-2026-09-v1"; await Available(c,v.G("id"),start,end.AddMinutes(v.Obj("rate").I("bufferMinutes",60)));
        var id=Guid.NewGuid(); var expiry=DateTime.UtcNow.AddMinutes(15);
        await Store.Exec(c,"INSERT INTO quotes(id,vehicle_id,start_at,end_at,expires_at,snapshot,total) VALUES($1,$2,$3,$4,$5,$6::jsonb,$7)",id,v.G("id"),start,end,expiry,snapshot.ToJsonString(),snapshot["total"]!.GetValue<long>());
        return new JsonObject{["id"]=id,["expiresAt"]=expiry,["snapshot"]=snapshot};
    }
    public async Task<JsonObject> Request(JsonObject input)
    {
        await using var c=await db.Open(); await using var tx=await c.BeginTransactionAsync();
        var q=await Store.One(c,"SELECT q.*,v.supplier_id,s.profile FROM quotes q JOIN vehicles v ON v.id=q.vehicle_id JOIN suppliers s ON s.id=v.supplier_id WHERE q.id=$1",input.G("quoteId"));
        if(q["expires_at"]!.GetValue<DateTime>()<=DateTime.UtcNow) throw new RuleException("quote_expired",409);
        if(input.S("termsVersion")!=q.Obj("snapshot").S("termsVersion")||!input.B("acceptTerms")) throw new RuleException("terms_required");
        var customer=input.Obj("customer"); if(customer.I("licenceYears")<q.Obj("snapshot").Obj("policy").I("minLicenceYears",2)) throw new RuleException("driver_ineligible"); customer.Required("name",100); customer.Required("phone",40); var email=customer.Required("email",254);
        if(!System.Net.Mail.MailAddress.TryCreate(email,out _)) throw new RuleException("invalid_email");
        var v=await Store.One(c,VehicleSelect+" WHERE v.id=$1",q.G("vehicle_id")); await Eligible(c,v,q["end_at"]!.GetValue<DateTime>());
        await Available(c,v.G("id"),q["start_at"]!.GetValue<DateTime>(),q["end_at"]!.GetValue<DateTime>().AddMinutes(q.Obj("snapshot").I("bufferMinutes")));
        var locale=input.S("locale","en"); if(locale is not("en" or "sq" or "de")) throw new RuleException("invalid_locale");
        var token=Security.Token(); var id=Guid.NewGuid(); var due=Rules.Deadline(DateTime.UtcNow,q.Obj("profile"));
        if(due>=q["start_at"]!.GetValue<DateTime>()) throw new RuleException("insufficient_response_time");
        await Store.Exec(c,"INSERT INTO bookings(id,quote_id,supplier_id,vehicle_id,status,customer,access_hash,locale,response_due,accepted_terms) VALUES($1,$2,$3,$4,'awaiting_supplier',$5::jsonb,$6,$7,$8,$9)",id,q.G("id"),q.G("supplier_id"),q.G("vehicle_id"),new JsonObject{["name"]=customer.S("name"),["email"]=email,["phone"]=customer.S("phone"),["driverName"]=customer.S("driverName",customer.S("name")),["licenceYears"]=customer.I("licenceYears",2)}.ToJsonString(),Security.Hash(token),locale,due,input.S("termsVersion"));
        await Store.Exec(c,"UPDATE bookings SET access_secret=$2,access_expires_at=$3 WHERE id=$1",id,secrets.Protect(token),q["end_at"]!.GetValue<DateTime>().AddDays(configuration.GetValue("RetentionDays",180))); var b=await Store.One(c,"SELECT * FROM bookings WHERE id=$1",id); await Notify(c,b,"awaiting_supplier","guest","Request received; awaiting supplier");
        await Store.Audit(c,"guest","booking.request",id.ToString(),"Versioned terms accepted",null,new JsonObject{["status"]="awaiting_supplier",["termsVersion"]=input.S("termsVersion")});
        await tx.CommitAsync(); return new JsonObject{["id"]=id,["accessToken"]=token,["status"]="awaiting_supplier",["responseDue"]=due};
    }
    public async Task<JsonObject> Transition(HttpContext ctx,Guid id,JsonObject input)
    {
        await using var c=await db.Open(); await using var tx=await c.BeginTransactionAsync(); var b=await Security.Booking(ctx,c,id,true);
        var next=input.Required("status"); var reason=input.Required("reason",1000); var guest=ctx.Request.Headers.ContainsKey("X-Booking-Token"); Actor? actor=null;
        if(guest) { if(next!="cancelled") throw new RuleException("forbidden",403); } else { actor=await Security.Actor(ctx,c); Security.Scope(actor,b.G("supplier_id")); }
        var old=b.S("status"); if(!Rules.Transitions.TryGetValue(old,out var allowed)||!allowed.Contains(next)) throw new RuleException("invalid_transition",409);
        if(next is "confirmed" or "awaiting_payment") {
            if(old!="awaiting_supplier") throw new RuleException("payment_required",409);
            if(b["response_due"]!.GetValue<DateTime>()<=DateTime.UtcNow) throw new RuleException("request_expired",409);
            var v=await Store.One(c,VehicleSelect+" WHERE v.id=$1 FOR SHARE OF v,s",b.G("vehicle_id")); await Eligible(c,v,b["end_at"]!.GetValue<DateTime>());
            var pay=b.Obj("snapshot")["payNow"]!.GetValue<long>()>0; next=pay?"awaiting_payment":"confirmed";
            await Store.Exec(c,"INSERT INTO allocations(id,vehicle_id,booking_id,period,kind,expires_at) VALUES($1,$2,$3,tstzrange($4,$5,'[)'),$6,$7)",Guid.NewGuid(),b.G("vehicle_id"),id,b["start_at"]!.GetValue<DateTime>(),b["end_at"]!.GetValue<DateTime>().AddMinutes(b.Obj("snapshot").I("bufferMinutes")),pay?"hold":"booking",pay?DateTime.UtcNow.AddMinutes(20):(object?)null);
            if(pay) await Store.Exec(c,"UPDATE bookings SET payment_due=now()+interval '20 minutes' WHERE id=$1",id);
        }
        if(next=="active"&&DateTime.UtcNow<b["start_at"]!.GetValue<DateTime>().AddHours(-2)) throw new RuleException("pickup_too_early");
        if(next=="no_show"&&DateTime.UtcNow<b["start_at"]!.GetValue<DateTime>()) throw new RuleException("pickup_not_due");
        if(next is "cancelled" or "declined" or "expired" or "completed" or "no_show") await Store.Exec(c,"DELETE FROM allocations WHERE booking_id=$1",id);
        await Store.Exec(c,"UPDATE bookings SET status=$2,refund_state=CASE WHEN $2='cancelled' AND payment_state='paid' THEN 'requested' ELSE refund_state END WHERE id=$1",id,next);
        if(next is "active" or "completed") await Store.Exec(c,"INSERT INTO operations(id,supplier_id,booking_id,kind,state,data) VALUES($1,$2,$3,$4,'recorded',$5::jsonb)",Guid.NewGuid(),b.G("supplier_id"),id,next=="active"?"pickup":"return",input.Obj("record").ToJsonString());
        if(next=="completed") { var snap=b.Obj("snapshot"); await Store.Exec(c,"INSERT INTO commissions(booking_id,amount) VALUES($1,$2) ON CONFLICT DO NOTHING",id,snap["total"]!.GetValue<long>()*snap.I("commissionBasisPoints")/10000); }
        await Notify(c,b,next,actor?.Key??"guest",reason); await Store.Audit(c,actor?.Key??"guest","booking.transition",id.ToString(),reason,new JsonObject{["status"]=old},new JsonObject{["status"]=next}); await tx.CommitAsync(); return new JsonObject{["status"]=next};
    }
}
