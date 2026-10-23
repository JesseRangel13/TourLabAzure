import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.ts',
  fullyParallel: true,
  retries: 0,
  timeout: 20_000,
  use: {
    baseURL: process.env['AZURELAB_BASE_URL'] ?? 'https://localhost:9443',
    // Browser must trust the lab CA. Never suppress certificate validation.
    ignoreHTTPSErrors: false,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }]
});
