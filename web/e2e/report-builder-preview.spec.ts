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
    fields: [
      { key: 'status', name: 'Estado', type: 'Text', operators: ['is'], values: ['Todo', 'Done'] },
      // Un campo personalizado de selección: con sus operadores y sus opciones como valores.
      { key: 'custom:00000000-0000-0000-0000-0000000000aa', name: 'Área', type: 'Text', operators: ['is', 'empty'], values: ['Ventas', 'Soporte'] },
    ],
    measures: [{ key: 'count', name: 'Número de tareas' }],
  }],
  operators: [
    { key: 'is', name: 'Es', types: ['Text'], needsValue: true },
    { key: 'empty', name: 'Está vacío', types: ['Text'], needsValue: false },
  ],
  visualizations: [{ key: 'table', name: 'Tabla' }, { key: 'bar', name: 'Barras' }, { key: 'stacked_bar', name: 'Barras apiladas' }, { key: 'pie', name: 'Tarta' }],
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

/**
 * Un campo personalizado sirve para agrupar —«tickets por área»— y para filtrar —«solo los del
 * área Ventas»—, con sus opciones en un desplegable en vez de texto libre.
 */
test('a custom field can be used to group and to filter, with its options', async ({ page }) => {
  await openBuilder(page);

  const groupBy = page.locator('select[name="groupBy"]');
  await expect(groupBy.locator('option', { hasText: 'Área' })).toHaveCount(1);

  // Con un filtro añadido hay dos desplegables que ofrecen «Área»: agrupar y filtrar.
  await page.getByRole('button', { name: 'Añadir filtro' }).click();
  const offering = (label: string) => page.locator('select').filter({ has: page.locator('option', { hasText: label }) });
  await expect(offering('Área')).toHaveCount(2);

  await offering('Área').last().selectOption({ label: 'Área' });
  await expect(offering('Ventas')).toHaveCount(1);
  await expect(offering('Soporte')).toHaveCount(1);
});

/**
 * Barras apiladas: se elige por qué se parte cada barra, la definición lo manda y la gráfica
 * pinta un trozo por serie, con su leyenda. Antes se pintaban como barras normales.
 */
test('stacked bars ask what to stack by and paint one series per column', async ({ page }) => {
  await openBuilder(page);

  let sent: { stackBy?: string | null } | null = null;
  await page.route(/\/reports\/preview/, r => {
    sent = JSON.parse(r.request().postData() ?? '{}').definition;
    return r.fulfill(json({
      title: 'Tareas por estado', subtitle: 'apilado por área',
      columns: ['Estado', 'Ventas', 'Soporte'], rows: [['Todo', '3', '1'], ['Done', '1', '2']], totalRows: 2,
    }));
  });

  await expect(page.getByLabel('Apilado por')).toHaveCount(0);
  await page.locator('select[name="visualization"]').selectOption('stacked_bar');
  await page.getByLabel('Apilado por').selectOption({ label: 'Área' });
  await page.getByRole('button', { name: 'Ver resultado' }).click();

  await expect.poll(() => sent?.stackBy).toBe('custom:00000000-0000-0000-0000-0000000000aa');
  const preview = page.getByTestId('builder-preview');
  // Que pinte una serie apilada por columna lo comprueba la prueba unitaria de la gráfica:
  // ECharts dibuja en un lienzo y no deja nada que leer en la página.
  await expect(preview.locator('canvas')).toBeVisible();
});
