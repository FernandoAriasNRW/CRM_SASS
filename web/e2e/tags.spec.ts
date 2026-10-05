import { test, expect, type Page, type Request } from '@playwright/test';

/**
 * La pantalla de etiquetas: la lista, y el cajón que se abre al pulsar una fila para ver el
 * detalle, editarla o borrarla. Sin fila, el mismo cajón es el alta.
 *
 * La API va simulada, como en el resto de la suite. Lo que se cuida es que la pantalla respete lo
 * que decide el servidor —qué se puede tocar (`canManage`)— y que un rechazo se explique sin
 * perder lo escrito.
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

const TAGS = [
  {
    id: 'tag-socio', name: 'Socio', colorHex: '#14B8A6', category: 'Business', categoryLabel: 'Negocio',
    builtInKey: 'partner', createdBy: null, canManage: true,
  },
  {
    id: 'tag-portal', name: 'Portal web', colorHex: '#000000', category: 'Project', categoryLabel: 'Proyecto',
    builtInKey: null, createdBy: null, canManage: false,
  },
  {
    id: 'tag-propia', name: 'Clientes grandes', colorHex: '#3B82F6', category: 'Business', categoryLabel: 'Negocio',
    builtInKey: null, createdBy: USER.id, canManage: true,
  },
];

const CATEGORIES = [
  { id: null, name: 'Project', label: 'Proyecto', isCustom: false, isAutomatic: true },
  { id: null, name: 'Business', label: 'Negocio', isCustom: false, isAutomatic: false },
  { id: null, name: 'WorkType', label: 'Tipo de trabajo', isCustom: false, isAutomatic: false },
  { id: 'cat-vacia', name: 'Clientes', label: 'Clientes', isCustom: true, isAutomatic: false, tagCount: 0, canManage: true },
  { id: 'cat-llena', name: 'Socios', label: 'Socios', isCustom: true, isAutomatic: false, tagCount: 2, canManage: true },
];

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

type Responses = { created?: { status: number; body: unknown } };

/** Las escrituras que llegaron al servidor, para comprobar qué mandó la pantalla. */
const writes: Request[] = [];

async function signIn(page: Page, responses: Responses = {}) {
  writes.length = 0;
  await page.route(/\/api\/v1\/auth\/login/, r => r.fulfill(json(SESSION)));

  await page.route(/\/api\/v1\//, async r => {
    const url = r.request().url();
    const method = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fallback();
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USER));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USER]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));

    if (/\/tags\/categories/.test(url)) {
      if (method !== 'GET') writes.push(r.request());
      if (method === 'PUT') return r.fulfill(json({ ...CATEGORIES[3], name: 'Clientes VIP', label: 'Clientes VIP' }));
      if (method === 'DELETE') return r.fulfill({ status: 204, body: '' });
      return r.fulfill(json(CATEGORIES));
    }

    if (/\/tags/.test(url)) {
      if (method !== 'GET') writes.push(r.request());
      if (method === 'POST') {
        const created = responses.created;
        return r.fulfill(created
          ? { status: created.status, contentType: 'application/json', body: JSON.stringify(created.body) }
          : json({ ...TAGS[2], id: 'tag-nueva' }, 201));
      }
      if (method === 'PUT') return r.fulfill(json(TAGS[2]));
      if (method === 'DELETE') return r.fulfill({ status: 204, body: '' });
      return r.fulfill(json(TAGS));
    }

    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });
}

/** Por la paleta, nunca con `page.goto`: el token vive en memoria y una recarga vuelve al login. */
async function goToTags(page: Page) {
  await page.keyboard.press('Control+k');
  await page.keyboard.type('etiquetas');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tags/, { timeout: 15_000 });
  await expect(page.getByText('Clientes grandes')).toBeVisible({ timeout: 15_000 });
}

test('the list shows each tag with its category and type', async ({ page }) => {
  await signIn(page);
  await goToTags(page);

  const row = page.getByRole('row').filter({ hasText: 'Socio' });
  await expect(row).toContainText('Negocio');
  await expect(row).toContainText('Predefinida');
  await expect(page.getByRole('row').filter({ hasText: 'Portal web' })).toContainText('Automática');
  await expect(page.getByRole('row').filter({ hasText: 'Clientes grandes' })).toContainText('Propia');
});

