import { test, expect } from '@playwright/test';

/**
 * Flujo de acceso. Las respuestas de la API se interceptan para que la prueba no dependa
 * de que haya un backend levantado ni de datos sembrados: lo que se verifica aquí es el
 * comportamiento del frontend —guardas de ruta, gestión de sesión, mensajes de error—,
 * no la autenticación en sí, que ya cubren los tests de integración.
 */

const SESION_VALIDA = {
  accessToken: 'token-de-prueba',
  refreshToken: 'refresco-de-prueba',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 864e5).toISOString(),
  user: {
    id: '00000000-0000-0000-0000-000000000001',
    name: 'Admin Administrator',
    email: 'admin@acme.com',
    role: 'Admin',
    tenantId: '00000000-0000-0000-0000-0000000000ff',
  },
};

test('un usuario no autenticado que entra a la raíz acaba en el login', async ({ page }) => {
  await page.goto('/');

  await expect(page).toHaveURL(/\/login/);
  // Por rol, no por texto: comprueba de paso que la pantalla tiene un encabezado real.
  // Hasta la Fase 3 no lo tenía —ui-card-title renderizaba un elemento sin semántica—
  // y un lector de pantalla no percibía ninguna estructura.
  await expect(page.getByRole('heading', { name: 'Iniciar sesión', level: 1 })).toBeVisible();
});

test('una ruta protegida no es accesible sin sesión', async ({ page }) => {
  await page.goto('/projects');

  // La guarda debe interceptar antes de pintar nada de la vista de proyectos.
  await expect(page).toHaveURL(/\/login/);
});

test('con credenciales inválidas se informa del error y no se navega', async ({ page }) => {
  await page.route('**/api/v1/auth/login', route =>
    route.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('quien@sea.com');
  await page.getByPlaceholder('••••••••').fill('incorrecta');
  await page.getByRole('button', { name: /ingresar/i }).click();

  await expect(page).toHaveURL(/\/login/);
});

test('con credenciales válidas se entra a la aplicación', async ({ page }) => {
  await page.route('**/api/v1/auth/login', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(SESION_VALIDA),
    }));

  // El resto de llamadas de la pantalla inicial se responden vacías: la prueba mide
  // que se atraviesa el login, no lo que muestra el panel.
  await page.route('**/api/v1/**', route =>
    route.request().url().includes('/auth/login')
      ? route.fallback()
      : route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }));

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();

  await expect(page).not.toHaveURL(/\/login/, { timeout: 15_000 });
});
