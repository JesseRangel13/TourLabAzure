import { expect, test, Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
test.use({trace:'off',screenshot:'off'});
test.setTimeout(120_000);
async function login(page:Page,name:string){
  const path=process.env['AZURELAB_IDENTITY_SEEDS'];
  if(!path)throw new Error('Use a dashboard-enabled private SQL/broker/Cosmos profile.');
  const user=(JSON.parse(readFileSync(path,'utf8')).users as {userName:string;password:string}[]).find(u=>u.userName===name)!;
  await page.goto('/login?returnUrl=/tours');
  await page.getByLabel('User name',{exact:true}).fill(user.userName);
  await page.getByLabel('Password',{exact:true}).fill(user.password);
  await page.getByRole('button',{name:'Sign in',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Tours',exact:true})).toBeVisible();
}
test('Cosmos dashboard shows the immutable booking and keeps authoritative actions separate',async({page,browser})=>{
  const name='Browser Cosmos '+crypto.randomUUID();
  await login(page,'Admin');
  await page.getByLabel('Tour name',{exact:true}).fill(name);
  await page.getByRole('button',{name:'Create tour',exact:true}).click();
  await page.getByLabel('Search tours').fill(name);
  await page.getByRole('button',{name:'Search',exact:true}).click();
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
  const reservation=new URL(page.url()).pathname.split('/').pop()!;
  await page.goto('/dashboard');
  await expect(page.getByText(/Eventually consistent/)).toBeVisible();
  await expect(async()=>{
    await page.getByRole('button',{name:'First page / Refresh',exact:true}).click();
    await expect(page.getByRole('link',{name,exact:true})).toBeVisible();
  }).toPass({timeout:45_000,intervals:[1000]});
  await page.getByRole('link',{name,exact:true}).click();
  await expect(page).toHaveURL('/dashboard/'+reservation);
  await expect(page.getByRole('link',{name:'Authoritative details and actions',exact:true})).toHaveAttribute('href','/reservations/'+reservation);
  await expect(page.getByRole('button',{name:'Cancel confirmed booking',exact:true})).toHaveCount(0);
  await page.reload();await expect(page.getByRole('heading',{name,exact:true})).toBeVisible();
  const other=await browser.newContext({ignoreHTTPSErrors:false});
  try{
    const bob=await other.newPage();await login(bob,'Bob');await bob.goto('/dashboard/'+reservation);
    await expect(bob.getByRole('alert')).toContainText(/not available/);
    await expect(bob.getByRole('heading',{name,exact:true})).toHaveCount(0);
  }finally{await other.close();}
});
