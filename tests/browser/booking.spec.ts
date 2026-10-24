import { expect, test, Page } from '@playwright/test';
import { readFileSync } from 'node:fs';

test.use({ trace: 'off', screenshot: 'off' });
test.setTimeout(90_000);
async function login(page: Page, name: string) {
  const file = process.env['AZURELAB_IDENTITY_SEEDS'];
  if (!file) throw new Error('Initialize the Azure lab and set AZURELAB_IDENTITY_SEEDS.');
  const users = JSON.parse(readFileSync(file, 'utf8')).users as { userName: string; password: string }[];
  const user = users.find(value => value.userName === name)!;
  await page.goto('/login?returnUrl=/tours');
  await page.getByLabel('User name', { exact: true }).fill(user.userName);
  await page.getByLabel('Password', { exact: true }).fill(user.password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tours', exact: true })).toBeVisible();
}
test('Alice books, cancels with one simulated refund and Bob cannot open her booking', async ({ page, browser }) => {
  const name = 'Synthetic browser booking ' + crypto.randomUUID();
  await login(page, 'Admin');
  await page.getByLabel('Tour name', { exact: true }).fill(name);
  await page.getByLabel('Description', { exact: true }).fill('Synthetic isolated booking evidence.');
  await page.getByRole('button', { name: 'Create tour', exact: true }).click();
  await page.getByLabel('Search tours').fill(name);
  await page.getByRole('button', { name: 'Search', exact: true }).click();
  await page.getByRole('link', { name, exact: true }).click();
  await page.getByLabel('Departure in UTC').fill(new Date(Date.now() + 3 * 86_400_000).toISOString().slice(0, 16));
  await page.getByLabel('Price in MXN cents', { exact: true }).fill('12345');
  await page.getByLabel('Capacity', { exact: true }).fill('6');
  await page.getByRole('button', { name: 'Create session', exact: true }).click();
  await expect(page.getByText('6 / 6 seats available', { exact: false })).toBeVisible();
  const tourUrl = page.url();
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await login(page, 'Alice');
  await page.goto(tourUrl);
  await page.getByLabel('Participants', { exact: true }).fill('2');
  await page.getByRole('button', { name: 'Book session', exact: true }).click();
  await expect(page).toHaveURL(/\/reservations\/[a-f0-9-]+$/);
  const bookingUrl = page.url();
  await expect(async () => {
    await page.getByRole('button', { name: 'Refresh booking', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Confirmed', exact: true })).toBeVisible();
    await expect(page.getByText('Sent · SIMULATED DELIVERY', { exact: true })).toBeVisible();
  }).toPass({ timeout: 45_000, intervals: [1000] });
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Confirmed', exact: true })).toBeVisible();
  const other = await browser.newContext({ ignoreHTTPSErrors: false });
  try {
    const bob = await other.newPage();
    await login(bob, 'Bob');
    await bob.goto(bookingUrl);
    await expect(bob.getByRole('alert').first()).toContainText(/unavailable/i);
    await expect(bob.getByRole('heading', { name: 'Confirmed', exact: true })).toHaveCount(0);
  } finally { await other.close(); }
  await page.getByRole('button', { name: 'Cancel confirmed booking', exact: true }).click();
  await expect(async () => {
    await page.getByRole('button', { name: 'Refresh booking', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Cancelled', exact: true })).toBeVisible();
  }).toPass({ timeout: 60_000, intervals: [1000] });
  const reservationId = new URL(bookingUrl).pathname.split('/').pop()!;
  await page.getByRole('link', { name: 'Payments', exact: true }).click();
  await page.getByLabel('Reservation ID filter', { exact: true }).fill(reservationId);
  await page.getByRole('button', { name: 'Filter effects', exact: true }).click();
  await page.locator('a[href^="/payments/"]').click();
  await expect(page.getByRole('heading', { name: 'Simulated refund Refunded', exact: true })).toBeVisible();
});
