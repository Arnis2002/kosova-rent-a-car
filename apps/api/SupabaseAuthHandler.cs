using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
namespace Kosova;
public sealed class SupabaseAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,SupabaseIdentityVerifier verifier):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if(!System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(),out var header)||!string.Equals(header.Scheme,"Bearer",StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(header.Parameter))return AuthenticateResult.NoResult();
        try
        {
            var user=await verifier.VerifyAsync(header.Parameter,Context.RequestAborted);
            if(user is null)return AuthenticateResult.Fail("Supabase access token is invalid or email is unverified");
            var claims=new[]{new System.Security.Claims.Claim("sub",user.Id.ToString()),new System.Security.Claims.Claim("email",user.Email),new System.Security.Claims.Claim("email_verified","true")};
            var identity=new System.Security.Claims.ClaimsIdentity(claims,Scheme.Name);
            return AuthenticateResult.Success(new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity),Scheme.Name));
        }
        catch(HttpRequestException e)
        {
            Logger.LogWarning("Supabase Auth validation unavailable: {StatusCode}",e.StatusCode);
            return AuthenticateResult.Fail("Supabase Auth validation unavailable");
        }
        catch(System.Text.Json.JsonException)
        {
            return AuthenticateResult.Fail("Supabase Auth returned an invalid response");
        }
    }
}