test('clicking a row opens the drawer, and saving sends the edited tag', async ({ page }) => {
  await signIn(page);
  await goToTags(page);

  await page.getByText('Clientes grandes').click();
  const cajon = page.getByRole('dialog');
  await expect(cajon.getByRole('heading', { name: 'Clientes grandes' })).toBeVisible();
  await expect(cajon.getByText('Propia').first()).toBeVisible();

  await cajon.getByLabel('Nombre').fill('Clientes estratégicos');
  await cajon.getByTestId('tag-save').click();

  await expect(cajon).toBeHidden();
  const put = writes.find(e => e.method() === 'PUT')!;
  expect(put.url()).toContain('/tags/tag-propia');
  expect(put.postDataJSON()).toEqual({ name: 'Clientes estratégicos', colorHex: '#3B82F6', category: 'Business' });
});

test('a project tag is shown but not editable, and it explains why', async ({ page }) => {
  await signIn(page);
  await goToTags(page);

  await page.getByText('Portal web').click();
  const cajon = page.getByRole('dialog');

  await expect(cajon.getByTestId('tag-read-only')).toContainText('sigue a su equipo o proyecto');
  await expect(cajon.getByLabel('Nombre')).toBeDisabled();
  await expect(cajon.getByTestId('tag-save')).toHaveCount(0);
  await expect(cajon.getByTestId('tag-delete')).toHaveCount(0);
});

test('deleting asks for confirmation and then sends the DELETE', async ({ page }) => {
  await signIn(page);
  await goToTags(page);

  await page.getByText('Clientes grandes').click();
  const cajon = page.getByRole('dialog');

  await cajon.getByTestId('tag-delete').click();
  await expect(cajon.getByText(/¿Borrarla\?/)).toBeVisible();
  expect(writes).toHaveLength(0);

  await cajon.getByTestId('tag-confirm-delete').click();
  await expect(cajon).toBeHidden();
  expect(writes.map(e => `${e.method()} ${new URL(e.url()).pathname}`)).toEqual(['DELETE /api/v1/tags/tag-propia']);
});

test('if the server rejects the creation, the drawer stays open with what was typed and the reason', async ({ page }) => {
  await signIn(page, { created: { status: 409, body: 'Ya existe una etiqueta llamada «Socio» en esa categoría' } });
  await goToTags(page);

  await page.getByTestId('new-tag').click();
  const cajon = page.getByRole('dialog');
  await expect(cajon.getByRole('heading', { name: 'Nueva etiqueta' })).toBeVisible();

  // Las de proyecto no se ofrecen: no admiten etiquetas a mano.
  await expect(cajon.getByLabel('Categoría').locator('option', { hasText: 'Proyecto' })).toHaveCount(0);

  await cajon.getByLabel('Nombre').fill('Socio');
  await cajon.getByLabel('Categoría').selectOption('Business');
  await cajon.getByTestId('tag-save').click();

  await expect(cajon.getByTestId('tag-error')).toContainText('Ya existe una etiqueta llamada «Socio»');
  await expect(cajon.getByLabel('Nombre')).toHaveValue('Socio');
});

test('categories can be renamed, and only an empty one can be deleted', async ({ page }) => {
  await signIn(page);
  await goToTags(page);

  await page.getByTestId('open-categories').click();
  const cajon = page.getByRole('dialog');
  const empty = cajon.locator('[data-category="Clientes"]');
  const filled = cajon.locator('[data-category="Socios"]');

  // Con etiquetas dentro no se ofrece borrar: el servidor lo rechazaría.
  await expect(filled.getByRole('button', { name: 'Borrar' })).toBeDisabled();
  await expect(filled).toContainText('2 etiquetas');

  await empty.getByRole('button', { name: 'Renombrar' }).click();
  await empty.getByLabel('Nombre nuevo').fill('Clientes VIP');
  await empty.getByRole('button', { name: 'Guardar' }).click();
  await expect.poll(() => writes.map(e => e.method())).toContain('PUT');
  const put = writes.find(e => e.method() === 'PUT')!;
  expect(new URL(put.url()).pathname).toBe('/api/v1/tags/categories/cat-vacia');
  expect(put.postDataJSON()).toEqual({ name: 'Clientes VIP' });

  await empty.getByRole('button', { name: 'Borrar' }).click();
  await empty.getByRole('button', { name: 'Sí' }).click();
  await expect.poll(() => writes.map(e => `${e.method()} ${new URL(e.url()).pathname}`))
    .toContain('DELETE /api/v1/tags/categories/cat-vacia');
});
