using System.Text.Json.Nodes;
namespace Kosova;
public sealed record PartnerApplicationDetails(string CompanyName,string ContactName,string Email,string Phone,string City);
public static class PartnerApplications
{
    public static PartnerApplicationDetails ValidateApplication(JsonObject input,string verifiedEmail)
    {
        var company=input.Required("companyName",120);
        var contact=input.Required("contactName",120);
        var phone=input.Required("phone",40);
        var city=input.Required("city",100);
        var email=verifiedEmail.Trim().ToLowerInvariant();
        if(!System.Net.Mail.MailAddress.TryCreate(email,out var address)||address.Address!=email)throw new RuleException("invalid_verified_email");
        var digits=phone.Count(char.IsDigit);
        if(digits<7||phone.Any(c=>!char.IsDigit(c)&&c is not('+' or ' ' or '(' or ')' or '-' or '.')))throw new RuleException("invalid_phone");
        return new PartnerApplicationDetails(company,contact,email,phone,city);
    }
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/partner/applications",async(HttpContext ctx,Store db,JsonObject input)=>
        {
            if(ctx.User.Identity?.AuthenticationType!="SupabaseBearer"||ctx.User.FindFirst("email_verified")?.Value!="true")throw new RuleException("unauthorized",401);
            var subject=ctx.User.FindFirst("sub")?.Value;
            var email=ctx.User.FindFirst("email")?.Value;
            if(!Guid.TryParse(subject,out _)||string.IsNullOrWhiteSpace(email))throw new RuleException("unauthorized",401);
            var details=ValidateApplication(input,email);
            var id=Guid.NewGuid();
            await using var c=await db.Open();
            await Store.Exec(c,"INSERT INTO supplier_applications(id,auth_subject,company_name,contact_name,email,phone,city) VALUES($1,$2,$3,$4,$5,$6,$7)",id,subject,details.CompanyName,details.ContactName,details.Email,details.Phone,details.City);
            return Results.Ok(new{id,status="pending"});
        }).RequireRateLimiting("auth");

        app.MapGet("/api/manage/partner-applications",async(HttpContext ctx,Store db)=>
        {
            await using var c=await db.Open();
            Security.Admin(await Security.Actor(ctx,c));
            return await Store.Query(c,"SELECT id,company_name,contact_name,email,phone,city,status,supplier_id,created_at,reviewed_at,review_reason FROM supplier_applications ORDER BY created_at DESC LIMIT 300");
        });

        app.MapPost("/api/manage/partner-applications/{id:guid}/review",async(HttpContext ctx,Store db,Guid id,JsonObject input)=>
        {
            var decision=input.Required("decision",16);
            if(decision is not("approved" or "rejected"))throw new RuleException("invalid_decision");
            var reason=input.Required("reason",1000);
            await using var c=await db.Open();
            var admin=await Security.Actor(ctx,c);Security.Admin(admin);
            await using var tx=await c.BeginTransactionAsync();
            var application=await Store.One(c,"SELECT * FROM supplier_applications WHERE id=$1 FOR UPDATE",id);
            if(application.S("status")!="pending")throw new RuleException("application_already_reviewed",409);
            Guid? supplierId=null;
            if(decision=="approved")
            {
                supplierId=Guid.NewGuid();
                var profile=new JsonObject{["description"]="",["openHour"]=8,["closeHour"]=20,["responseMinutes"]=120,["locations"]=new JsonArray("prn","prishtina")};
                await Store.Exec(c,"INSERT INTO suppliers(id,name,status,demo,profile) VALUES($1,$2,'approved',false,$3::jsonb)",supplierId,application.S("company_name"),profile.ToJsonString());
                await Store.Exec(c,"INSERT INTO users(id,email,password_hash,role,supplier_id,external_subject) VALUES($1,$2,'supabase-auth-only','owner',$3,$4)",Guid.NewGuid(),application.S("email"),supplierId,application.S("auth_subject"));
            }
            await Store.Exec(c,"UPDATE supplier_applications SET status=$2,supplier_id=$3,reviewed_at=now(),reviewed_by=$4,review_reason=$5 WHERE id=$1",id,decision,supplierId,admin.Id,reason);
            await Store.Audit(c,admin.Key,"partner_application.review",id.ToString(),reason,new JsonObject{["status"]="pending"},new JsonObject{["status"]=decision,["supplierId"]=supplierId?.ToString()});
            await tx.CommitAsync();
            return Results.Ok(new{id,status=decision,supplierId});
        });
    }
}
