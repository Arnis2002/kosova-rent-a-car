using System.Text.Json.Nodes;
namespace Kosova;

public static class AvailabilityManagement
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/manage/allocations", async(HttpContext ctx, Store db, JsonObject input) => {
            await using var c=await db.Open();
            var actor=await Security.Actor(ctx,c);
            await using var tx=await c.BeginTransactionAsync();
            var v=await Store.One(c,"SELECT * FROM vehicles WHERE id=$1 FOR SHARE",input.G("vehicleId"));
            Security.Scope(actor,v.G("supplier_id"));
            var (start,end,kind,reason)=Validate(input);
            var buffer=v.Obj("rate").I("bufferMinutes",60);
            var id=Guid.NewGuid();
            await Store.Exec(c,"INSERT INTO allocations(id,vehicle_id,period,kind,note,scheduled_end,buffer_minutes) VALUES($1,$2,tstzrange($3,$4,'[)'),$5,$6,$7,$8)",id,v.G("id"),start,end.AddMinutes(buffer),kind,reason,end,buffer);
            await Store.Audit(c,actor.Key,"allocation.create",id.ToString(),reason,null,input);
            await tx.CommitAsync();
            return Results.Ok(new{id,revision=1});
        });

        app.MapPost("/api/manage/allocations/{id:guid}", async(HttpContext ctx, Store db, Guid id, JsonObject input) => {
            await using var c=await db.Open();
            var actor=await Security.Actor(ctx,c);
            await using var tx=await c.BeginTransactionAsync();
            var row=await Store.One(c,"SELECT a.id,a.vehicle_id,a.booking_id,a.kind,a.note,a.revision,a.buffer_minutes,lower(a.period) AS start,COALESCE(a.scheduled_end,upper(a.period)) AS end,v.supplier_id FROM allocations a JOIN vehicles v ON v.id=a.vehicle_id WHERE a.id=$1 FOR UPDATE OF a",id);
            Security.Scope(actor,row.G("supplier_id"));
            if(row["booking_id"]!=null) throw new RuleException("use_booking_transition");
            if(input.I("expectedRevision")!=row.I("revision")) throw new RuleException("availability_changed",409);
            var (start,end,kind,reason)=Validate(input);
            // The exclusion constraint checks this update atomically against all bookings and blocks.
            await Store.Exec(c,"UPDATE allocations SET period=tstzrange($2,$3,'[)'),scheduled_end=$4,kind=$5,note=$6,revision=revision+1 WHERE id=$1",id,start,end.AddMinutes(row.I("buffer_minutes")),end,kind,reason);
            await Store.Audit(c,actor.Key,"allocation.update",id.ToString(),reason,row,input);
            await tx.CommitAsync();
            return Results.Ok(new{id,revision=row.I("revision")+1});
        });

        app.MapPost("/api/manage/allocations/{id:guid}/remove", async(HttpContext ctx, Store db, Guid id, JsonObject input) => {
            await using var c=await db.Open();
            var actor=await Security.Actor(ctx,c);
            await using var tx=await c.BeginTransactionAsync();
            var row=await Store.One(c,"SELECT a.id,a.vehicle_id,a.booking_id,a.kind,a.note,a.revision,lower(a.period) AS start,upper(a.period) AS end,v.supplier_id FROM allocations a JOIN vehicles v ON v.id=a.vehicle_id WHERE a.id=$1 FOR UPDATE OF a",id);
            Security.Scope(actor,row.G("supplier_id"));
            if(row["booking_id"]!=null) throw new RuleException("use_booking_transition");
            if(input.ContainsKey("expectedRevision")&&input.I("expectedRevision")!=row.I("revision")) throw new RuleException("availability_changed",409);
            var reason=input.Required("reason",1000);
            await Store.Exec(c,"DELETE FROM allocations WHERE id=$1",id);
            await Store.Audit(c,actor.Key,"allocation.remove",id.ToString(),reason,row,null);
            await tx.CommitAsync();
            return Results.Ok(new{id});
        });
    }

    private static (DateTime Start,DateTime End,string Kind,string Reason) Validate(JsonObject input)
    {
        var start=Rules.Instant(input.Required("start"));
        var end=Rules.Instant(input.Required("end"));
        if(end<=start) throw new RuleException("invalid_interval");
        var kind=input.S("kind");
        if(kind is not("external" or "maintenance" or "blackout")) throw new RuleException("invalid_kind");
        return(start,end,kind,input.Required("reason",1000));
    }
}
