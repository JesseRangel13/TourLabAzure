import { expect, test } from '@playwright/test';

test('real gateway serves the shell and process status', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'TourLab Azure', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Check gateway' }).click();
  await expect(page.getByText('Alive', { exact: true })).toBeVisible();
});

test('unknown API and auth paths are not SPA success responses', async ({ request }) => {
  for (const path of ['/api/missing', '/auth/missing', '/health/missing', '/missing.js']) {
    const response = await request.get(path);
    expect(response.status(), path).toBe(404);
  }
});

test('client deep links reach the Angular shell', async ({ page }) => {
  await page.goto('/foundation');
  await expect(page.getByRole('heading', { name: 'TourLab Azure', exact: true })).toBeVisible();
});
