import { test, expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/**
 * Auditoría de accesibilidad de las vistas que exigen sesión.
 *
 * Hasta ahora sólo se auditaban login y el alta pública de tickets, que es la parte más
 * pequeña del producto: el resto —donde se pasa el día quien lo usa— quedaba sin cubrir
 * porque hacía falta sesión y datos. Con la API simulada y la navegación por la paleta
 * ya no hace falta ninguna de las dos cosas.
 *
 * axe detecta del orden de la mitad de los problemas reales. Sirve como red contra
 * regresiones, no como certificado de conformidad.
 */

const SESSION = {
  accessToken: 'token-de-prueba',
  refreshToken: 'refresco-de-prueba',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 864e5).toISOString(),
  user: {
    id: '00000000-0000-0000-0000-000000000001',
    name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
    tenantId: '00000000-0000-0000-0000-0000000000ff',
  },
};

/** Datos mínimos para que las vistas pinten contenido y no sólo estados vacíos. */
const ELEMENTS = [
  { id: 'aaaaaaaa-0000-0000-0000-000000000001', name: 'Proyecto de ejemplo', title: 'Elemento de ejemplo',
    description: 'Descripción', status: 'To Do', priority: 'High', projectId: 'p1',
    assigneeId: null, estimatedHours: 4, dueDate: new Date().toISOString(),
    createdAt: new Date().toISOString(), tagIds: [] },
];

async function signIn(page: Page) {
  await page.route(/\/api\/v1\/auth\/login/, r =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(SESSION) }));

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    if (/\/auth\/login/.test(url)) return r.fallback();
    // Las vistas guardadas devuelven un array, no un objeto paginado.
    if (/\/views\//.test(url)) {
      return r.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }
    return r.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ items: ELEMENTS, totalCount: ELEMENTS.length }),
    });
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });
}

/**
 * Navega con la paleta de comandos. Un `page.goto` recargaría la página y perdería el
 * token, que vive en memoria y no en localStorage por decisión de seguridad.
 */
async function goTo(page: Page, term: string, expectedUrl: RegExp) {
  await page.keyboard.press('Control+k');
  await page.keyboard.type(term);
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(expectedUrl, { timeout: 15_000 });
}

const VIEWS = [
  { name: 'tareas',     term: 'tareas',     url: /\/tasks/ },
  { name: 'tickets',    term: 'tickets',    url: /\/tickets/ },
  { name: 'proyectos',  term: 'proyectos',  url: /\/projects/ },
  { name: 'panel',      term: 'dashboard',  url: /\/dashboard/ },
  { name: 'calendario', term: 'calendario', url: /\/calendar/ },
  { name: 'informes',   term: 'informes',   url: /\/reports/ },
];

for (const view of VIEWS) {
  test(`${view.name} has no serious accessibility violations`, async ({ page }) => {
    await signIn(page);
    await goTo(page, view.term, view.url);

    const result = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    const graves = result.violations.filter(
      v => v.impact === 'critical' || v.impact === 'serious');

    // El mensaje enumera qué falla y dónde: un fallo que sólo diga «esperaba 0, hubo 3»
    // obliga a reproducirlo a mano para saber qué arreglar.
    expect(graves.map(v => `${v.id} (${v.impact}) ×${v.nodes.length}: ${v.help}`)).toEqual([]);
  });
}

test('the command palette has no serious violations', async ({ page }) => {
  await signIn(page);
  await page.keyboard.press('Control+k');
  await expect(page.getByRole('dialog', { name: 'Paleta de comandos' })).toBeVisible();

  const result = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  const graves = result.violations.filter(
    v => v.impact === 'critical' || v.impact === 'serious');

  expect(graves.map(v => `${v.id} (${v.impact}) ×${v.nodes.length}: ${v.help}`)).toEqual([]);
});
