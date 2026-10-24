import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
test.use({trace:'off',screenshot:'off'});
test('Admin uploads a private tour image and removes it through Angular',async({page})=>{
  const seeds=process.env['AZURELAB_IDENTITY_SEEDS'];
  if(!seeds)throw new Error('Use a private Verify-Images profile with Catalog image storage enabled.');
  const user=(JSON.parse(readFileSync(seeds,'utf8')) as {users:{userName:string;password:string}[]}).users.find(x=>x.userName==='Admin')!;
  await page.goto('/login?returnUrl=/tours');
  await page.getByLabel('User name',{exact:true}).fill(user.userName);
  await page.getByLabel('Password',{exact:true}).fill(user.password);
  await page.getByRole('button',{name:'Sign in',exact:true}).click();
  const name='Browser Blob '+crypto.randomUUID();
  await page.getByLabel('Tour name',{exact:true}).fill(name);
  await page.getByRole('button',{name:'Create tour',exact:true}).click();
  await page.getByLabel('Search tours').fill(name);
  await page.getByRole('button',{name:'Search',exact:true}).click();
  await page.getByRole('link',{name,exact:true}).click();
  const panel=page.getByRole('region',{name:'Tour images'});
  await panel.getByLabel('JPEG or PNG image').setInputFiles({name:'tour.png',mimeType:'image/png',
    buffer:Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=','base64')});
  await panel.getByRole('button',{name:'Upload image',exact:true}).click();
  await expect(panel.getByRole('img',{name:'Tour photograph'})).toBeVisible();
  await expect.poll(()=>panel.getByRole('img').evaluate((img:HTMLImageElement)=>img.naturalWidth)).toBe(1);
  await page.reload();
  await expect(panel.getByRole('img')).toBeVisible();
  await panel.getByRole('button',{name:'Remove image',exact:true}).click();
  await expect(panel.getByRole('img')).toHaveCount(0);
});
