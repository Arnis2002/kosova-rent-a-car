using System.Text.Json.Nodes;
using Kosova;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.DataProtection;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
namespace Kosova.Tests;
public class RulesTests
{
    static JsonObject Vehicle()=>JsonNode.Parse("""
    {"name":"Exact car","id":"20000000-0000-0000-0000-000000000001","demo":true,"profile":{"afterHoursPickup":true},"spec":{"locations":["prn","prishtina"]},"rate":{"daily":3000,"minDays":1,"maxDays":60,"airportFee":1500,"returnFee":2000,"child_seat":500,"additional_driver":800,"youngDaily":500,"discountAfter":7,"discountPercent":10,"taxBasisPoints":1800,"bufferMinutes":60,"seasons":[]},"policy":{"minAge":21,"maxAge":80,"deposit":50000,"version":"v1"}}
    """)!.AsObject();
    static JsonObject Input(int age=30)=>new(){["pickup"]="prn",["dropoff"]="prn",["age"]=age,["extras"]=new JsonArray()};
    [Fact] public void PartialDayRoundsUpAndDepositIsSeparate(){var start=DateTime.UtcNow.AddDays(10);var q=Rules.Price(Vehicle(),Input(),start,start.AddHours(25));Assert.Equal(2,q.I("days"));Assert.Equal(8850,q["total"]!.GetValue<long>());Assert.Equal(50000,q.I("deposit"));Assert.Equal(0,q["payNow"]!.GetValue<long>());}
    [Fact] public void TaxDiscountExtrasAndFeesUseIntegerMinorUnits(){var v=Vehicle();var i=Input(22);i["dropoff"]="prishtina";i["extras"]=new JsonArray("child_seat");var s=DateTime.UtcNow.AddDays(10);var q=Rules.Price(v,i,s,s.AddDays(7));Assert.Equal(34692,q["total"]!.GetValue<long>());}
    [Fact] public void DuplicateExtrasRejected(){var i=Input();i["extras"]=new JsonArray("child_seat","child_seat");var s=DateTime.UtcNow.AddDays(3);Assert.Throws<RuleException>(()=>Rules.Price(Vehicle(),i,s,s.AddDays(1)));}
    [Theory] [InlineData(18)] [InlineData(81)] public void AgeRulesEnforced(int age){var s=DateTime.UtcNow.AddDays(5);Assert.Throws<RuleException>(()=>Rules.Price(Vehicle(),Input(age),s,s.AddDays(1)));}
    [Theory] [InlineData("2027-03-28T02:30")] [InlineData("2027-10-31T02:30")] public void InvalidOrAmbiguousLocalTimesRejected(string value)=>Assert.Throws<RuleException>(()=>Rules.LocalInstant(value));
    [Fact] public void DstUsesElapsedHours(){var s=Rules.LocalInstant("2027-03-27T12:00");var e=Rules.LocalInstant("2027-03-28T12:00");Assert.Equal(23,(e-s).TotalHours);}
    [Fact] public void OffsetRequired()=>Assert.Throws<RuleException>(()=>Rules.Instant("2027-04-02T12:00"));
    [Fact] public void ResponseDeadlineCountsOperatingMinutes(){var now=Rules.LocalInstant("2027-04-02T19:30");var due=Rules.Deadline(now,new JsonObject{["openHour"]=8,["closeHour"]=20,["responseMinutes"]=120});var local=TimeZoneInfo.ConvertTimeFromUtc(due,Rules.Zone);Assert.Equal(3,local.Day);Assert.Equal(9,local.Hour);Assert.Equal(30,local.Minute);}
    [Fact] public void SeasonalBoundariesUseKosovoCalendar(){var v=Vehicle();v.Obj("rate")["seasons"]=new JsonArray(new JsonObject{["from"]="07-01",["to"]="07-02",["daily"]=5000});v.Obj("rate")["taxBasisPoints"]=0;var s=Rules.LocalInstant("2027-06-30T23:30");var q=Rules.Price(v,Input(),s,s.AddDays(3));Assert.Equal(14500,q["total"]!.GetValue<long>());}
    [Fact] public void TerminalStatesCannotTransition(){foreach(var s in new[]{"cancelled","expired","completed","no_show","declined"})Assert.False(Rules.Transitions.ContainsKey(s));}
    [Fact] public void WebhookSignatureVerified(){var p=new TestPaymentProvider("test-secret");var body="{\"state\":\"succeeded\"}";Assert.True(p.Verify(body,p.Sign(body)));Assert.False(p.Verify(body+" ",p.Sign(body)));Assert.False(p.Verify(body,"bad"));}
}
public class HostingModeTests
{
    static WebApplicationBuilder Builder(string environment,Dictionary<string,string?> values)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName=environment});
        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }
    [Fact] public void HostedTestModeRunsInProductionEnvironmentWithoutProductionIntegrations()
    {
        var builder=Builder(Environments.Production,new Dictionary<string,string?>
        {
            ["RuntimeMode"]="HostedTest",
            ["WebOrigin"]="https://rent-kosova.example",
            ["Supabase:Url"]="https://project.supabase.co",
            ["Supabase:PublishableKey"]="sb_publishable_test"
        });
        var exception=Record.Exception(()=>Hosting.Configure(builder,Hosting.ResolveMode(builder)));
        Assert.Null(exception);
        using var services=builder.Services.BuildServiceProvider();
        Assert.IsType<UnavailablePrivateObjects>(services.GetRequiredService<IPrivateObjects>());
        Assert.NotNull(services.GetRequiredService<IDataProtectionProvider>());
    }
    [Fact] public void ProductionModeStillRequiresProductionIntegrations()
    {
        var builder=Builder(Environments.Production,new Dictionary<string,string?>{{"RuntimeMode","Production"}});
        Assert.Throws<InvalidOperationException>(()=>Hosting.Configure(builder,Hosting.ResolveMode(builder)));
    }
    [Fact] public void UnknownRuntimeModeIsRejectedExplicitly()
    {
        var builder=Builder(Environments.Production,new Dictionary<string,string?>{{"RuntimeMode","Preview"}});
        var error=Assert.Throws<InvalidOperationException>(()=>Hosting.Configure(builder,Hosting.ResolveMode(builder)));
        Assert.Contains("RuntimeMode",error.Message);
    }
    [Fact] public void DevelopmentDemoCannotBeEnabledInPublicHosting()
    {
        var builder=Builder(Environments.Production,new Dictionary<string,string?>
        {
            ["RuntimeMode"]="DevelopmentDemo",
            ["DemoMode"]="true"
        });
        var error=Assert.Throws<InvalidOperationException>(()=>Hosting.Configure(builder,Hosting.ResolveMode(builder)));
        Assert.Contains("Development",error.Message);
    }
}
public class HostedTestSafeguardTests
{
    [Theory] [InlineData(0,false)] [InlineData(5,true)] [InlineData(6,false)]
    public void HostedSchemaRejectsIncompleteSetup(int version,bool extension)
        =>Assert.Throws<InvalidOperationException>(()=>HostedSchema.EnsureReady(version,extension));
    [Fact] public void HostedSchemaAcceptsAppliedSetup()=>HostedSchema.EnsureReady(6,true);
    [Fact] public async Task HostedTestNotificationsAreSuppressedWithoutSmtpSettings()
    {
        var services=new ServiceCollection();services.AddDataProtection();using var provider=services.BuildServiceProvider();
        var delivery=new NotificationDelivery(new ConfigurationBuilder().Build(),new GuestSecrets(provider.GetRequiredService<IDataProtectionProvider>()),new RuntimeOptions(RuntimeMode.HostedTest,""));
        await delivery.Send(new JsonObject{["status"]="expired",["locale"]="en"},new JsonObject());
    }
    [Fact] public async Task HostedTestPrivateStorageRejectsWrites()
    {
        var storage=new UnavailablePrivateObjects();
        var error=await Assert.ThrowsAsync<RuleException>(()=>storage.Put("test",[1,2,3],"image/png"));
        Assert.Equal(503,error.Status);
    }
}
public class SupabaseIdentityVerifierTests
{
    const string UserJson="""{"id":"c65d97c4-8bd0-4c14-a361-f3d97e8b6a11","email":"owner@example.test","email_confirmed_at":"2026-10-10T10:00:00Z"}""";
    [Fact] public async Task ValidSupabaseUserResponseProducesVerifiedIdentityAndSendsRequiredHeaders()
    {
        HttpRequestMessage? seen=null;
        using var http=new HttpClient(new StubHandler(request=>{seen=request;return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(UserJson)};}));
        var verifier=new SupabaseIdentityVerifier(http,"https://project.supabase.co","sb_publishable_test");
        var identity=await verifier.VerifyAsync("valid-access-token",CancellationToken.None);
        Assert.NotNull(identity);
        Assert.Equal(Guid.Parse("c65d97c4-8bd0-4c14-a361-f3d97e8b6a11"),identity.Id);
        Assert.Equal("owner@example.test",identity.Email);
        Assert.Equal("https://project.supabase.co/auth/v1/user",seen!.RequestUri!.ToString());
        Assert.Equal("sb_publishable_test",seen.Headers.GetValues("apikey").Single());
        Assert.Equal("Bearer valid-access-token",seen.Headers.Authorization!.ToString());
    }
    [Fact] public async Task UnconfirmedSupabaseEmailDoesNotProduceIdentity()
    {
        using var http=new HttpClient(new StubHandler(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("""{"id":"c65d97c4-8bd0-4c14-a361-f3d97e8b6a11","email":"owner@example.test"}""")}));
        var verifier=new SupabaseIdentityVerifier(http,"https://project.supabase.co","sb_publishable_test");
        Assert.Null(await verifier.VerifyAsync("unconfirmed-token",CancellationToken.None));
    }
    [Fact] public async Task SupabaseAuthRejectsInvalidAccessToken()
    {
        using var http=new HttpClient(new StubHandler(_=>new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var verifier=new SupabaseIdentityVerifier(http,"https://project.supabase.co","sb_publishable_test");
        Assert.Null(await verifier.VerifyAsync("expired-token",CancellationToken.None));
    }
    [Fact] public async Task AuthenticationHandlerEmitsOnlySupabaseVerifiedIdentityClaims()
    {
        using var http=new HttpClient(new StubHandler(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(UserJson)}));
        var verifier=new SupabaseIdentityVerifier(http,"https://project.supabase.co","sb_publishable_test");
        var handler=new SupabaseAuthHandler(new TestOptionsMonitor<AuthenticationSchemeOptions>(new()),NullLoggerFactory.Instance,UrlEncoder.Default,verifier);
        var context=new DefaultHttpContext();context.Request.Headers.Authorization="Bearer verified-token";
        await handler.InitializeAsync(new AuthenticationScheme("SupabaseBearer","Supabase bearer",typeof(SupabaseAuthHandler)),context);
        var result=await handler.AuthenticateAsync();
        Assert.True(result.Succeeded);
        Assert.Equal("c65d97c4-8bd0-4c14-a361-f3d97e8b6a11",result.Principal!.FindFirst("sub")!.Value);
        Assert.Equal("owner@example.test",result.Principal.FindFirst("email")!.Value);
        Assert.Equal("true",result.Principal.FindFirst("email_verified")!.Value);
        Assert.Equal("SupabaseBearer",result.Principal.Identity!.AuthenticationType);
    }
    sealed class StubHandler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(respond(request));
    }
    sealed class TestOptionsMonitor<T>(T value):IOptionsMonitor<T> where T:class
    {
        public T CurrentValue=>value;
        public T Get(string? name)=>value;
        public IDisposable? OnChange(Action<T,string?> listener)=>null;
    }
}
public class PartnerApplicationValidationTests
{
    static JsonObject Valid()=>new(){["companyName"]="  Prishtina Rentals ",["contactName"]=" Arben K. ",["email"]="attacker@example.test",["phone"]=" +383 44 123 456 ",["city"]=" Prishtina "};
    [Fact] public void ApplicationUsesVerifiedAuthEmailAndTrimsBusinessFields()
    {
        var data=PartnerApplications.ValidateApplication(Valid(),"Owner@Example.test");
        Assert.Equal("Prishtina Rentals",data.CompanyName);
        Assert.Equal("Arben K.",data.ContactName);
        Assert.Equal("owner@example.test",data.Email);
        Assert.Equal("+383 44 123 456",data.Phone);
        Assert.Equal("Prishtina",data.City);
    }
    [Theory]
    [InlineData("companyName")]
    [InlineData("contactName")]
    [InlineData("phone")]
    [InlineData("city")]
    public void ApplicationRejectsMissingRequiredBusinessField(string field)
    {
        var data=Valid();data[field]=" ";
        Assert.Throws<RuleException>(()=>PartnerApplications.ValidateApplication(data,"owner@example.test"));
    }
    [Fact] public void ApplicationRejectsInvalidVerifiedEmail()
    {
        Assert.Throws<RuleException>(()=>PartnerApplications.ValidateApplication(Valid(),"not-an-email"));
    }
}
