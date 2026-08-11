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

const SESION = {
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
const ELEMENTOS = [
  { id: 'aaaaaaaa-0000-0000-0000-000000000001', name: 'Proyecto de ejemplo', title: 'Elemento de ejemplo',
    description: 'Descripción', status: 'To Do', priority: 'High', projectId: 'p1',
    assigneeId: null, estimatedHours: 4, dueDate: new Date().toISOString(),
    createdAt: new Date().toISOString(), tagIds: [] },
];

async function entrar(page: Page) {
  await page.route(/\/api\/v1\/auth\/login/, r =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(SESION) }));

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    if (/\/auth\/login/.test(url)) return r.fallback();
    // Las vistas guardadas devuelven un array, no un objeto paginado.
    if (/\/views\//.test(url)) {
      return r.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }
    return r.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ items: ELEMENTOS, totalCount: ELEMENTOS.length }),
    });
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 15_000 });
}

/**
 * Navega con la paleta de comandos. Un `page.goto` recargaría la página y perdería el
 * token, que vive en memoria y no en localStorage por decisión de seguridad.
 */
async function irA(page: Page, termino: string, urlEsperada: RegExp) {
  await page.keyboard.press('Control+k');
  await page.keyboard.type(termino);
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(urlEsperada, { timeout: 15_000 });
}

const VISTAS = [
  { nombre: 'tareas',     termino: 'tareas',     url: /\/tasks/ },
  { nombre: 'tickets',    termino: 'tickets',    url: /\/tickets/ },
  { nombre: 'proyectos',  termino: 'proyectos',  url: /\/projects/ },
  { nombre: 'panel',      termino: 'dashboard',  url: /\/dashboard/ },
  { nombre: 'calendario', termino: 'calendario', url: /\/calendar/ },
  { nombre: 'informes',   termino: 'reportes',   url: /\/reports/ },
];

for (const vista of VISTAS) {
  test(`${vista.nombre} no tiene violaciones graves de accesibilidad`, async ({ page }) => {
    await entrar(page);
    await irA(page, vista.termino, vista.url);

    const resultado = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    const graves = resultado.violations.filter(
      v => v.impact === 'critical' || v.impact === 'serious');

    // El mensaje enumera qué falla y dónde: un fallo que sólo diga «esperaba 0, hubo 3»
    // obliga a reproducirlo a mano para saber qué arreglar.
    expect(graves.map(v => `${v.id} (${v.impact}) ×${v.nodes.length}: ${v.help}`)).toEqual([]);
  });
}

test('la paleta de comandos no tiene violaciones graves', async ({ page }) => {
  await entrar(page);
  await page.keyboard.press('Control+k');
  await expect(page.getByRole('dialog', { name: 'Paleta de comandos' })).toBeVisible();

  const resultado = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  const graves = resultado.violations.filter(
    v => v.impact === 'critical' || v.impact === 'serious');

  expect(graves.map(v => `${v.id} (${v.impact}) ×${v.nodes.length}: ${v.help}`)).toEqual([]);
});
