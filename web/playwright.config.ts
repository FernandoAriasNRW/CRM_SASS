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

  // El servidor de desarrollo compila cada ruta perezosa la primera vez que se pide.
  // Con varios workers entrando a la vez, esa compilación se acumula y una espera de 15 s
  // se queda corta de forma intermitente. Se amplía el margen por defecto en lugar de
  // reducir la paralelización, que multiplicaría el tiempo total de la suite.
  timeout: 60_000,
  expect: { timeout: 10_000 },

  use: {
    // Puerto propio, distinto del 4200 que publica docker-compose para la aplicación.
    // Compartirlo hacía que la suite se ejecutara contra el contenedor —una build de
    // producción, sin los cambios en curso— y fallara por código que sí era correcto.
    baseURL: 'http://localhost:4300',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],

  webServer: {
    command: 'npm start -- --port 4300',
    url: 'http://localhost:4300',

    // Nunca reutilizar lo que ya escuche en el puerto, ni siquiera en local.
    //
    // `reuseExistingServer` sólo comprueba que el puerto responda, no que responda ESTA
    // aplicación: si otro proceso lo ocupa, la suite se ejecuta contra él y falla entera
    // sin ninguna pista del motivo. Pasó con el contenedor que docker-compose publica en
    // el 4200, que sirve una build de producción: los E2E fallaban por código correcto.
    //
    // Con un puerto propio la colisión no debería ocurrir; si ocurre, esto la convierte
    // en un error explícito en lugar de en horas de depuración.
    reuseExistingServer: false,
    timeout: 180_000,
  },
});
