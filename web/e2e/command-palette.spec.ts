import { test, expect, type Page } from '@playwright/test';

/**
 * Paleta de comandos. Vive detrás del inicio de sesión, así que cada prueba entra con la
 * API simulada: lo que se verifica es el comportamiento del paletón, no la autenticación,
 * que ya cubren los tests de integración.
 */

const SESION = {
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

async function entrar(page: Page) {
  await page.route('**/api/v1/auth/login', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(SESION) }));
  await page.route('**/api/v1/**', route =>
    route.request().url().includes('/auth/login')
      ? route.fallback()
      : route.fulfill({ status: 200, contentType: 'application/json', body: '{"items":[],"totalCount":0}' }));

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });
}

const paleta = (page: Page) => page.getByRole('dialog', { name: 'Paleta de comandos' });

test('Ctrl+K abre la paleta con el foco puesto en el buscador', async ({ page }) => {
  await entrar(page);

  await page.keyboard.press('Control+k');

  await expect(paleta(page)).toBeVisible();
  // Sin foco automático habría que hacer clic para escribir, que es justo lo que la
  // paleta existe para evitar.
  await expect(page.getByRole('combobox')).toBeFocused();
});

test('Escape la cierra', async ({ page }) => {
  await entrar(page);
  await page.keyboard.press('Control+k');
  await expect(paleta(page)).toBeVisible();

  await page.keyboard.press('Escape');

  await expect(paleta(page)).toBeHidden();
});

test('escribir filtra y Enter navega a la sección elegida', async ({ page }) => {
  await entrar(page);
  await page.keyboard.press('Control+k');

  await page.keyboard.type('tickets');
  await page.keyboard.press('Enter');

  await expect(page).toHaveURL(/\/tickets/);
  await expect(paleta(page)).toBeHidden();
});

test('las flechas recorren la lista y marcan una sola opción', async ({ page }) => {
  await entrar(page);
  await page.keyboard.press('Control+k');

  await page.keyboard.press('ArrowDown');

  const seleccionadas = page.locator('[role="option"][aria-selected="true"]');
  await expect(seleccionadas).toHaveCount(1);

  // El foco no se mueve a la opción: sigue en el campo para poder escribir, y es
  // aria-activedescendant quien le dice al lector de pantalla cuál está resaltada.
  await expect(page.getByRole('combobox')).toBeFocused();
  const activo = await page.getByRole('combobox').getAttribute('aria-activedescendant');
  expect(activo).toBeTruthy();
});

test('avisa cuando nada coincide, en lugar de quedarse vacía', async ({ page }) => {
  await entrar(page);
  await page.keyboard.press('Control+k');

  await page.keyboard.type('xyzzy-no-existe');

  await expect(page.getByText(/nada coincide/i)).toBeVisible();
});
