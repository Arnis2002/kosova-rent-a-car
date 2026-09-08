using System.Text.Json.Nodes;
namespace Kosova;
public sealed class OperationsWorker(Store db,RuntimeOptions options,NotificationDelivery delivery,ILogger<OperationsWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested) {
            try { await Tick(); } catch(Exception e) { logger.LogWarning("Operational job failed: {Type}",e.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(5),stoppingToken);
        }
    }
    public async Task Tick()
    {
        await using(var c=await db.Open()) {await using var tx=await c.BeginTransactionAsync();
            var expired=await Store.Query(c,"SELECT * FROM bookings WHERE (status='awaiting_supplier' AND response_due<=now()) OR (status='awaiting_payment' AND payment_due<=now()) FOR UPDATE SKIP LOCKED");
            foreach(var b in expired) { await Store.Exec(c,"DELETE FROM allocations WHERE booking_id=$1",b.G("id")); await Store.Exec(c,"UPDATE bookings SET status='expired' WHERE id=$1",b.G("id")); await BookingService.Notify(c,b,"expired","system","Response or payment deadline elapsed"); await Store.Audit(c,"system","booking.expire",b.S("id"),"Deadline elapsed",new JsonObject{["status"]=b.S("status")},new JsonObject{["status"]="expired"}); }
            await Store.Exec(c,"DELETE FROM sessions WHERE expires_at<=now()");
            await Store.Exec(c,"UPDATE bookings SET customer='{}',access_secret=null WHERE access_expires_at<now() AND access_secret IS NOT NULL AND status IN ('completed','cancelled','declined','expired','no_show') AND refund_state NOT IN ('requested','pending')");
            await tx.CommitAsync();
        }
        for(int i=0;i<20;i++) {
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();var jobs=await Store.Query(c,"SELECT * FROM notification_jobs WHERE state='pending' AND available_at<=now() ORDER BY available_at FOR UPDATE SKIP LOCKED LIMIT 1");if(jobs.Count==0)break;var job=jobs[0];
            try {var b=await Store.One(c,"SELECT * FROM bookings WHERE id=$1",job.G("booking_id"));await delivery.Send(job,b);await Store.Exec(c,"UPDATE notification_jobs SET state=$2,attempts=attempts+1,delivered_at=now() WHERE id=$1",job.G("id"),options.Demo?"development_mailbox":"delivered");}
            catch(Exception e){int attempt=job.I("attempts")+1;await Store.Exec(c,"UPDATE notification_jobs SET attempts=$2,state=$3,available_at=now()+make_interval(secs=>$4) WHERE id=$1",job.G("id"),attempt,attempt>=8?"failed":"pending",Math.Min(3600,(int)Math.Pow(2,attempt)*15));logger.LogWarning("Notification {Job} attempt {Attempt} failed: {Type}",job.S("id"),attempt,e.GetType().Name);}
            await tx.CommitAsync();
        }
    }
}
