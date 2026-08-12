import { test, expect, type Page } from '@playwright/test';

/**
 * Humo de la sección de documentos.
 *
 * Es la feature más grande del frontend —705 líneas de plantilla y 625 de componente— y
 * no tenía ninguna cobertura.
 *
 * Cubre lo que se puede comprobar sin conocer la forma exacta de su API: que la vista se
 * construye sin errores y que cumple accesibilidad. Los flujos con datos —listar
 * documentos, importar— quedan sin cubrir: no conseguí reproducir la respuesta que espera
 * el componente, y una prueba que adivina la forma de los datos da verde sin comprobar
 * nada. Es el primer trabajo pendiente si se va a partir este componente en piezas.
 */

const SESION = {
  accessToken: 't', refreshToken: 'r',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  user: { id: '1', name: 'Admin', email: 'admin@acme.com', role: 'Admin', tenantId: 'ff' },
};

const DOCUMENTOS = [
  { id: 'd1', title: 'Manual de arquitectura', description: 'Cómo está montado el sistema',
    type: 'Doc', isStarred: false, updatedAt: new Date().toISOString(), pages: [] },
];

async function entrarADocs(page: Page) {
  await page.route(/\/api\/v1\/auth\/login/, r =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(SESION) }));

  await page.route(/\/api\/v1\//, r => {
    const u = r.request().url();
    if (/\/auth\/login/.test(u)) return r.fallback();
    if (/\/views\//.test(u)) {
      return r.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }
    if (/\/docs/.test(u)) {
      return r.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({ items: DOCUMENTOS, totalCount: DOCUMENTOS.length }),
      });
    }
    return r.fulfill({ status: 200, contentType: 'application/json', body: '{"items":[],"totalCount":0}' });
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

  await page.keyboard.press('Control+k');
  await page.keyboard.type('docs');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/docs/, { timeout: 30_000 });
}

test('la sección de documentos carga sin errores de consola', async ({ page }) => {
  const errores: string[] = [];
  page.on('pageerror', e => errores.push(e.message));

  await entrarADocs(page);

  // Un fallo al construir cualquiera de sus piezas aparecería aquí antes que en pantalla.
  expect(errores).toEqual([]);
});



test('no tiene violaciones graves de accesibilidad', async ({ page }) => {
  const { default: AxeBuilder } = await import('@axe-core/playwright');
  await entrarADocs(page);

  const resultado = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  const graves = resultado.violations.filter(
    v => v.impact === 'critical' || v.impact === 'serious');

  expect(graves.map(v => `${v.id} (${v.impact}) ×${v.nodes.length}: ${v.help}`)).toEqual([]);
});
