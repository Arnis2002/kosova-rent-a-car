import {Client} from 'pg';
import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';

const base=process.env.TEST_API||'http://127.0.0.1:5081';
const password=process.env.DemoPassword||readFileSync('../../.local/runtime/demo-password.txt','utf8').trim();
const db=new Client({host:'127.0.0.1',port:Number(process.env.PGPORT||55432),database:'kosova_test',user:'kosova',password:process.env.PGPASSWORD||readFileSync('../../.local/runtime/db-password.txt','utf8').trim()});
await db.connect();
async function req(path,body,cookie=''){
 const res=await fetch(base+'/api'+path,{method:body===undefined?'GET':'POST',headers:{'Content-Type':'application/json',Cookie:cookie},body:body===undefined?undefined:JSON.stringify(body)});
 return {status:res.status,data:await res.json().catch(()=>({})),cookie:res.headers.get('set-cookie')?.split(';')[0]};
}
async function ok(path,body,cookie=''){const r=await req(path,body,cookie);assert.equal(r.status,200,JSON.stringify(r));return r.data;}
const supplier=(await req('/auth/login',{email:'supplier@demo.local',password})).cookie;
const other=(await req('/auth/login',{email:'other@demo.local',password})).cookie;
const admin=(await req('/auth/login',{email:'admin@demo.local',password})).cookie;
let vid;
try {
 const overview=await ok('/manage/overview',undefined,supplier),template=overview.vehicles[0];
 vid=(await ok('/manage/vehicles',{supplierId:overview.suppliers[0].id,name:'Availability regression '+Date.now(),spec:template.spec,rate:{...template.rate,bufferMinutes:60,prepay:false},policy:template.policy,reason:'Availability regression fixture'},supplier)).id;
 // Isolated test database fixture: document workflow is covered by integration.mjs.
 await db.query("INSERT INTO documents(id,supplier_id,vehicle_id,kind,expires_on,state,object_key,original_name,content_type) SELECT gen_random_uuid(),supplier_id,$1,kind,CURRENT_DATE+365,'approved','demo:test','Test only','application/pdf' FROM documents WHERE vehicle_id=$2 AND kind IN ('registration','insurance') LIMIT 2",[vid,template.id]);
 await ok('/manage/vehicles/'+vid,{status:'published',reason:'Test fixture approval'},admin);
 const start=new Date(Date.now()+45*86400000);start.setUTCHours(10,0,0,0);
 const end=new Date(+start+2*86400000);
 const input={pickup:'prn',dropoff:'prn',age:30,start:start.toISOString(),end:end.toISOString()};
 const visible=async()=> (await ok('/search',input)).some(v=>v.id===vid);
 assert.ok(await visible());
 const block=await ok('/manage/allocations',{vehicleId:vid,...input,kind:'external',reason:'Booked by telephone'},supplier);
 assert.equal(await visible(),false,'Manual booking must hide the car');
 let row=(await ok('/manage/overview',undefined,supplier)).allocations.find(a=>a.id===block.id);
 assert.equal(new Date(row.end).toISOString(),input.end,'Editor must show entered return time, without adding turnaround twice');
 assert.equal(new Date(row.blocked_until).getTime(),+end+3600000);
 assert.equal((await req('/manage/allocations/'+block.id,{...input,kind:'maintenance',reason:'Intrusion',expectedRevision:row.revision},other)).status,403);
 const shifted={start:new Date(+start+7*86400000).toISOString(),end:new Date(+end+7*86400000).toISOString()};
 await ok('/manage/allocations/'+block.id,{...shifted,kind:'maintenance',reason:'Workshop dates changed',expectedRevision:row.revision},supplier);
 assert.ok(await visible(),'Changing dates must release the old dates');
 assert.ok(!(await ok('/search',{...input,...shifted})).some(v=>v.id===vid));
 assert.equal((await req('/manage/allocations/'+block.id,{...input,kind:'external',reason:'Stale edit',expectedRevision:row.revision},supplier)).status,409);
 row=(await ok('/manage/overview',undefined,supplier)).allocations.find(a=>a.id===block.id);
 await ok('/manage/allocations/'+block.id,{...shifted,kind:'maintenance',reason:'Unchanged times',expectedRevision:row.revision},supplier);
 row=(await ok('/manage/overview',undefined,supplier)).allocations.find(a=>a.id===block.id);
 assert.equal(new Date(row.blocked_until).getTime(),Date.parse(shifted.end)+3600000,'Editing must not accumulate buffer time');
 const obstacle=await ok('/manage/allocations',{vehicleId:vid,...input,kind:'blackout',reason:'Separate block'},supplier);
 assert.equal((await req('/manage/allocations/'+block.id,{...input,kind:'maintenance',reason:'Conflict',expectedRevision:row.revision},supplier)).status,409);
 const afterConflict=(await ok('/manage/overview',undefined,supplier)).allocations.find(a=>a.id===block.id);
 assert.equal(afterConflict.start,row.start,'Failed edits must preserve the original block');
 assert.equal((await req('/manage/allocations/'+block.id+'/remove',{reason:'Stale removal',expectedRevision:row.revision-1},supplier)).status,409);
 await ok('/manage/allocations/'+obstacle.id+'/remove',{reason:'Test cleanup'},supplier);
 await ok('/manage/allocations/'+block.id+'/remove',{reason:'Test cleanup',expectedRevision:row.revision},supplier);
 assert.ok(await visible());
 const q=await ok('/quotes',{...input,vehicleId:vid,extras:[]});
 const b=await ok('/bookings',{quoteId:q.id,locale:'en',termsVersion:q.snapshot.termsVersion,acceptTerms:true,customer:{name:'Calendar Test',phone:'0000',email:'calendar@example.invalid',licenceYears:3}});
 await ok('/bookings/'+b.id+'/transition',{status:'confirmed',reason:'Reservation protection'},supplier);
 const reserved=(await ok('/manage/overview',undefined,supplier)).allocations.find(a=>a.booking_id===b.id);
 assert.equal((await req('/manage/allocations/'+reserved.id,{...shifted,kind:'external',reason:'Illegal reservation edit',expectedRevision:reserved.revision},supplier)).status,400);
 assert.equal((await req('/manage/allocations/'+reserved.id+'/remove',{reason:'Illegal reservation removal'},supplier)).status,400);
 await ok('/bookings/'+b.id+'/transition',{status:'cancelled',reason:'Test cleanup'},supplier);
 const audit=await ok('/manage/audit',undefined,admin);
 assert.ok(audit.some(a=>a.entity_id===block.id&&a.action==='allocation.update'));
 console.log('PASS: create, edit, release, search filtering, turnaround, stale writes, overlap rollback, supplier isolation, reservation protection and audit history');
} finally {
 if(vid){await db.query('DELETE FROM allocations WHERE vehicle_id=$1 AND booking_id IS NULL',[vid]);await ok('/manage/vehicles/'+vid,{status:'withdrawn',reason:'Finished test fixture'},admin);}
 await db.end();
}
