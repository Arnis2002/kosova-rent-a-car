using System.Text.Json.Nodes;
using Kosova;
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
