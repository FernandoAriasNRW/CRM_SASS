import { test, expect, type Page } from '@playwright/test';

/**
 * Paleta de comandos. Vive detrás del inicio de sesión, así que cada prueba entra con la
 * API simulada: lo que se verifica es el comportamiento del paletón, no la autenticación,
 * que ya cubren los tests de integración.
 */

const SESSION = {
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

async function signIn(page: Page) {
  await page.route('**/api/v1/auth/login', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(SESSION) }));
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

const palette = (page: Page) => page.getByRole('dialog', { name: 'Paleta de comandos' });

test('Ctrl+K opens the palette with focus on the search box', async ({ page }) => {
  await signIn(page);

  await page.keyboard.press('Control+k');

  await expect(palette(page)).toBeVisible();
  // Sin foco automático habría que hacer clic para escribir, que es justo lo que la
  // paleta existe para evitar.
  await expect(page.getByRole('combobox')).toBeFocused();
});

test('Escape closes it', async ({ page }) => {
  await signIn(page);
  await page.keyboard.press('Control+k');
  await expect(palette(page)).toBeVisible();

  await page.keyboard.press('Escape');

  await expect(palette(page)).toBeHidden();
});

test('typing filters and Enter navigates to the chosen section', async ({ page }) => {
  await signIn(page);
  await page.keyboard.press('Control+k');

  await page.keyboard.type('tickets');
  await page.keyboard.press('Enter');

  await expect(page).toHaveURL(/\/tickets/);
  await expect(palette(page)).toBeHidden();
});

test('the arrows move through the list and highlight one option', async ({ page }) => {
  await signIn(page);
  await page.keyboard.press('Control+k');

  await page.keyboard.press('ArrowDown');

  const selected = page.locator('[role="option"][aria-selected="true"]');
  await expect(selected).toHaveCount(1);

  // El foco no se mueve a la opción: sigue en el campo para poder escribir, y es
  // aria-activedescendant quien le dice al lector de pantalla cuál está resaltada.
  await expect(page.getByRole('combobox')).toBeFocused();
  const active = await page.getByRole('combobox').getAttribute('aria-activedescendant');
  expect(active).toBeTruthy();
});

test('it says when nothing matches instead of staying empty', async ({ page }) => {
  await signIn(page);
  await page.keyboard.press('Control+k');

  await page.keyboard.type('xyzzy-no-existe');

  await expect(page.getByText(/nada coincide/i)).toBeVisible();
});

// Antes ninguna de las tres pantallas leía el parámetro y el comando sólo llevaba a la lista.
const CREATE_COMMANDS = [
  { command: 'Nueva tarea', form: 'Nueva Tarea', url: /\/tasks$/ },
  { command: 'Nuevo proyecto', form: 'Nuevo Proyecto', url: /\/projects$/ },
  { command: 'Nuevo ticket', form: 'Nuevo Ticket', url: /\/tickets$/ },
];

for (const { command, form, url } of CREATE_COMMANDS) {
  test(`«${command}» opens the creation form and leaves the URL clean`, async ({ page }) => {
    await signIn(page);
    await page.keyboard.press('Control+k');

    await page.keyboard.type(command.toLowerCase());
    await page.keyboard.press('Enter');

    await expect(page.getByRole('dialog', { name: form })).toBeVisible();
    // Sin el parámetro en la URL, recargar o volver atrás no reabre el formulario. Recargar aquí
    // no sirve para comprobarlo: con la API simulada la sesión no sobrevive a la recarga.
    await expect(page).toHaveURL(url);
  });
}
