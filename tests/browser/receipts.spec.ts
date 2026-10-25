import { expect, test, Page } from '@playwright/test';
import { login, logout } from './support';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
test.setTimeout(120_000);

test('historical simulated confirmation downloads after the actual Function and survives cancellation',async({page,browser})=>{
  const name='Browser confirmation '+crypto.randomUUID();
  await login(page,'Admin');
  await page.getByLabel('Tour name',{exact:true}).fill(name);
  await page.getByRole('button',{name:'Create tour',exact:true}).click();
  await page.getByLabel('Search tours').fill(name);await page.getByRole('button',{name:'Search',exact:true}).click();
  await page.getByRole('link',{name,exact:true}).click();
  await page.getByLabel('Departure in UTC').fill(new Date(Date.now()+3*86_400_000).toISOString().slice(0,16));
  await page.getByLabel('Price in MXN cents',{exact:true}).fill('12345');
  await page.getByLabel('Capacity',{exact:true}).fill('6');
  await page.getByRole('button',{name:'Create session',exact:true}).click();
  await expect(page.getByText('6 / 6 seats available',{exact:false})).toBeVisible();
  const tour=page.url();await logout(page);
  await login(page,'Alice');await page.goto(tour);
  const trace=crypto.randomUUID().replaceAll('-','');
  await page.setExtraHTTPHeaders({traceparent:'00-'+trace+'-0123456789abcdef-01'});
  await page.getByLabel('Participants',{exact:true}).fill('2');
  await page.getByRole('button',{name:'Book session',exact:true}).click();
  await expect(page).toHaveURL(/\/reservations\/[a-f0-9-]+$/);
  const reservationUrl=page.url();
  await expect(async()=>{
    await page.getByRole('button',{name:'Refresh booking',exact:true}).click();
    await expect(page.getByRole('heading',{name:'Confirmed',exact:true})).toBeVisible();
    await expect(page.getByRole('button',{name:'Download simulated confirmation',exact:true})).toBeVisible();
  }).toPass({timeout:45_000,intervals:[1000]});
  const pending=page.waitForEvent('download');
  await page.getByRole('button',{name:'Download simulated confirmation',exact:true}).click();
  const download=await pending;expect(download.suggestedFilename()).toMatch(/^simulated-confirmation-.*\.txt$/);
  const historical=readFileSync((await download.path())!);
  expect(historical.toString()).toContain('SIMULATED EDUCATIONAL BOOKING CONFIRMATION');
  expect(historical.toString()).toContain('Accepted amount: 24690 MXN cents');
  const reservation=new URL(reservationUrl).pathname.split('/').pop()!;
  const status=await page.request.get('/api/notifications/v1/receipts/reservation/'+reservation);
  expect(status.status()).toBe(200);
  const receipt=(await status.json() as {id:string}[])[0];expect(receipt).toBeDefined();
  const report=process.env['AZURELAB_BROWSER_REPORT'];
  if(!report)throw new Error('Full-stack evidence directory is required.');
  mkdirSync(report,{recursive:true});
  writeFileSync(resolve(report,'receipt-trace-id.txt'),trace);
  await page.getByRole('button',{name:'Cancel confirmed booking',exact:true}).click();
  await expect(async()=>{
    await page.getByRole('button',{name:'Refresh booking',exact:true}).click();
    await expect(page.getByRole('heading',{name:'Cancelled',exact:true})).toBeVisible();
  }).toPass({timeout:45_000,intervals:[1000]});
  await expect(page.getByRole('button',{name:'Download simulated confirmation',exact:true})).toBeVisible();
  const after=page.waitForEvent('download');
  await page.getByRole('button',{name:'Download simulated confirmation',exact:true}).click();
  expect(readFileSync((await (await after).path())!)).toEqual(historical);
  const other=await browser.newContext({baseURL:process.env['AZURELAB_BASE_URL'] ?? 'https://localhost:9443',ignoreHTTPSErrors:false});
  try{
    const bob=await other.newPage();await login(bob,'Bob');await bob.goto(reservationUrl);
    expect((await bob.request.get('/api/notifications/v1/receipts/'+receipt.id+'/content')).status()).toBe(404);
    await expect(bob.getByRole('button',{name:'Download simulated confirmation',exact:true})).toHaveCount(0);
  }finally{await other.close();}
});
