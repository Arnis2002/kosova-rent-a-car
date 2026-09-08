using System.Text.Json.Nodes;
namespace Kosova;
public static class Rules
{
    public static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Belgrade");
    public static readonly Dictionary<string,string[]> Transitions=new() {
        ["awaiting_supplier"]=["confirmed","awaiting_payment","declined","expired","cancelled"],
        ["awaiting_payment"]=["confirmed","expired","cancelled"], ["confirmed"]=["active","cancelled","no_show"], ["active"]=["completed"]
    };
    public static DateTime Instant(string s)
    {
        if(!(s.EndsWith('Z')||System.Text.RegularExpressions.Regex.IsMatch(s,@"[+-]\d{2}:\d{2}$"))||!DateTimeOffset.TryParse(s,out var d)) throw new RuleException("invalid_datetime");
        return d.UtcDateTime;
    }
    public static DateTime LocalInstant(string s)
    {
        if(!DateTime.TryParseExact(s,"yyyy-MM-ddTHH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var d)) throw new RuleException("invalid_datetime");
        d=DateTime.SpecifyKind(d,DateTimeKind.Unspecified);
        if(Zone.IsInvalidTime(d)||Zone.IsAmbiguousTime(d)) throw new RuleException("ambiguous_datetime");
        return TimeZoneInfo.ConvertTimeToUtc(d,Zone);
    }
    public static void Interval(DateTime start,DateTime end,int min=1,int max=60)
    { var days=Math.Ceiling((end-start).TotalDays); if(start<=DateTime.UtcNow||end<=start||days<min||days>max) throw new RuleException("invalid_interval"); }
    public static DateTime Deadline(DateTime now,JsonObject profile)
    {
        var local=TimeZoneInfo.ConvertTimeFromUtc(now,Zone); var remaining=profile.I("responseMinutes",120);
        if(remaining<15||remaining>480) remaining=120;
        var open=profile.I("openHour",8); var close=profile.I("closeHour",20); var days=profile["openDays"] as JsonArray??new JsonArray(0,1,2,3,4,5,6); if(days.Count==0) throw new RuleException("supplier_closed");
        while(remaining>0) { local=local.AddMinutes(1); if(local.Hour>=open&&local.Hour<close&&days.Any(x=>x!.GetValue<int>()==(int)local.DayOfWeek)) remaining--; }
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local,DateTimeKind.Unspecified),Zone);
    }
    public static JsonObject Price(JsonObject vehicle,JsonObject input,DateTime start,DateTime end)
    {
        var rate=vehicle.Obj("rate"); var policy=vehicle.Obj("policy"); var spec=vehicle.Obj("spec");
        Interval(start,end,rate.I("minDays",1),rate.I("maxDays",60));
        var profile=vehicle.Obj("profile"); var openDays=profile["openDays"] as JsonArray??new JsonArray(0,1,2,3,4,5,6); foreach(var instant in new[]{start,end}) { var local=TimeZoneInfo.ConvertTimeFromUtc(instant,Zone); if(!profile.B("afterHoursPickup")&&(local.Hour<profile.I("openHour",8)||local.Hour>=profile.I("closeHour",20)||!openDays.Any(x=>x!.GetValue<int>()==(int)local.DayOfWeek))) throw new RuleException("outside_operating_hours"); } int age=input.I("age"); if(age<policy.I("minAge",21)||age>policy.I("maxAge",80)) throw new RuleException("driver_ineligible");
        var pickup=input.Required("pickup"); var dropoff=input.S("dropoff",pickup);
        var locations=spec["locations"] as JsonArray ?? new JsonArray();
        if(!locations.Any(x=>x?.GetValue<string>()==pickup)||!locations.Any(x=>x?.GetValue<string>()==dropoff)) throw new RuleException("location_unavailable");
        int days=(int)Math.Ceiling((end-start).TotalDays); var lines=new JsonArray(); long subtotal=0;
        void Add(string key,long amount,int quantity=1) { if(amount!=0) { lines.Add(new JsonObject {["key"]=key,["amount"]=amount,["quantity"]=quantity}); subtotal=checked(subtotal+amount); } }
        long rental=0;
        for(int i=0;i<days;i++) { var local=TimeZoneInfo.ConvertTimeFromUtc(start.AddDays(i),Zone).ToString("MM-dd"); var daily=rate.I("daily",3500); foreach(var season in rate["seasons"] as JsonArray??new JsonArray()) if(season!=null&&string.CompareOrdinal(local,season.S("from"))>=0&&string.CompareOrdinal(local,season.S("to"))<=0) daily=season.I("daily"); rental=checked(rental+daily); }
        Add("rental",rental,days); if(days>=rate.I("discountAfter",7)) Add("duration_discount",-rental*rate.I("discountPercent",0)/100);
        if(pickup=="prn") Add("airport_delivery",rate.I("airportFee")); if(dropoff!=pickup) Add("different_return",rate.I("returnFee"));
        if(age<25) Add("young_driver",(long)rate.I("youngDaily")*days,days);
        foreach(var extra in input["extras"] as JsonArray??new JsonArray()) { var key=extra?.GetValue<string>()??""; if(key is not ("child_seat" or "additional_driver")) throw new RuleException("invalid_extra"); Add(key,(long)rate.I(key)*days,days); }
        if((input["extras"] as JsonArray)?.Select(x=>x!.ToJsonString()).Distinct().Count()!=(input["extras"] as JsonArray)?.Count) throw new RuleException("duplicate_extra");
        Add("tax",subtotal*rate.I("taxBasisPoints")/10000);
        var payNow=rate.B("prepay")?subtotal:0;
        return new JsonObject { ["version"]=1,["vehicleName"]=vehicle.S("name"),["vehicleId"]=vehicle.S("id"),["supplierName"]=vehicle.S("supplier_name"),["start"]=start,["end"]=end,["pickup"]=pickup,["dropoff"]=dropoff,["age"]=age,["days"]=days,["lines"]=lines,["total"]=subtotal,["currency"]="EUR",["payNow"]=payNow,["payAtPickup"]=subtotal-payNow,["deposit"]=policy.I("deposit"),["policy"]=policy.DeepClone(),["termsVersion"]="draft-2026-09-v1",["bufferMinutes"]=rate.I("bufferMinutes",60),["demo"]=vehicle.B("demo"),["commissionBasisPoints"]=rate.I("commissionBasisPoints",1000) };
    }
}
