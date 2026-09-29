import { test, expect, type Page, type Request } from '@playwright/test';

/**
 * La pantalla de etiquetas: la lista, y el cajón que se abre al pulsar una fila para ver el
 * detalle, editarla o borrarla. Sin fila, el mismo cajón es el alta.
 *
 * La API va simulada, como en el resto de la suite. Lo que se cuida es que la pantalla respete lo
 * que decide el servidor —qué se puede tocar (`canManage`)— y que un rechazo se explique sin
 * perder lo escrito.
 */

const USUARIO = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const SESION = {
  accessToken: 'token-de-prueba',
  accessTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  refreshToken: 'refresco-de-prueba',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 864e5).toISOString(),
  user: USUARIO,
};

const ETIQUETAS = [
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
    builtInKey: null, createdBy: USUARIO.id, canManage: true,
  },
];

const CATEGORIAS = [
  { id: null, name: 'Project', label: 'Proyecto', isCustom: false, isAutomatic: true },
  { id: null, name: 'Business', label: 'Negocio', isCustom: false, isAutomatic: false },
  { id: null, name: 'WorkType', label: 'Tipo de trabajo', isCustom: false, isAutomatic: false },
];

const json = (cuerpo: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(cuerpo),
});

type Respuestas = { alta?: { status: number; cuerpo: unknown } };

/** Las escrituras que llegaron al servidor, para comprobar qué mandó la pantalla. */
const escrituras: Request[] = [];

async function entrar(page: Page, respuestas: Respuestas = {}) {
  escrituras.length = 0;
  await page.route(/\/api\/v1\/auth\/login/, r => r.fulfill(json(SESION)));

  await page.route(/\/api\/v1\//, async r => {
    const url = r.request().url();
    const metodo = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fallback();
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USUARIO));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USUARIO]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));

    if (/\/tags\/categories/.test(url)) return r.fulfill(json(CATEGORIAS));

    if (/\/tags/.test(url)) {
      if (metodo !== 'GET') escrituras.push(r.request());
      if (metodo === 'POST') {
        const alta = respuestas.alta;
        return r.fulfill(alta
          ? { status: alta.status, contentType: 'application/json', body: JSON.stringify(alta.cuerpo) }
          : json({ ...ETIQUETAS[2], id: 'tag-nueva' }, 201));
      }
      if (metodo === 'PUT') return r.fulfill(json(ETIQUETAS[2]));
      if (metodo === 'DELETE') return r.fulfill({ status: 204, body: '' });
      return r.fulfill(json(ETIQUETAS));
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
async function irALasEtiquetas(page: Page) {
  await page.keyboard.press('Control+k');
  await page.keyboard.type('etiquetas');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tags/, { timeout: 15_000 });
  await expect(page.getByText('Clientes grandes')).toBeVisible({ timeout: 15_000 });
}

test('la lista enseña cada etiqueta con su categoría y su tipo', async ({ page }) => {
  await entrar(page);
  await irALasEtiquetas(page);

  const fila = page.getByRole('row').filter({ hasText: 'Socio' });
  await expect(fila).toContainText('Negocio');
  await expect(fila).toContainText('Predefinida');
  await expect(page.getByRole('row').filter({ hasText: 'Portal web' })).toContainText('Automática');
  await expect(page.getByRole('row').filter({ hasText: 'Clientes grandes' })).toContainText('Propia');
});

test('pulsar una fila abre el cajón, y guardar manda la etiqueta editada', async ({ page }) => {
  await entrar(page);
  await irALasEtiquetas(page);

  await page.getByText('Clientes grandes').click();
  const cajon = page.getByRole('dialog');
  await expect(cajon.getByRole('heading', { name: 'Clientes grandes' })).toBeVisible();
  await expect(cajon.getByText('Propia').first()).toBeVisible();

  await cajon.getByLabel('Nombre').fill('Clientes estratégicos');
  await cajon.getByTestId('tag-save').click();

  await expect(cajon).toBeHidden();
  const put = escrituras.find(e => e.method() === 'PUT')!;
  expect(put.url()).toContain('/tags/tag-propia');
  expect(put.postDataJSON()).toEqual({ name: 'Clientes estratégicos', colorHex: '#3B82F6', category: 'Business' });
});

test('una etiqueta de proyecto se ve pero no se edita, y se explica por qué', async ({ page }) => {
  await entrar(page);
  await irALasEtiquetas(page);

  await page.getByText('Portal web').click();
  const cajon = page.getByRole('dialog');

  await expect(cajon.getByTestId('tag-read-only')).toContainText('sigue a su equipo o proyecto');
  await expect(cajon.getByLabel('Nombre')).toBeDisabled();
  await expect(cajon.getByTestId('tag-save')).toHaveCount(0);
  await expect(cajon.getByTestId('tag-delete')).toHaveCount(0);
});

test('borrar pide confirmación y después manda el DELETE', async ({ page }) => {
  await entrar(page);
  await irALasEtiquetas(page);

  await page.getByText('Clientes grandes').click();
  const cajon = page.getByRole('dialog');

  await cajon.getByTestId('tag-delete').click();
  await expect(cajon.getByText(/¿Borrarla\?/)).toBeVisible();
  expect(escrituras).toHaveLength(0);

  await cajon.getByTestId('tag-confirm-delete').click();
  await expect(cajon).toBeHidden();
  expect(escrituras.map(e => `${e.method()} ${new URL(e.url()).pathname}`)).toEqual(['DELETE /api/v1/tags/tag-propia']);
});

test('si el servidor rechaza el alta, el cajón sigue abierto con lo escrito y el motivo', async ({ page }) => {
  await entrar(page, { alta: { status: 409, cuerpo: 'Ya existe una etiqueta llamada «Socio» en esa categoría' } });
  await irALasEtiquetas(page);

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
