import { defineConfig, devices } from '@playwright/test';

// When PLAYWRIGHT_BASE_URL is set we test that running app (e.g. a deployed env)
// and skip the local build/preview server. Otherwise build+preview locally.
const externalBaseURL = process.env.PLAYWRIGHT_BASE_URL;
const baseURL = externalBaseURL ?? 'http://localhost:4173';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL,
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'setup', testMatch: /auth\.setup\.ts/ },
    {
      name: 'mobile-chromium',
      dependencies: ['setup'],
      use: {
        ...devices['Pixel 7'],
        storageState: 'e2e/.auth/state.json',
      },
    },
  ],
  webServer: externalBaseURL
    ? undefined
    : {
        command: 'pnpm build && pnpm preview --port 4173 --strictPort',
        url: 'http://localhost:4173',
        reuseExistingServer: !process.env.CI,
        timeout: 180_000,
      },
});
