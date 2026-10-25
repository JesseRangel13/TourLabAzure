import { expect, test, Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
test.use({trace:'off',screenshot:'off'});
test.setTimeout(120_000);
async function login(page:Page,name:string){
  const path=process.env['AZURELAB_IDENTITY_SEEDS'];
  if(!path)throw new Error('Use the private receipt SQL/broker/Azurite/Functions profile.');
  const user=(JSON.parse(readFileSync(path,'utf8')).users as {userName:string;password:string}[]).find(u=>u.userName===name)!;
  await page.goto('/login?returnUrl=/tours');
  await page.getByLabel('User name',{exact:true}).fill(user.userName);
  await page.getByLabel('Password',{exact:true}).fill(user.password);
  await page.getByRole('button',{name:'Sign in',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Tours',exact:true})).toBeVisible();
}
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
  const tour=page.url();await page.getByRole('button',{name:'Sign out',exact:true}).click();
  await login(page,'Alice');await page.goto(tour);
  await page.getByLabel('Participants',{exact:true}).fill('2');
  await page.getByRole('button',{name:'Book session',exact:true}).click();
  await expect(page).toHaveURL(/\/reservations\/[a-f0-9-]+$/);
  const reservationUrl=page.url();
  await expect(async()=>{
    await page.getByRole('button',{name:'Refresh booking',exact:true}).click();
    await expect(page.getByRole('button',{name:'Download simulated confirmation',exact:true})).toBeVisible();
  }).toPass({timeout:45_000,intervals:[1000]});
  const pending=page.waitForEvent('download');
  await page.getByRole('button',{name:'Download simulated confirmation',exact:true}).click();
  const download=await pending;expect(download.suggestedFilename()).toMatch(/^simulated-confirmation-.*\.txt$/);
  await page.getByRole('button',{name:'Cancel confirmed booking',exact:true}).click();
  await expect(async()=>{
    await page.getByRole('button',{name:'Refresh booking',exact:true}).click();
    await expect(page.getByRole('heading',{name:'Cancelled',exact:true})).toBeVisible();
  }).toPass({timeout:45_000,intervals:[1000]});
  await expect(page.getByRole('button',{name:'Download simulated confirmation',exact:true})).toBeVisible();
  const other=await browser.newContext({ignoreHTTPSErrors:false});
  try{
    const bob=await other.newPage();await login(bob,'Bob');await bob.goto(reservationUrl);
    await expect(bob.getByRole('button',{name:'Download simulated confirmation',exact:true})).toHaveCount(0);
  }finally{await other.close();}
});