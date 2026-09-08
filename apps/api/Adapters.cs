using Amazon.S3;
using Amazon.S3.Model;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.AspNetCore.DataProtection;
using System.Text;
using System.Text.Json.Nodes;
using System.Net.Sockets;
using System.Buffers.Binary;
namespace Kosova;
public interface IPrivateObjects { Task Put(string key,byte[] bytes,string contentType); Task<byte[]> Get(string key); }
public sealed class LocalPrivateObjects(RuntimeOptions options):IPrivateObjects
{
    public Task Put(string key,byte[] bytes,string contentType){Directory.CreateDirectory(options.PrivateStorage);return File.WriteAllBytesAsync(Path.Combine(options.PrivateStorage,key),bytes);}
    public Task<byte[]> Get(string key)=>File.ReadAllBytesAsync(Path.Combine(options.PrivateStorage,key));
}
public sealed class S3PrivateObjects(IConfiguration config):IPrivateObjects,IDisposable
{
    readonly AmazonS3Client client=new(new AmazonS3Config{RegionEndpoint=Amazon.RegionEndpoint.GetBySystemName(config["Storage:Region"])});
    readonly string bucket=config["Storage:Bucket"]!;
    public async Task Put(string key,byte[] bytes,string contentType){using var stream=new MemoryStream(bytes);await client.PutObjectAsync(new PutObjectRequest{BucketName=bucket,Key="quarantine/"+key,InputStream=stream,ContentType=contentType,ServerSideEncryptionMethod=ServerSideEncryptionMethod.AES256});}
    public async Task<byte[]> Get(string key){using var response=await client.GetObjectAsync(bucket,"quarantine/"+key);using var stream=new MemoryStream();await response.ResponseStream.CopyToAsync(stream);return stream.ToArray();}
    public void Dispose()=>client.Dispose();
}
public sealed class UploadScanner(IConfiguration configuration,RuntimeOptions options)
{
    public async Task<string> Scan(byte[] bytes)
    {
        if(options.Demo)return "demo_unscanned";
        using var client=new TcpClient();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));await client.ConnectAsync(configuration["Scanner:Host"]!,configuration.GetValue("Scanner:Port",3310),timeout.Token);using var stream=client.GetStream();await stream.WriteAsync("zINSTREAM\0"u8.ToArray(),timeout.Token);
        for(int offset=0;offset<bytes.Length;offset+=65536){int count=Math.Min(65536,bytes.Length-offset);var length=new byte[4];BinaryPrimitives.WriteInt32BigEndian(length,count);await stream.WriteAsync(length,timeout.Token);await stream.WriteAsync(bytes.AsMemory(offset,count),timeout.Token);}await stream.WriteAsync(new byte[4],timeout.Token);
        var buffer=new byte[1024];int read=await stream.ReadAsync(buffer,timeout.Token);var result=Encoding.UTF8.GetString(buffer,0,read);return result.Contains("stream: OK")?"clean":result.Contains("FOUND")?"infected":"error";
    }
}
public sealed class GuestSecrets(IDataProtectionProvider provider) { readonly IDataProtector protector=provider.CreateProtector("Kosova.GuestAccess.v1");public string Protect(string token)=>protector.Protect(token);public string Unprotect(string ciphertext)=>protector.Unprotect(ciphertext); }
public sealed class NotificationDelivery(IConfiguration configuration,GuestSecrets secrets,RuntimeOptions options)
{
    static readonly Dictionary<string,string[]> Statuses=new(){["awaiting_supplier"]=["Request received. Awaiting supplier confirmation.","Kërkesa u pranua. Në pritje të konfirmimit të furnizuesit.","Anfrage eingegangen. Bestätigung des Vermieters steht aus."],["awaiting_payment"]=["Supplier accepted. Payment is required before confirmation.","Furnizuesi pranoi. Pagesa kërkohet para konfirmimit.","Vermieter hat angenommen. Zahlung ist vor Bestätigung erforderlich."],["confirmed"]=["Your reservation is confirmed.","Rezervimi juaj është konfirmuar.","Ihre Reservierung ist bestätigt."],["declined"]=["The supplier declined your request.","Furnizuesi refuzoi kërkesën tuaj.","Der Vermieter hat Ihre Anfrage abgelehnt."],["expired"]=["Your request or payment window expired.","Afati i kërkesës ose pagesës ka skaduar.","Ihre Anfrage oder Zahlungsfrist ist abgelaufen."],["cancelled"]=["Your reservation was cancelled.","Rezervimi juaj u anulua.","Ihre Reservierung wurde storniert."],["active"]=["Your rental has started.","Qiraja juaj ka filluar.","Ihre Miete hat begonnen."],["completed"]=["Your rental is complete.","Qiraja juaj ka përfunduar.","Ihre Miete ist abgeschlossen."],["no_show"]=["Your reservation was marked as no-show.","Rezervimi u shënua si mosparaqitje.","Ihre Reservierung wurde als nicht erschienen markiert."],["refunded"]=["Your refund was recorded.","Rimbursimi juaj u regjistrua.","Ihre Erstattung wurde erfasst."],["replacement_proposed"]=["A replacement was proposed. Your acceptance is required.","U propozua një zëvendësim. Kërkohet pranimi juaj.","Ein Ersatzfahrzeug wurde vorgeschlagen. Ihre Zustimmung ist erforderlich."]};
    public static string Text(string status,string locale)=>Statuses.TryGetValue(status,out var text)?text[locale=="sq"?1:locale=="de"?2:0]:throw new RuleException("invalid_notification_status");
    public async Task Send(JsonObject job,JsonObject booking)
    {
        var locale=job.S("locale");var text=Text(job.S("status"),locale);
        if(options.Demo)return;
        var link=configuration["WebOrigin"]+"/"+locale+"/booking/"+booking.S("id")+"#"+secrets.Unprotect(booking.S("access_secret"));
        var message=new MimeMessage();message.From.Add(MailboxAddress.Parse(configuration["Email:From"]!));message.To.Add(MailboxAddress.Parse(booking.Obj("customer").S("email")));message.Subject="Kosova Rent-A-Car · "+text;message.MessageId=job.S("id")+"@"+new System.Net.Mail.MailAddress(configuration["Email:From"]!).Host;
        message.Body=new TextPart("plain"){Text=text+"\n\n"+link+"\n\n"+(locale=="sq"?"Lidhje private. Mos e ndani.":locale=="de"?"Privater Link. Nicht weitergeben.":"Private link. Do not share.")};
        using var client=new SmtpClient();client.Timeout=15000;await client.ConnectAsync(configuration["Email:Host"]!,configuration.GetValue("Email:Port",587),SecureSocketOptions.StartTls);await client.AuthenticateAsync(configuration["Email:Username"]!,configuration["Email:Password"]!);await client.SendAsync(message);await client.DisconnectAsync(true);
    }
}
