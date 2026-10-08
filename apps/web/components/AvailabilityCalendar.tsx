'use client';
import {useEffect,useRef,useState} from 'react';
import {ArrowLeft,ArrowRight,CalendarBlank,Plus,ArrowClockwise} from '@phosphor-icons/react';
import {api,Data} from '@/lib/api';
import {availabilityText,kosovoLocal,occupiesDay,shiftDay} from '@/lib/availability';
import {date} from '@/lib/i18n';
import {Field,Status,useText} from './Marketplace';

type Editor={id?:string;revision?:number;vehicleId:string;start:string;end:string;kind:string;reason:string;remove?:boolean};
type Allocation=Data & {start:string;end:string;blocked_until:string};
export default function AvailabilityCalendar({vehicles,allocations,initialVehicle='',refresh,openBooking}:{vehicles:Data[];allocations:Allocation[];initialVehicle?:string;refresh:()=>Promise<void>;openBooking:(id:string)=>void}){
 const {locale}=useText(),copy=availabilityText[locale];
 const today=kosovoLocal(new Date()).slice(0,10);
 const [first,setFirst]=useState(today),[vehicleId,setVehicleId]=useState(initialVehicle);
 const [editor,setEditor]=useState<Editor|null>(null),[busy,setBusy]=useState(false),[error,setError]=useState(''),[saved,setSaved]=useState(false);
 const editorRef=useRef<HTMLDivElement>(null),periodRef=useRef<HTMLDivElement>(null);
 const days=Array.from({length:14},(_,i)=>shiftDay(first,i));
 const visibleCars=vehicles.filter(v=>!vehicleId||v.id===vehicleId);
 const periods=allocations.filter(a=>(!vehicleId||a.vehicle_id===vehicleId)&&days.some(day=>occupiesDay(a,day))).sort((a,b)=>Date.parse(a.start)-Date.parse(b.start));
 const label=(kind:string)=>copy[kind as 'booking'|'hold'|'external'|'maintenance'|'blackout']||kind;
 const dayLabel=(day:string)=>new Intl.DateTimeFormat(locale==='sq'?'sq-AL':locale,{day:'numeric',month:'short',timeZone:'UTC'}).format(new Date(day+'T12:00:00Z'));
 useEffect(()=>{if(editor){editorRef.current?.scrollIntoView({block:'nearest'});editorRef.current?.focus({preventScroll:true});}},[editor?.id,editor?.vehicleId,editor?.remove]);
 function begin(carId=vehicleId||vehicles[0]?.id,day=first){if(!carId)return;setError('');setSaved(false);setEditor({vehicleId:carId,start:day+'T10:00',end:shiftDay(day,1)+'T10:00',kind:'external',reason:''});}
 function edit(a:Data,remove=false){setError('');setSaved(false);setEditor({id:a.id,revision:a.revision,vehicleId:a.vehicle_id,start:kosovoLocal(a.start),end:kosovoLocal(a.end),kind:a.kind,reason:remove?'':a.note,remove});}
 function message(code:string){return code==='availability_changed'?copy.stale:code==='conflict'?copy.conflict:code==='invalid_interval'?copy.invalid:code==='ambiguous_datetime'?copy.timeError:copy.error;}
 async function reload(){setBusy(true);setError('');try{await refresh();}catch{setError(copy.error);}finally{setBusy(false);}}
 async function submit(event:React.FormEvent){
  event.preventDefault();if(!editor||busy)return;
  setBusy(true);setError('');setSaved(false);
  try{
   if(editor.remove)await api('/manage/allocations/'+editor.id+'/remove',{reason:editor.reason,expectedRevision:editor.revision});
   else{
    if(editor.end<=editor.start)throw new Error('invalid_interval');
    const times=await api('/time',{start:editor.start,end:editor.end});
    await api('/manage/allocations'+(editor.id?'/'+editor.id:''),{vehicleId:editor.vehicleId,...times,kind:editor.kind,reason:editor.reason,expectedRevision:editor.revision});
   }
   setEditor(null);setSaved(true);await refresh();
  }catch(e){setError(message((e as Error).message));}finally{setBusy(false);}
 }
 const change=(key:keyof Editor,value:string)=>setEditor(old=>old?{...old,[key]:value}:null);
 return <div className="availability-workspace">
  <div className="availability-heading"><div><h2>{copy.title}</h2><p>{copy.intro}</p></div><div className="button-row"><button className="button secondary" onClick={reload} disabled={busy}><ArrowClockwise size={18}/>{copy.refresh}</button><button className="button" onClick={()=>begin()} disabled={busy||!vehicles.length}><Plus size={18}/>{copy.newBlock}</button></div></div>
  {saved&&<div role="status" className="notice">{copy.success}</div>}
  {error&&<div role="alert" className="notice error">{error}</div>}
  <section className="panel availability-board" aria-label={copy.title}>
   <div className="availability-toolbar"><Field label={copy.find}><select value={vehicleId} onChange={e=>{setVehicleId(e.target.value);setEditor(null);}}><option value="">{copy.allCars}</option>{vehicles.map(v=><option value={v.id} key={v.id}>{v.name}</option>)}</select></Field><Field label={copy.from}><input type="date" required value={first} onChange={e=>{if(/^\d{4}-\d{2}-\d{2}$/.test(e.target.value))setFirst(e.target.value);}}/></Field><div className="button-row"><button className="button secondary" aria-label={copy.previous} onClick={()=>setFirst(shiftDay(first,-14))}><ArrowLeft size={18}/></button><button className="button secondary" onClick={()=>setFirst(today)}>{copy.today}</button><button className="button secondary" aria-label={copy.next} onClick={()=>setFirst(shiftDay(first,14))}><ArrowRight size={18}/></button></div></div>
   <div className="availability-legend">{(['open','booking','hold','external','maintenance','blackout'] as const).map(k=><span key={k}><i aria-hidden="true" className={'calendar-swatch calendar-'+k}/>{copy[k]}</span>)}</div>
   <div className="calendar-scroll" tabIndex={0} role="region" aria-label={copy.days}>
    <table className="fleet-calendar"><caption>{dayLabel(first)} – {dayLabel(days[13])} · {copy.timezone}</caption><thead><tr><th scope="col">{copy.selectCar}</th>{days.map(day=><th scope="col" key={day} className={day===today?'calendar-today':''}><span>{new Intl.DateTimeFormat(locale==='sq'?'sq-AL':locale,{weekday:'short',timeZone:'UTC'}).format(new Date(day+'T12:00:00Z'))}</span>{dayLabel(day)}</th>)}</tr></thead><tbody>{visibleCars.map(v=><tr key={v.id}><th scope="row"><span>{v.name}</span><Status value={v.status}/></th>{days.map(day=>{
     const entries=allocations.filter(a=>a.vehicle_id===v.id&&occupiesDay(a,day)),entry=entries[0];
     return <td key={day}><button type="button" className={'calendar-day calendar-'+(entry?.kind||'open')} aria-label={`${v.name}, ${dayLabel(day)}: ${entries.length?entries.map(a=>label(a.kind)).join(', '):copy.open}`} onClick={()=>{setVehicleId(v.id);if(!entry)begin(v.id,day);else if(entries.length===1&&!entry.booking_id)edit(entry);else{setEditor(null);periodRef.current?.focus();periodRef.current?.scrollIntoView({block:'nearest'});}}}>{entry?<><span>{label(entry.kind)}</span>{entries.length>1&&<small>+{entries.length-1}</small>}</>:<span aria-hidden="true">—</span>}</button></td>;
    })}</tr>)}</tbody></table>
   </div>
   {!visibleCars.length&&<p className="calendar-empty">{vehicles.length?copy.noCars:copy.addFirst}</p>}
   <p className="calendar-hint">{copy.hint}</p>
  </section>
  {editor&&<div className="panel availability-editor" tabIndex={-1} ref={editorRef}><form className="form-stack" onSubmit={submit}>
   <div className="row"><h2>{editor.remove?copy.removeTitle:editor.id?copy.editBlock:copy.newBlock}</h2><button className="link-button" type="button" disabled={busy} onClick={()=>setEditor(null)}>{copy.cancel}</button></div>
   {editor.remove?<><p>{copy.removeHint}</p><strong>{vehicles.find(v=>v.id===editor.vehicleId)?.name}</strong><p>{editor.start.replace('T',' ')} → {editor.end.replace('T',' ')}</p></>:<div className="two-columns">
    <Field label={copy.selectCar}><select required disabled={busy||!!editor.id} value={editor.vehicleId} onChange={e=>change('vehicleId',e.target.value)}>{vehicles.map(v=><option value={v.id} key={v.id}>{v.name}</option>)}</select></Field>
    <Field label={copy.kind}><select disabled={busy} value={editor.kind} onChange={e=>change('kind',e.target.value)}>{['external','maintenance','blackout'].map(k=><option value={k} key={k}>{label(k)}</option>)}</select></Field>
    <Field label={copy.start}><input type="datetime-local" required disabled={busy} value={editor.start} onChange={e=>change('start',e.target.value)}/></Field>
    <Field label={copy.end}><input type="datetime-local" required disabled={busy} value={editor.end} onChange={e=>change('end',e.target.value)}/></Field>
   </div>}
   <small>{copy.timezone}</small><Field label={copy.note}><textarea required maxLength={1000} disabled={busy} value={editor.reason} onChange={e=>change('reason',e.target.value)}/></Field>
   <button className={'button'+(editor.remove?' danger':'')} disabled={busy}>{busy?copy.saving:editor.remove?copy.confirmRemove:copy.save}</button>
  </form></div>}
  <section className="panel availability-periods" tabIndex={-1} ref={periodRef} aria-label={copy.periods}><h2>{copy.periods}</h2>{!periods.length?<p>{copy.empty}</p>:periods.map(a=><article className="availability-period" key={a.id}><div><div className="row"><h3>{vehicles.find(v=>v.id===a.vehicle_id)?.name}</h3><span className={'availability-kind calendar-'+a.kind}>{label(a.kind)}</span></div><p>{date(a.start,locale)} → {date(a.end,locale)}</p>{Date.parse(a.blocked_until)>Date.parse(a.end)&&<small>{copy.buffer}: {date(a.blocked_until,locale)}</small>}{a.note&&<p>{a.note}</p>}</div><div className="availability-period-actions">{a.booking_id?<><p>{copy.locked}</p><button className="button secondary" onClick={()=>openBooking(a.booking_id)}><CalendarBlank size={17}/>{label('booking')}</button></>:<><button className="button secondary" disabled={busy} onClick={()=>edit(a)}>{copy.edit}</button><button className="link-button" disabled={busy} onClick={()=>edit(a,true)}>{copy.remove}</button></>}</div></article>)}</section>
 </div>;
}
