using Kosova;
using Npgsql;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;
var builder=WebApplication.CreateBuilder(args);
var demo=builder.Configuration.GetValue<bool>("DemoMode");
Hosting.Configure(builder,demo);
var connection=builder.Configuration.GetConnectionString("Database")??throw new InvalidOperationException("ConnectionStrings__Database is required");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connection)); builder.Services.AddSingleton<Store>(); builder.Services.AddSingleton<BookingService>();
builder.Services.AddHostedService<OperationsWorker>(); builder.Services.AddSingleton(new RuntimeOptions(demo,builder.Configuration["PrivateStorage"]??Path.Combine(builder.Environment.ContentRootPath,"../../.local/documents")));
builder.Services.AddRateLimiter(o=> { o.RejectionStatusCode=429; o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=builder.Configuration.GetValue("RateLimit:RequestsPerMinute",120),Window=TimeSpan.FromMinutes(1),QueueLimit=0})); o.AddPolicy("auth",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=builder.Configuration.GetValue("RateLimit:AuthRequestsPerMinute",10),Window=TimeSpan.FromMinutes(1)})); });
var app=builder.Build();
app.Use(async(ctx,next)=> {
    ctx.Response.Headers["X-Content-Type-Options"]="nosniff"; ctx.Response.Headers["Referrer-Policy"]="no-referrer"; ctx.Response.Headers["Cache-Control"]="no-store";
    try {
        if(ctx.Request.ContentType?.Contains("application/json")==true&&ctx.Request.ContentLength>65536)throw new RuleException("payload_too_large",413);
        if(ctx.Request.Method is not("GET" or "HEAD" or "OPTIONS") && ctx.Request.Path!="/api/auth/callback") { var origin=ctx.Request.Headers.Origin.ToString(); if(origin.Length>0&&origin!=(builder.Configuration["WebOrigin"]??"http://127.0.0.1:3000")) throw new RuleException("invalid_origin",403); }
        await next();
    } catch(RuleException e) { ctx.Response.StatusCode=e.Status; await ctx.Response.WriteAsJsonAsync(new{error=e.Message}); }
    catch(System.Text.Json.JsonException) {ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{error="invalid_request"});}
    catch(PostgresException e) when(e.SqlState is "23P01" or "23505") { ctx.Response.StatusCode=409; await ctx.Response.WriteAsJsonAsync(new{error="conflict"}); }
    catch(Exception e) { app.Logger.LogError("Request failed: {Type}; trace {Trace}",e.GetType().Name,ctx.TraceIdentifier); ctx.Response.StatusCode=500; await ctx.Response.WriteAsJsonAsync(new{error="server_error",trace=ctx.TraceIdentifier}); }
});
app.UseRateLimiter();
if(!demo) app.UseAuthentication();
app.MapGet("/api/auth/config",()=>Results.Ok(new{demo,oidc=!demo}));
app.MapGet("/api/auth/oidc",()=> !demo?Results.Challenge(new Microsoft.AspNetCore.Authentication.AuthenticationProperties(),["oidc"]):Results.NotFound());
await using(var c=await app.Services.GetRequiredService<Store>().Open()) {
    await Store.Exec(c,"SELECT pg_advisory_lock(884021)");
    try { var exists=await Store.One(c,"SELECT to_regclass('public.schema_versions') IS NOT NULL AS exists"); if(!exists.B("exists")) await Store.Exec(c,await File.ReadAllTextAsync(Path.Combine(app.Environment.ContentRootPath,"Sql/001_initial.sql"))); if((await Store.Query(c,"SELECT version FROM schema_versions WHERE version=2")).Count==0) await Store.Exec(c,await File.ReadAllTextAsync(Path.Combine(app.Environment.ContentRootPath,"Sql/002_operations.sql"))); if(demo) await Seed.Run(c,builder.Configuration["DemoPassword"]??throw new InvalidOperationException("DemoPassword is required for explicit seed")); }
    finally { await Store.Exec(c,"SELECT pg_advisory_unlock(884021)"); }
    if(!demo && (await Store.Query(c,"SELECT id FROM users LIMIT 1")).Count==0) { var subject=builder.Configuration["BootstrapAdminSubject"]??throw new InvalidOperationException("BootstrapAdminSubject required for first start");var email=builder.Configuration["BootstrapAdminEmail"]??throw new InvalidOperationException("BootstrapAdminEmail required for first start");await Store.Exec(c,"INSERT INTO users(id,email,password_hash,role,external_subject) VALUES($1,$2,'oidc-only','admin',$3)",Guid.NewGuid(),email,subject); }
    if(!demo && (await Store.Query(c,"SELECT id FROM suppliers WHERE demo=true LIMIT 1")).Count>0) throw new InvalidOperationException("Production requires a database without demonstration inventory");
}
if(args.Contains("--migrate-only")) return;
app.MapGet("/api/health",async(Store db)=> { await using var c=await db.Open(); await Store.Exec(c,"SELECT 1"); return Results.Ok(new{status="ok",demo}); });
app.MapGet("/api/locations",async(Store db)=> { await using var c=await db.Open(); return await Store.Query(c,"SELECT * FROM locations ORDER BY id"); });
app.MapPost("/api/time",(JsonObject input)=>Results.Ok(new{start=Rules.LocalInstant(input.Required("start")),end=Rules.LocalInstant(input.Required("end"))}));
app.MapPost("/api/auth/login",async(HttpContext ctx,Store db,JsonObject input)=> {
    await using var c=await db.Open(); if(!demo) throw new RuleException("use_oidc",403); var email=input.Required("email").ToLowerInvariant(); var rows=await Store.Query(c,"SELECT * FROM users WHERE email=$1",email);
    if(rows.Count!=1||rows[0].S("password_hash")=="revoked"||new PasswordHasher<string>().VerifyHashedPassword(email,rows[0].S("password_hash"),input.Required("password"))==PasswordVerificationResult.Failed) throw new RuleException("invalid_credentials",401);
    var token=Security.Token(); await Store.Exec(c,"INSERT INTO sessions(token_hash,user_id,expires_at) VALUES($1,$2,now()+interval '8 hours')",Security.Hash(token),rows[0].G("id"));
    ctx.Response.Cookies.Append("kr_session",token,new CookieOptions{HttpOnly=true,SameSite=SameSiteMode.Strict,Secure=!app.Environment.IsDevelopment(),MaxAge=TimeSpan.FromHours(8),Path="/"}); return Results.Ok(new{role=rows[0].S("role")});
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/logout",async(HttpContext ctx,Store db)=>{ await using var c=await db.Open(); if(ctx.Request.Cookies.TryGetValue("kr_session",out var t)) await Store.Exec(c,"DELETE FROM sessions WHERE token_hash=$1",Security.Hash(t)); ctx.Response.Cookies.Delete("kr_session"); return Results.NoContent(); });
app.MapGet("/api/auth/me",async(HttpContext ctx,Store db)=>{ await using var c=await db.Open(); return await Security.Actor(ctx,c); });
app.MapPost("/api/search",async(Store db,JsonObject input)=> {
    var start=Rules.Instant(input.Required("start")); var end=Rules.Instant(input.Required("end")); Rules.Interval(start,end);
    await using var c=await db.Open(); var vehicles=await Store.Query(c,BookingService.VehicleSelect+" WHERE v.status='published' AND s.status='approved' AND s.demo=$1 ORDER BY v.id",demo); var results=new List<JsonObject>();
    foreach(var v in vehicles) try { await BookingService.Eligible(c,v,end); var price=Rules.Price(v,input,start,end); await BookingService.Available(c,v.G("id"),start,end.AddMinutes(v.Obj("rate").I("bufferMinutes",60))); v.Remove("profile"); v["price"]=price; results.Add(v); } catch(RuleException) { }
    return Results.Ok(results.OrderBy(v=>v.Obj("price")["total"]!.GetValue<long>()).ThenBy(v=>v.S("id")));
});
app.MapGet("/api/vehicles/{id:guid}",async(Store db,Guid id)=>{ await using var c=await db.Open(); return await Store.One(c,BookingService.VehicleSelect+" WHERE v.id=$1 AND v.status='published' AND s.status='approved' AND s.demo=$2",id,demo); });
app.MapGet("/api/suppliers/{id:guid}",async(Store db,Guid id)=>{ await using var c=await db.Open(); return await Store.One(c,"SELECT id,name,profile,demo FROM suppliers WHERE id=$1 AND status='approved' AND demo=$2",id,demo); });
app.MapPost("/api/quotes",(BookingService service,JsonObject input)=>service.Quote(input));
app.MapPost("/api/bookings",(BookingService service,JsonObject input)=>service.Request(input)).RequireRateLimiting("auth");
app.MapGet("/api/bookings/{id:guid}",async(HttpContext ctx,Store db,Guid id)=> {
    await using var c=await db.Open(); var b=await Security.Booking(ctx,c,id); b.Remove("access_hash"); b.Remove("access_secret");
    b["proposals"]=System.Text.Json.JsonSerializer.SerializeToNode(await Store.Query(c,"SELECT o.id,o.state,o.data,q.snapshot,q.expires_at FROM operations o JOIN quotes q ON q.id=(o.data->>'quoteId')::uuid WHERE o.booking_id=$1 AND o.kind='replacement' ORDER BY o.created_at",id));
    b["history"]=System.Text.Json.JsonSerializer.SerializeToNode(await Store.Query(c,"SELECT status,reason,created_at FROM booking_history WHERE booking_id=$1 ORDER BY id",id));
    b["payments"]=System.Text.Json.JsonSerializer.SerializeToNode(await Store.Query(c,"SELECT id,provider,amount,state FROM payments WHERE booking_id=$1",id));
    b["notifications"]=System.Text.Json.JsonSerializer.SerializeToNode(await Store.Query(c,"SELECT status,state,locale,delivered_at FROM notification_jobs WHERE booking_id=$1 ORDER BY available_at",id)); return b;
});
app.MapPost("/api/bookings/{id:guid}/transition",(HttpContext ctx,BookingService service,Guid id,JsonObject input)=>service.Transition(ctx,id,input));
app.MapPost("/api/support",async(Store db,JsonObject input)=> { await using var c=await db.Open(); var email=input.Required("email",254); if(!System.Net.Mail.MailAddress.TryCreate(email,out _)) throw new RuleException("invalid_email"); var message=input.Required("message",4000); var id=Guid.NewGuid(); await Store.Exec(c,"INSERT INTO operations(id,supplier_id,kind,data) SELECT $1,id,'support',$2::jsonb FROM suppliers WHERE status='approved' ORDER BY id LIMIT 1",id,new JsonObject{["email"]=email,["message"]=message}.ToJsonString()); return Results.Ok(new{id,status="received"}); }).RequireRateLimiting("auth");
Management.Map(app); Payments.Map(app); CustomerOperations.Map(app); Media.Map(app);
app.Run();
public record RuntimeOptions(bool Demo,string PrivateStorage);
public partial class Program { }

