using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Npgsql;
namespace Kosova;
public record Actor(Guid Id,string Role,Guid? SupplierId) { public string Key=>Id.ToString(); public bool Admin=>Role=="admin"; }
public static class Security
{
    public static string Token()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    public static async Task<Actor> Actor(HttpContext ctx,NpgsqlConnection c)
    {
        if(!ctx.Request.Cookies.TryGetValue("kr_session",out var token)) throw new RuleException("unauthorized",401);
        var rows=await Store.Query(c,"SELECT u.id,u.role,u.supplier_id FROM sessions s JOIN users u ON u.id=s.user_id WHERE s.token_hash=$1 AND s.expires_at>now()",Hash(token));
        if(rows.Count==0) throw new RuleException("unauthorized",401); var u=rows[0]; return new Actor(u.G("id"),u.S("role"),u["supplier_id"]==null?null:u.G("supplier_id"));
    }
    public static void Scope(Actor a,Guid supplier,bool owner=false) { if(!a.Admin&&(a.SupplierId!=supplier||(owner&&a.Role!="owner"))) throw new RuleException("forbidden",403); }
    public static void Admin(Actor a) { if(!a.Admin) throw new RuleException("forbidden",403); }
    public static async Task<JsonObject> Booking(HttpContext ctx,NpgsqlConnection c,Guid id,bool locked=false)
    {
        var b=await Store.One(c,"SELECT b.*,q.snapshot,q.start_at,q.end_at FROM bookings b JOIN quotes q ON q.id=b.quote_id WHERE b.id=$1"+(locked?" FOR UPDATE OF b":""),id);
        var token=ctx.Request.Headers["X-Booking-Token"].ToString();
        if(token.Length>0&&b["access_expires_at"]!.GetValue<DateTime>()>DateTime.UtcNow&&CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(token)),Encoding.UTF8.GetBytes(b.S("access_hash")))) return b;
        if(token.Length>0) throw new RuleException("unauthorized",401);
        var a=await Actor(ctx,c); Scope(a,b.G("supplier_id")); return b;
    }
}
public static class Seed
{
    public static async Task Run(NpgsqlConnection c,string password)
    {
        if((await Store.Query(c,"SELECT id FROM suppliers LIMIT 1")).Count>0) return;
        var s=Guid.Parse("10000000-0000-0000-0000-000000000001");
        await Store.Exec(c,"INSERT INTO suppliers(id,name,status,demo,profile) VALUES($1,'Prishtina Drive · Demo','approved',true,$2::jsonb)",s,"{\"description\":\"Demonstration professional rental company. This is not a real supplier.\",\"openHour\":8,\"closeHour\":20,\"responseMinutes\":120,\"locations\":[\"prn\",\"prishtina\"]}");
        var s2=Guid.Parse("10000000-0000-0000-0000-000000000002");
        await Store.Exec(c,"INSERT INTO suppliers(id,name,status,demo,profile) VALUES($1,'Dukagjini Mobility · Demo','pending',true,'{}')",s2);
        foreach(var account in new[]{("admin@demo.local","admin",(Guid?)null),("supplier@demo.local","owner",(Guid?)s),("other@demo.local","owner",(Guid?)s2)}) {
            var hash=new PasswordHasher<string>().HashPassword(account.Item1,password);
            await Store.Exec(c,"INSERT INTO users(id,email,password_hash,role,supplier_id) VALUES($1,$2,$3,$4,$5)",Guid.NewGuid(),account.Item1,hash,account.Item2,account.Item3);
        }
        string[] names=["Volkswagen Golf","Škoda Octavia","Volkswagen Tiguan","Toyota Yaris"];
        for(int i=0;i<names.Length;i++) {
            var id=Guid.NewGuid();
            var spec=new JsonObject{["category"]=i==2?"SUV":i==1?"Estate":"Compact",["transmission"]=i==3?"Manual":"Automatic",["fuel"]=i==3?"Hybrid":"Diesel",["seats"]=5,["luggage"]=i==2?4:3,["ac"]=true,["locations"]=new JsonArray("prn","prishtina"),["pickupMethod"]="Meet and greet",["image"]=$"/cars/car-{i+1}.svg"};
            var rate=new JsonObject{["daily"]=3200+i*1100,["minDays"]=1,["maxDays"]=60,["bufferMinutes"]=60,["airportFee"]=1500,["returnFee"]=2000,["child_seat"]=500,["additional_driver"]=800,["youngDaily"]=500,["discountAfter"]=7,["discountPercent"]=10,["taxBasisPoints"]=0,["commissionBasisPoints"]=1000,["prepay"]=i==2,["seasons"]=new JsonArray()};
            var policy=new JsonObject{["version"]="demo-policy-v1",["minAge"]=21,["maxAge"]=80,["deposit"]=30000+i*10000,["depositMethod"]="Card at pickup",["mileageDaily"]=200,["fuel"]="Full to full",["insurance"]="Demonstration terms: basic damage cover; excess equals deposit. Glass, tyres and negligence excluded. Requires supplier and legal review.",["documents"]="Valid driving licence held for 2 years and passport or national ID shown at pickup.",["crossBorder"]="Not permitted unless the supplier provides written authorization.",["cancellation"]="Demo policy: free cancellation before pickup; no-show charges require manual review.",["pickup"]="Meet the supplier at the agreed pickup point. Exact instructions follow confirmation.",["return"]="Return to the agreed location at the booked time."};
            await Store.Exec(c,"INSERT INTO vehicles(id,supplier_id,name,status,spec,rate,policy) VALUES($1,$2,$3,'published',$4::jsonb,$5::jsonb,$6::jsonb)",id,s,names[i],spec.ToJsonString(),rate.ToJsonString(),policy.ToJsonString());
            foreach(var kind in new[]{"registration","insurance"}) await Store.Exec(c,"INSERT INTO documents(id,supplier_id,vehicle_id,kind,expires_on,state,object_key,original_name,content_type) VALUES($1,$2,$3,$4,$5,'approved','demo:no-real-document','Demonstration only','application/pdf')",Guid.NewGuid(),s,id,kind,DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)));
        }
    }
}
