import { defineConfig, devices } from '@playwright/test';

/**
 * Los E2E arrancan el servidor de Angular por su cuenta (`webServer`), de modo que
 * `npx playwright test` funciona igual en local que en CI sin pasos previos.
 *
 * No levantan el backend: eso exigiría MySQL y dejaría la suite dependiendo de datos
 * sembrados. Los flujos que necesitan API interceptan las peticiones con `page.route`,
 * lo que además los hace deterministas. La cobertura del backend real ya la dan los
 * tests de integración contra Testcontainers.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  workers: process.env['CI'] ? 1 : undefined,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : [['list']],

  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],

  webServer: {
    command: 'npm start',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env['CI'],
    timeout: 180_000,
  },
});
