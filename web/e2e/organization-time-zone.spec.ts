import { test, expect, type Page } from '@playwright/test';

/**
 * La zona horaria de la organización, en Administración.
 *
 * Los informes programados comparaban con la hora del servidor: «cada lunes a las 8» era a las 8
 * del servidor para todas las organizaciones. Ahora quien administra elige la zona y se guarda.
 */

const USER = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const SESSION = {
  accessToken: 'token-de-prueba',
  accessTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  refreshToken: 'refresco-de-prueba',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 864e5).toISOString(),
  user: USER,
};

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

async function openOrganization(page: Page, reject = false): Promise<string[]> {
  const saved: string[] = [];
  let current = 'UTC';

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    const method = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fulfill(json(SESSION));
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USER));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USER]));
    if (/\/users(\?|$)/.test(url)) return r.fulfill(json([USER]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/organization\/settings/.test(url)) {
      if (method === 'PUT') {
        if (reject) return r.fulfill(json('Esa zona horaria no existe.', 400));
        current = JSON.parse(r.request().postData() ?? '{}').timeZone;
        saved.push(current);
      }
      return r.fulfill(json({ timeZone: current }));
    }
    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

  // Se navega por dentro: `page.goto` recargaría y perdería el token, que vive en memoria.
  await page.getByRole('link', { name: 'Admin' }).click();
  await expect(page).toHaveURL(/\/admin/, { timeout: 15_000 });
  await page.getByRole('button', { name: 'Organización' }).click();
  await expect(page.getByLabel('Zona horaria')).toHaveValue('UTC', { timeout: 15_000 });
  return saved;
}

test('an admin picks the organization time zone and it is saved', async ({ page }) => {
  const saved = await openOrganization(page);

  await page.getByLabel('Zona horaria').selectOption('America/Bogota');
  await page.getByRole('button', { name: 'Guardar' }).click();

  await expect(page.getByRole('status')).toHaveText('Guardado.');
  expect(saved).toEqual(['America/Bogota']);
  await expect(page.getByRole('button', { name: 'Guardar' })).toBeDisabled();
});

test('if the server rejects it, it says why', async ({ page }) => {
  await openOrganization(page, true);

  await page.getByLabel('Zona horaria').selectOption('Europe/Madrid');
  await page.getByRole('button', { name: 'Guardar' }).click();

  await expect(page.getByRole('alert')).toBeVisible();
});
