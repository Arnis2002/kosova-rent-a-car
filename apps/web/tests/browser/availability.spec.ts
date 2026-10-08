import {test,expect} from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import {readFileSync} from 'node:fs';
import {kosovoLocal,shiftDay} from '../../lib/availability';

const password=process.env.DemoPassword||readFileSync('../../.local/runtime/demo-password.txt','utf8').trim();
test('a company adds a car and opens its own availability calendar',async({page})=>{
 await page.request.post('/api/auth/login',{data:{email:'supplier@demo.local',password}});
 await page.goto('/en/dashboard');await page.getByRole('button',{name:'Add vehicle',exact:true}).click();
 const name='New fleet car '+Date.now();
 await page.getByLabel('Vehicle',{exact:true}).fill(name);
 await page.getByLabel('Deposit collection method',{exact:true}).fill('Card at pickup');
 await page.getByLabel('Driver & documents',{exact:true}).fill('Valid driving licence and identification');
 await page.getByLabel('Insurance & exclusions',{exact:true}).fill('Demonstration supplier policy');
 await page.getByLabel('Cancellation',{exact:true}).fill('Contact rental company before pickup');
 await page.getByLabel('Reason',{exact:true}).fill('New fleet listing');
 await page.getByRole('button',{name:'Save changes',exact:true}).click();
 await page.getByLabel('Search your fleet',{exact:true}).fill(name);
 const card=page.locator('.fleet-grid article').filter({hasText:name});
 await expect(card.getByText('Draft',{exact:true})).toBeVisible();
 await card.getByRole('button',{name:'Manage availability',exact:true}).click();
 await expect(page.getByRole('heading',{name:'Fleet calendar',exact:true})).toBeVisible();
 const added=await page.getByRole('combobox',{name:'Find a vehicle',exact:true}).getByRole('option',{name,exact:true}).getAttribute('value');
 await expect(page.getByRole('combobox',{name:'Find a vehicle',exact:true})).toHaveValue(added!);
});
test('supplier calendar updates public results, edits dates and releases a car',async({page,browser})=>{
 await page.goto('/en/login');
 await page.getByLabel('Email address',{exact:true}).fill('supplier@demo.local');
 await page.getByLabel('Password',{exact:true}).fill(password);
 await page.getByRole('button',{name:'Sign in',exact:true}).click();
 await page.getByRole('button',{name:'Availability',exact:true}).click();
 await expect(page.getByRole('heading',{name:'Fleet calendar',exact:true})).toBeVisible();
 const day=shiftDay(kosovoLocal(new Date()).slice(0,10),70),end=shiftDay(day,2);
 const local={start:day+'T10:00',end:end+'T10:00'};
 const times=await (await page.request.post('/api/time',{data:local})).json();
 const search={...times,pickup:'prn',dropoff:'prn',age:30};
 const vehicles=await (await page.request.post('/api/search',{data:search})).json();
 const car=vehicles.find((vehicle:any)=>vehicles.filter((candidate:any)=>candidate.name===vehicle.name).length===1);expect(car).toBeTruthy();
 const guest=await browser.newContext();const publicPage=await guest.newPage();
 const url=new URL('/en/search',page.url());url.search=new URLSearchParams({...local,pickup:'prn',dropoff:'prn',age:'30'}).toString();
 await publicPage.goto(url.toString());
 const result=publicPage.getByRole('heading',{name:car.name,exact:true});await expect(result).toBeVisible();
 const note='Calendar browser '+Date.now();
 try {
  await page.getByLabel('Calendar starts',{exact:true}).fill(day);
  await page.getByRole('combobox',{name:'Find a vehicle',exact:true}).selectOption(car.id);
  await page.getByRole('button',{name:'Block dates',exact:true}).click();
  await page.getByLabel('Unavailable from',{exact:true}).fill(local.start);
  await page.getByLabel('Return / end time',{exact:true}).fill(local.end);
  await page.getByLabel('Note / reason',{exact:true}).fill(note);
  await page.getByRole('button',{name:'Save availability',exact:true}).click();
  await expect(page.getByText(note,{exact:true})).toBeVisible();
  await publicPage.evaluate(()=>window.dispatchEvent(new Event('focus')));
  await expect(result).toHaveCount(0);
  const period=page.locator('.availability-period').filter({hasText:note});
  await period.getByRole('button',{name:'Edit dates'}).click();
  await expect(page.getByLabel('Return / end time',{exact:true})).toHaveValue(local.end);
  await page.getByLabel('Unavailable from',{exact:true}).fill(shiftDay(day,5)+'T10:00');
  await page.getByLabel('Return / end time',{exact:true}).fill(shiftDay(end,5)+'T10:00');
  await page.getByRole('button',{name:'Save availability',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Edit unavailable period',exact:true})).toHaveCount(0);
  await publicPage.evaluate(()=>window.dispatchEvent(new Event('focus')));await expect(result).toBeVisible();
  await page.setViewportSize({width:390,height:844});
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBeTruthy();
  const a11y=await new AxeBuilder({page}).withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();expect(a11y.violations).toEqual([]);
  await page.screenshot({path:'../../artifacts/availability-mobile.png',fullPage:true});
  await page.setViewportSize({width:1440,height:1000});
  await page.screenshot({path:'../../artifacts/availability-desktop.png',fullPage:true});
  await period.getByRole('button',{name:'Release these dates'}).click();
  await page.getByLabel('Note / reason',{exact:true}).fill('External booking cancelled');
  await page.getByRole('button',{name:'Confirm release',exact:true}).click();
  await expect(page.getByText(note,{exact:true})).toHaveCount(0);
 }finally{
  const overview=await (await page.request.get('/api/manage/overview')).json();
  for(const a of overview.allocations.filter((a:any)=>a.vehicle_id===car.id&&a.note===note))await page.request.post('/api/manage/allocations/'+a.id+'/remove',{data:{reason:'Browser test cleanup'}});
  await guest.close();
 }
});

for(const locale of ['sq','de'])test(`${locale}: company calendar is localized and usable on mobile`,async({page})=>{
 const login=await page.request.post('/api/auth/login',{data:{email:'supplier@demo.local',password}});expect(login.ok()).toBeTruthy();
 await page.setViewportSize({width:390,height:844});await page.goto('/'+locale+'/dashboard');
 await page.getByRole('button',{name:'Availability',exact:true}).click();
 await expect(page.getByRole('heading',{name:locale==='sq'?'Kalendari i flotës':'Fuhrparkkalender',exact:true})).toBeVisible();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBeTruthy();
 const a11y=await new AxeBuilder({page}).withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();expect(a11y.violations).toEqual([]);
});
