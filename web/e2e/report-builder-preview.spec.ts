import { test, expect, type Page } from '@playwright/test';
import { createSession, submitLogin } from './support/auth';

/**
 * La vista previa del constructor de informes se pinta con la forma elegida.
 *
 * Era siempre una tabla, aunque se eligieran barras o tarta, mientras el panel ya pintaba
 * gráficas con la misma definición: quien construía el informe no veía lo que iba a ver en su
 * recuadro. Ahora la vista previa usa la misma gráfica que el panel.
 */

const USER = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const SESSION = createSession(USER);

const REPORT = { id: 'r1', tenantId: USER.tenantId, createdById: USER.id, name: 'Tareas por estado', type: 'Custom', format: 'Pdf', parameters: null, tagIds: [] };

const CATALOG = {
  dataSources: [{
    key: 'Tasks', name: 'Tareas',
    fields: [{ key: 'status', name: 'Estado', type: 'Text', operators: ['is'], values: ['Todo', 'Done'] }],
    measures: [{ key: 'count', name: 'Número de tareas' }],
  }],
  operators: [{ key: 'is', name: 'Es', types: ['Text'], needsValue: true }],
  visualizations: [{ key: 'table', name: 'Tabla' }, { key: 'bar', name: 'Barras' }, { key: 'pie', name: 'Tarta' }],
  granularities: [],
};

const PREVIEW = {
  title: 'Tareas por estado', subtitle: 'Tareas, agrupado por estado',
  columns: ['Estado', 'Número de tareas'], rows: [['Todo', '4'], ['Done', '2']], totalRows: 2,
};

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

async function openBuilder(page: Page) {
  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();

    if (/\/auth\/login/.test(url)) return r.fulfill(json(SESSION));
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USER));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USER]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/tags/.test(url)) return r.fulfill(json([]));
    if (/\/reports\/catalog/.test(url)) return r.fulfill(json(CATALOG));
    if (/\/reports\/r1\/definition/.test(url)) return r.fulfill(json({ title: 'No tiene' }, 404));
    if (/\/reports\/preview/.test(url)) return r.fulfill(json(PREVIEW));
    if (/\/reports(\?|$)/.test(url)) return r.fulfill(json({ items: [REPORT], totalCount: 1 }));

    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await submitLogin(page);

  // Se navega por dentro: `page.goto` recargaría y perdería el token, que vive en memoria.
  await page.keyboard.press('Control+k');
  await page.keyboard.type('informes');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/reports/);

  await page.getByRole('button', { name: 'Construir' }).first().click();
  await expect(page.locator('select[name="visualization"]')).toBeVisible({ timeout: 15_000 });
}

test('choosing bars previews a chart, not a table', async ({ page }) => {
  await openBuilder(page);

  await page.locator('select[name="visualization"]').selectOption('bar');
  await page.getByRole('button', { name: 'Ver resultado' }).click();

  const preview = page.getByTestId('builder-preview');
  await expect(preview.locator('canvas')).toBeVisible();
  await expect(preview.locator('table')).toHaveCount(0);
});

test('choosing table previews the rows as a table', async ({ page }) => {
  await openBuilder(page);

  await page.locator('select[name="visualization"]').selectOption('table');
  await page.getByRole('button', { name: 'Ver resultado' }).click();

  const preview = page.getByTestId('builder-preview');
  await expect(preview.locator('table')).toBeVisible();
  await expect(preview.getByRole('cell', { name: 'Todo' })).toBeVisible();
});
