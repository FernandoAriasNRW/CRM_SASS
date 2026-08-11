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

    // Nunca reutilizar lo que ya escuche en el puerto, ni siquiera en local.
    //
    // `reuseExistingServer` sólo comprueba que el puerto responda, no que responda la
    // aplicación: si otro proceso lo ocupa, la suite se ejecuta contra él y falla entera
    // sin ninguna pista del motivo. Ocurrió con un contenedor de los tests de
    // integración, que publica su puerto al azar y se quedó con el 4200.
    //
    // Cuesta unos segundos por ejecución y ahorra depurar fallos que no son del código.
    reuseExistingServer: false,
    timeout: 180_000,
  },
});
