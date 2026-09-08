using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
namespace Kosova;
public static class Hosting
{
    public static void Configure(WebApplicationBuilder builder,bool demo)
    {
        var c=builder.Configuration;
        if(!demo) {
            foreach(var key in new[]{"WebOrigin","Oidc:Authority","Oidc:ClientId","Oidc:ClientSecret","Oidc:MfaClaim","Oidc:MfaValue","Storage:Region","Storage:Bucket","Scanner:Host","Email:Host","Email:From","Email:Username","Email:Password","DataProtection:Path","DataProtection:CertificatePath","DataProtection:CertificatePassword","RetentionDays","TermsVersion"})if(string.IsNullOrWhiteSpace(c[key]))throw new InvalidOperationException("Required production configuration: "+key);
            if(!c["WebOrigin"]!.StartsWith("https://")||!c["Oidc:Authority"]!.StartsWith("https://"))throw new InvalidOperationException("HTTPS required");
            if(c.GetValue<int>("RetentionDays")<1)throw new InvalidOperationException("RetentionDays must be positive");
        }
        if(demo&&!builder.Environment.IsDevelopment())throw new InvalidOperationException("DemoMode is allowed only in Development; use an isolated private staging environment.");
        var protection=builder.Services.AddDataProtection().SetApplicationName("KosovaRentACar").PersistKeysToFileSystem(new DirectoryInfo(c["DataProtection:Path"]??Path.Combine(builder.Environment.ContentRootPath,"../../.local/keys")));
        if(!demo)protection.ProtectKeysWithCertificate(System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(c["DataProtection:CertificatePath"]!,c["DataProtection:CertificatePassword"]));
        builder.Services.AddSingleton<GuestSecrets>();builder.Services.AddSingleton<UploadScanner>();builder.Services.AddSingleton<NotificationDelivery>();
        if(demo)builder.Services.AddSingleton<IPrivateObjects,LocalPrivateObjects>();else builder.Services.AddSingleton<IPrivateObjects,S3PrivateObjects>();
        if(!demo)builder.Services.AddAuthentication(o=>{o.DefaultScheme="oidc-cookie";o.DefaultChallengeScheme="oidc";}).AddCookie("oidc-cookie",o=>{o.Cookie.Name="kr_oidc";o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.HttpOnly=true;}).AddOpenIdConnect("oidc",o=>{
            o.Authority=c["Oidc:Authority"];o.ClientId=c["Oidc:ClientId"];o.ClientSecret=c["Oidc:ClientSecret"];o.ResponseType="code";o.UsePkce=true;o.SaveTokens=false;o.MapInboundClaims=false;o.CallbackPath="/api/auth/callback";o.Scope.Add("email");
            o.Events.OnTokenValidated=ctx=>{if(!ctx.Principal!.Claims.Any(x=>x.Type==c["Oidc:MfaClaim"]&&x.Value==c["Oidc:MfaValue"]))ctx.Fail("MFA required");return Task.CompletedTask;};
            o.Events.OnTicketReceived=async ctx=>{var subject=ctx.Principal!.FindFirstValue("sub")??"";await using var db=await ctx.HttpContext.RequestServices.GetRequiredService<Store>().Open();var rows=await Store.Query(db,"SELECT id,role FROM users WHERE external_subject=$1",subject);if(rows.Count!=1){ctx.Fail("Account not provisioned");return;}var token=Security.Token();await Store.Exec(db,"INSERT INTO sessions(token_hash,user_id,expires_at) VALUES($1,$2,now()+interval '8 hours')",Security.Hash(token),rows[0].G("id"));ctx.Response.Cookies.Append("kr_session",token,new CookieOptions{HttpOnly=true,Secure=true,SameSite=SameSiteMode.Strict,MaxAge=TimeSpan.FromHours(8),Path="/"});ctx.Response.Redirect(c["WebOrigin"]+"/en/"+(rows[0].S("role")=="admin"?"admin":"dashboard"));ctx.HandleResponse();};
            o.Events.OnRedirectToIdentityProvider=ctx=>{ctx.ProtocolMessage.RedirectUri=c["WebOrigin"]+"/api/auth/callback";return Task.CompletedTask;};
        });
    }
}
