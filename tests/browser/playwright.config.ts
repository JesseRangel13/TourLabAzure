import { defineConfig } from '@playwright/test';
import { resolve } from 'node:path';

const report = process.env['AZURELAB_BROWSER_REPORT'] ?? resolve('../../.local/browser-report');
export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.ts',
  fullyParallel: false,
  workers: 1, // Shared lab fault controls and the unchanged ten-credentials/minute limiter.
  forbidOnly: true,
  maxFailures: 1,
  retries: 0,
  timeout: 90_000,
  expect: { timeout: 10_000 },
  outputDir: resolve(report, 'private-results'),
  reporter: [['list'], ['html', { outputFolder: resolve(report, 'html'), open: 'never' }],
    ['junit', { outputFile: resolve(report, 'junit.xml') }]],
  use: {
    baseURL: process.env['AZURELAB_BASE_URL'] ?? 'https://localhost:9443',
    ignoreHTTPSErrors: false,
    actionTimeout: 15_000,
    // Authenticated DOM/network captures can contain passwords, cookies and private data.
    // Keep bounded assertion reports locally; never upload auth traces or storage state.
    trace: 'off', screenshot: 'off', video: 'off'
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }]
});
