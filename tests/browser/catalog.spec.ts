import { expect, test, Page } from '@playwright/test';
import { login, logout } from './support';

// Authentication snapshots and traces are never retained.
test.setTimeout(120_000);

test('Admin edits Catalog through Angular and tourist sees current availability without Admin controls', async ({ page, browser }) => {
  const name = 'Browser Catalog ' + crypto.randomUUID();
  await login(page, 'Admin');
  await page.getByLabel('Tour name', { exact: true }).fill(name);
  await page.getByLabel('Description', { exact: true }).fill('Synthetic Catalog browser evidence.');
  await page.getByRole('button', { name: 'Create tour', exact: true }).click();
  await page.getByLabel('Search tours').fill(name);
  await page.getByRole('button', { name: 'Search', exact: true }).click();
  await page.getByRole('link', { name, exact: true }).click();
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
  const departure = new Date(Date.now() + 3 * 86_400_000).toISOString().slice(0, 16);
  await page.getByLabel('Departure in UTC').fill(departure);
  await page.getByLabel('Price in MXN cents', { exact: true }).fill('12345');
  await page.getByLabel('Capacity', { exact: true }).fill('3');
  await page.getByRole('button', { name: 'Create session', exact: true }).click();
  const session = page.getByRole('region', { name: /^Session / });
  await expect(session).toContainText('3 / 3 seats available');
  await session.getByLabel('Price in MXN cents').fill('15000');
  await session.getByRole('button', { name: 'Update price', exact: true }).click();
  await expect(session).toContainText('150.00');
  await session.getByLabel('Capacity', { exact: true }).fill('4');
  await session.getByRole('button', { name: 'Update capacity', exact: true }).click();
  await expect(session).toContainText('4 / 4 seats available');
  await page.reload();
  await expect(session).toContainText('4 / 4 seats available');
  const detailUrl = page.url();
  const other = await browser.newContext({baseURL:process.env['AZURELAB_BASE_URL'] ?? 'https://localhost:9443',ignoreHTTPSErrors: false });
  try {
    const tourist = await other.newPage();
    await login(tourist, 'Alice');
    await tourist.goto(detailUrl);
    await expect(tourist.getByRole('heading', { name, exact: true })).toBeVisible();
    await expect(tourist.getByText('4 / 4 seats available', { exact: false })).toBeVisible();
    await expect(tourist.getByRole('button', { name: 'Save tour', exact: true })).toHaveCount(0);
    await logout(tourist);
    await tourist.goto(detailUrl);
    await expect(tourist.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible();
  } finally { await other.close(); }
  await page.getByLabel('Active tour', { exact: false }).uncheck();
  await page.getByRole('button', { name: 'Save tour', exact: true }).click();
  await expect(page.getByText('Inactive', { exact: true })).toBeVisible();
});
