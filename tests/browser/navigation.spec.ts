import { expect, test } from '@playwright/test';
import { login, logout } from './support';

test.setTimeout(120_000);
test('concurrent Alice and Bob contexts keep identity and secure cookies isolated across refresh and back', async ({page,browser}) => {
  await login(page,'Alice');
  const other=await browser.newContext({baseURL:process.env['AZURELAB_BASE_URL'] ?? 'https://localhost:9443',ignoreHTTPSErrors:false});
  try {
    const bob=await other.newPage(); await login(bob,'Bob');
    await page.goto('/account'); await bob.goto('/account');
    await expect(page.getByText('Alice',{exact:true})).toBeVisible();
    await expect(bob.getByText('Bob',{exact:true})).toBeVisible();
    const auth=(await page.context().cookies()).find(cookie=>cookie.httpOnly && cookie.name === '__Host-tourlab-azure');
    expect(auth).toBeDefined(); expect(auth!.secure).toBe(true); expect(auth!.sameSite).toBe('Strict');
    for(const current of [page,bob]) {
      await current.goto('/reservations');
      await current.goto('/payments'); await current.goBack();
      await expect(current).toHaveURL(/\/reservations$/); await current.reload();
      await expect(current.getByRole('heading',{name:'My bookings',exact:true})).toBeVisible();
      const storage=await current.evaluate(()=>({local:Object.keys(localStorage),session:Object.keys(sessionStorage)}));
      expect(storage.local).toEqual([]); expect(storage.session).toEqual([]);
    }
    await logout(page); await login(page,'Bob'); await page.goto('/account');
    await expect(page.getByText('Bob',{exact:true})).toBeVisible();
    await expect(page.getByText('Alice',{exact:true})).toHaveCount(0);
    await bob.reload(); await expect(bob.getByRole('heading',{name:'My bookings',exact:true})).toBeVisible();
    await bob.goto('/account'); await expect(bob.getByText('Bob',{exact:true})).toBeVisible();
  }finally{await other.close();}
});

test('lost session resets NGXS private state on real API 401 and authenticated mutations require CSRF', async ({page}) => {
  await login(page,'Alice'); await page.goto('/reservations');
  await expect(page.getByRole('heading',{name:'My bookings',exact:true})).toBeVisible();
  const response=await page.request.post('/auth/logout',{data:{},headers:{Origin:'https://localhost:9443'}});
  expect(response.status()).toBe(400);
  expect((await page.request.get('/auth/me')).status()).toBe(200);
  // Removing the real cookie models expiry; no API response interception or auth bypass.
  await page.context().clearCookies();
  await page.getByRole('button',{name:'Refresh bookings',exact:true}).click();
  await expect(page.getByRole('link',{name:'My account',exact:true})).toHaveCount(0);
  await page.goto('/account');
  await expect(page.getByRole('heading',{name:'Sign in',exact:true})).toBeVisible();
  await expect(page.getByText('Alice',{exact:true})).toHaveCount(0);
});
