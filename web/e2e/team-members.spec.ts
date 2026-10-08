import { test, expect, type Page } from '@playwright/test';

/**
 * Editar los miembros de un equipo en administración.
 *
 * Guardar manda la lista entera y la API la aplica tal cual. La edición abría sin nadie marcado,
 * porque la API no decía quiénes eran los miembros: guardar sólo el nombre habría vaciado el
 * equipo. Aquí se comprueba en el navegador que la edición parte de los miembros que hay y que
 * lo que se guarda es lo que se ve marcado.
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

const ANA = { id: 'u-ana', name: 'Ana Pérez', email: 'ana@acme.com' };
const LUIS = { id: 'u-luis', name: 'Luis Gómez', email: 'luis@acme.com' };
const MARTA = { id: 'u-marta', name: 'Marta Ruiz', email: 'marta@acme.com' };

const TEAM = {
  id: 't1', name: 'Soporte', description: 'Primer nivel', memberCount: 2, memberIds: [ANA.id, LUIS.id],
};

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

async function openTeams(page: Page): Promise<Record<string, unknown>[]> {
  const saved: Record<string, unknown>[] = [];

  await page.route(/\/api\/v1\/auth\/login/, r => r.fulfill(json(SESSION)));

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    const method = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fallback();
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USER));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USER]));
    if (/\/users(\?|$)/.test(url)) return r.fulfill(json([ANA, LUIS, MARTA]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/custom-fields/.test(url)) return r.fulfill(json([]));

    if (/\/teams\/t1$/.test(url) && method === 'PUT') {
      saved.push(JSON.parse(r.request().postData() ?? '{}'));
      return r.fulfill({ status: 204, body: '' });
    }
    if (/\/teams(\?|$)/.test(url)) return r.fulfill(json([TEAM]));

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
  await page.getByRole('button', { name: 'Equipos & Grupos' }).click();

  return saved;
}

const memberBox = (page: Page, name: string) =>
  page.locator('label', { hasText: name }).getByRole('checkbox');

test('editing a team starts from the members it has', async ({ page }) => {
  await openTeams(page);

  await page.getByTitle('Editar Equipo').click();

  await expect(memberBox(page, 'Ana Pérez')).toBeChecked();
  await expect(memberBox(page, 'Luis Gómez')).toBeChecked();
  await expect(memberBox(page, 'Marta Ruiz')).not.toBeChecked();
});

test('what is saved is what is checked', async ({ page }) => {
  const saved = await openTeams(page);

  await page.getByTitle('Editar Equipo').click();
  await page.locator('label', { hasText: 'Ana Pérez' }).click();
  await page.locator('label', { hasText: 'Marta Ruiz' }).click();
  await page.getByRole('button', { name: 'Guardar Cambios' }).click();

  await expect.poll(() => saved.length).toBe(1);
  expect(saved[0]['memberIds']).toEqual([LUIS.id, MARTA.id]);
});
