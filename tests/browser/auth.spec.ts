import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';

test.use({ trace: 'off', screenshot: 'off' });
test('Alice logs in, refreshes her account, then signs out', async ({ page }) => {
  const path = process.env['AZURELAB_IDENTITY_SEEDS'];
  test.skip(!path, 'Set AZURELAB_IDENTITY_SEEDS to this Azure lab private provisioner config.');
  const config = JSON.parse(readFileSync(path!, 'utf8')) as { users: { userName: string; password: string }[] };
  const alice = config.users.find(user => user.userName === 'Alice')!;
  await page.goto('/account');
  await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible();
  await page.getByLabel('User name', { exact: true }).fill(alice.userName);
  await page.getByLabel('Password', { exact: true }).fill(alice.password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'My account' })).toBeVisible();
  await expect(page.getByText('Alice', { exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading', { name: 'My account' })).toBeVisible();
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible();
  await page.goto('/account');
  await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible();
});
