using Npgsql;
using System.Text.Json.Nodes;

namespace Kosova;
public sealed class Store(NpgsqlDataSource source)
{
    public Task<NpgsqlConnection> Open() => source.OpenConnectionAsync().AsTask();
    public static async Task<List<JsonObject>> Query(NpgsqlConnection c, string sql, params object?[] args)
    {
        await using var cmd = new NpgsqlCommand(sql,c);
        foreach(var arg in args) cmd.Parameters.Add(new NpgsqlParameter { Value=arg??DBNull.Value });
        await using var r=await cmd.ExecuteReaderAsync(); var rows=new List<JsonObject>();
        while(await r.ReadAsync()) { var row=new JsonObject(); for(int i=0;i<r.FieldCount;i++) row[r.GetName(i)] = r.IsDBNull(i)?null: r.GetDataTypeName(i) is "jsonb" or "json" ? JsonNode.Parse(r.GetString(i)) : System.Text.Json.JsonSerializer.SerializeToNode(r.GetValue(i)); rows.Add(row); }
        return rows;
    }
    public static async Task<int> Exec(NpgsqlConnection c,string sql,params object?[] args)
    { await using var cmd=new NpgsqlCommand(sql,c); foreach(var arg in args) cmd.Parameters.Add(new NpgsqlParameter{Value=arg??DBNull.Value}); return await cmd.ExecuteNonQueryAsync(); }
    public static async Task<JsonObject> One(NpgsqlConnection c,string sql,params object?[] args) => (await Query(c,sql,args)).FirstOrDefault() ?? throw new RuleException("not_found",404);
    public static Task Audit(NpgsqlConnection c,string actor,string action,string id,string reason,JsonNode? before,JsonNode? after) => Exec(c,"INSERT INTO audit_events(actor,action,entity_id,reason,before_value,after_value) VALUES($1,$2,$3,$4,$5::jsonb,$6::jsonb)",actor,action,id,reason,before?.ToJsonString(),after?.ToJsonString());
}
public sealed class RuleException(string code,int status=400):Exception(code) { public int Status {get;}=status; }
public static class J
{
    public static string S(this JsonNode n,string k,string d="")=>n[k]?.ToString()??d;
    public static int I(this JsonNode n,string k,int d=0)=>n[k]?.GetValue<int>()??d;
    public static bool B(this JsonNode n,string k,bool d=false)=>n[k]?.GetValue<bool>()??d;
    public static Guid G(this JsonNode n,string k)=>n[k] is JsonValue value && value.TryGetValue<Guid>(out var direct) ? direct : Guid.TryParse(n.S(k).Trim('"'),out var g)?g:throw new RuleException("invalid_id");
    public static JsonObject Obj(this JsonNode n,string k)=>n[k] as JsonObject??new JsonObject();
    public static string Required(this JsonNode n,string k,int max=200) { var s=n.S(k).Trim(); return s.Length>0&&s.Length<=max?s:throw new RuleException("invalid_"+k); }
}


