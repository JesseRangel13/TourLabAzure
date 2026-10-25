import { expect, test } from '@playwright/test';
import { login, logout } from './support';

test('Alice logs in, refreshes her account, then signs out', async ({page}) => {
  await login(page, 'Alice');
  const paths=['/health/ready',...['catalog','reservations','payments','notifications'].map(service=>'/api/'+service+'/health/ready')];
  for(const path of paths){
    const ready=await page.request.get(path);
    expect(ready.status(),path).toBe(200);
    expect((await ready.json()).status,path).toBe('Ready');
  }
  await page.goto('/account');
  await expect(page.getByRole('heading', {name:'My account'})).toBeVisible();
  await expect(page.getByText('Alice', {exact:true})).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading', {name:'My account'})).toBeVisible();
  await logout(page);
  expect((await page.request.get('/health/ready')).status()).toBe(401);
  for(const port of [9444,9445,9446,9447]){
    expect((await page.request.get('https://localhost:'+port+'/health/ready')).status()).toBe(401);
  }
  await page.goto('/account');
  await expect(page.getByRole('heading', {name:'Sign in',exact:true})).toBeVisible();
});
