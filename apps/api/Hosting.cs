using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
namespace Kosova;
public enum RuntimeMode { DevelopmentDemo, HostedTest, Production }
public static class HostedSchema
{
    public const int RequiredVersion=6;
    public const string ReadinessSql="SELECT COALESCE((SELECT max(version) FROM schema_versions),0) AS version, EXISTS(SELECT 1 FROM pg_extension WHERE extname='btree_gist') AS has_btree_gist";
    public static void EnsureReady(int version,bool hasBtreeGist)
    {
        if(version<RequiredVersion) throw new InvalidOperationException($"HostedTest database schema version {version} is not ready; apply migrations through version {RequiredVersion} before starting the API");
        if(!hasBtreeGist) throw new InvalidOperationException("HostedTest database requires the btree_gist extension; enable it before starting the API");
    }
}
public static class Hosting
{
    public static RuntimeMode ResolveMode(WebApplicationBuilder builder)
    {
        var c=builder.Configuration;
        var raw=c["RuntimeMode"];
        var demoFlag=c.GetValue<bool>("DemoMode");
        RuntimeMode mode;
        if(string.IsNullOrWhiteSpace(raw))
        {
            if(demoFlag) mode=RuntimeMode.DevelopmentDemo;
            else mode=RuntimeMode.Production;
        }
        else if(!Enum.TryParse(raw,false,out mode)||!Enum.IsDefined(mode))
            throw new InvalidOperationException("RuntimeMode must be DevelopmentDemo, HostedTest, or Production");

        if(mode==RuntimeMode.DevelopmentDemo)
        {
            if(!builder.Environment.IsDevelopment())throw new InvalidOperationException("DevelopmentDemo is allowed only in Development");
            if(!demoFlag)throw new InvalidOperationException("DevelopmentDemo requires DemoMode=true");
        }
        else if(demoFlag) throw new InvalidOperationException("DemoMode=true is allowed only with RuntimeMode=DevelopmentDemo");

        if(mode==RuntimeMode.HostedTest)
        {
            if(!Uri.TryCreate(c["WebOrigin"],UriKind.Absolute,out var web)||web.Scheme!="https")throw new InvalidOperationException("HostedTest requires an HTTPS WebOrigin");
            if(!Uri.TryCreate(c["Supabase:Url"],UriKind.Absolute,out var supabase)||supabase.Scheme!="https")throw new InvalidOperationException("HostedTest requires an HTTPS Supabase:Url");
            if(string.IsNullOrWhiteSpace(c["Supabase:PublishableKey"]))throw new InvalidOperationException("HostedTest requires Supabase:PublishableKey");
        }
        return mode;
    }

    public static void Configure(WebApplicationBuilder builder,RuntimeMode mode)
    {
        var c=builder.Configuration;
        if(mode==RuntimeMode.Production) {
            foreach(var key in new[]{"WebOrigin","Oidc:Authority","Oidc:ClientId","Oidc:ClientSecret","Oidc:MfaClaim","Oidc:MfaValue","Storage:Region","Storage:Bucket","Scanner:Host","Email:Host","Email:From","Email:Username","Email:Password","DataProtection:Path","DataProtection:CertificatePath","DataProtection:CertificatePassword","RetentionDays","TermsVersion"})if(string.IsNullOrWhiteSpace(c[key]))throw new InvalidOperationException("Required production configuration: "+key);
            if(!c["WebOrigin"]!.StartsWith("https://")||!c["Oidc:Authority"]!.StartsWith("https://"))throw new InvalidOperationException("HTTPS required");
            if(c.GetValue<int>("RetentionDays")<1)throw new InvalidOperationException("RetentionDays must be positive");
        }
        var protection=builder.Services.AddDataProtection().SetApplicationName("KosovaRentACar");
        if(mode==RuntimeMode.HostedTest)protection.UseEphemeralDataProtectionProvider();
        else protection.PersistKeysToFileSystem(new DirectoryInfo(c["DataProtection:Path"]??Path.Combine(builder.Environment.ContentRootPath,"../../.local/keys")));
        if(mode==RuntimeMode.Production)protection.ProtectKeysWithCertificate(System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(c["DataProtection:CertificatePath"]!,c["DataProtection:CertificatePassword"]));
        builder.Services.AddSingleton<GuestSecrets>();builder.Services.AddSingleton<UploadScanner>();builder.Services.AddSingleton<NotificationDelivery>();
        if(mode==RuntimeMode.DevelopmentDemo)builder.Services.AddSingleton<IPrivateObjects,LocalPrivateObjects>();
        else if(mode==RuntimeMode.HostedTest)builder.Services.AddSingleton<IPrivateObjects,UnavailablePrivateObjects>();
        else builder.Services.AddSingleton<IPrivateObjects,S3PrivateObjects>();
        if(mode==RuntimeMode.HostedTest)
        {
            builder.Services.AddHttpClient();
            builder.Services.AddSingleton(sp=>new SupabaseIdentityVerifier(sp.GetRequiredService<IHttpClientFactory>().CreateClient(),c["Supabase:Url"]!,c["Supabase:PublishableKey"]!));
            builder.Services.AddAuthentication("SupabaseBearer").AddScheme<AuthenticationSchemeOptions,SupabaseAuthHandler>("SupabaseBearer",_=>{});
        }
        if(mode==RuntimeMode.Production)builder.Services.AddAuthentication(o=>{o.DefaultScheme="oidc-cookie";o.DefaultChallengeScheme="oidc";}).AddCookie("oidc-cookie",o=>{o.Cookie.Name="kr_oidc";o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.HttpOnly=true;}).AddOpenIdConnect("oidc",o=>{
            o.Authority=c["Oidc:Authority"];o.ClientId=c["Oidc:ClientId"];o.ClientSecret=c["Oidc:ClientSecret"];o.ResponseType="code";o.UsePkce=true;o.SaveTokens=false;o.MapInboundClaims=false;o.CallbackPath="/api/auth/callback";o.Scope.Add("email");
            o.Events.OnTokenValidated=ctx=>{if(!ctx.Principal!.Claims.Any(x=>x.Type==c["Oidc:MfaClaim"]&&x.Value==c["Oidc:MfaValue"]))ctx.Fail("MFA required");return Task.CompletedTask;};
            o.Events.OnTicketReceived=async ctx=>{var subject=ctx.Principal!.FindFirstValue("sub")??"";await using var db=await ctx.HttpContext.RequestServices.GetRequiredService<Store>().Open();var rows=await Store.Query(db,"SELECT id,role FROM users WHERE external_subject=$1",subject);if(rows.Count!=1){ctx.Fail("Account not provisioned");return;}var token=Security.Token();await Store.Exec(db,"INSERT INTO sessions(token_hash,user_id,expires_at) VALUES($1,$2,now()+interval '8 hours')",Security.Hash(token),rows[0].G("id"));ctx.Response.Cookies.Append("kr_session",token,new CookieOptions{HttpOnly=true,Secure=true,SameSite=SameSiteMode.Strict,MaxAge=TimeSpan.FromHours(8),Path="/"});ctx.Response.Redirect(c["WebOrigin"]+"/en/"+(rows[0].S("role")=="admin"?"admin":"dashboard"));ctx.HandleResponse();};
            o.Events.OnRedirectToIdentityProvider=ctx=>{ctx.ProtocolMessage.RedirectUri=c["WebOrigin"]+"/api/auth/callback";return Task.CompletedTask;};
        });
    }
}
