namespace Kosova;
public sealed record SupabaseIdentity(Guid Id,string Email);
public sealed class SupabaseIdentityVerifier(HttpClient http,string projectUrl,string publishableKey)
{
    public async Task<SupabaseIdentity?> VerifyAsync(string accessToken,CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(accessToken)||accessToken.Length>8192)return null;
        using var request=new HttpRequestMessage(HttpMethod.Get,projectUrl.TrimEnd('/')+"/auth/v1/user");
        request.Headers.TryAddWithoutValidation("apikey",publishableKey);
        request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",accessToken);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
        if(response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)return null;
        if(!response.IsSuccessStatusCode)throw new HttpRequestException("Supabase Auth user verification failed",null,response.StatusCode);
        using var body=await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),cancellationToken:cancellationToken);
        var user=body.RootElement;
        var idText=user.TryGetProperty("id",out var idValue)?idValue.GetString():null;
        var email=user.TryGetProperty("email",out var emailValue)?emailValue.GetString():null;
        var confirmed=HasValue(user,"email_confirmed_at")||HasValue(user,"confirmed_at");
        if(!confirmed||!Guid.TryParse(idText,out var id)||string.IsNullOrWhiteSpace(email)||!System.Net.Mail.MailAddress.TryCreate(email,out _))return null;
        return new SupabaseIdentity(id,email.Trim().ToLowerInvariant());
    }
    static bool HasValue(System.Text.Json.JsonElement obj,string name)=>obj.TryGetProperty(name,out var value)&&value.ValueKind==System.Text.Json.JsonValueKind.String&&!string.IsNullOrWhiteSpace(value.GetString());
}
