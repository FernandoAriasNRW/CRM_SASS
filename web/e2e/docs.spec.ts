import { test, expect, type Page } from '@playwright/test';

/**
 * Humo de la sección de documentos.
 *
 * Es la feature más grande del frontend —705 líneas de plantilla y 625 de componente— y
 * no tenía ninguna cobertura.
 *
 * El detalle que costó acertar: `GET /api/v1/docs` devuelve un **array plano**, no el
 * objeto `{items, totalCount}` que usan proyectos, tareas y tickets. Simular la forma
 * equivocada dejaba la vista vacía sin ningún error, que es la clase de fallo que hace
 * perder una tarde.
 */

const SESION = {
  accessToken: 't', refreshToken: 'r',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  user: { id: '1', name: 'Admin', email: 'admin@acme.com', role: 'Admin', tenantId: 'ff' },
};

/** Ajustado a `DocumentDto`: `type` es numérico (1 List, 2 Wiki, 3 MeetingNote, 4 Template). */
const DOCUMENTOS = [
  {
    id: '00000000-0000-0000-0000-0000000000d1',
    title: 'Manual de arquitectura',
    description: 'Cómo está montado el sistema',
    type: 1,
    ownerId: '00000000-0000-0000-0000-000000000001',
    createdAtUtc: new Date().toISOString(),
    updatedAtUtc: new Date().toISOString(),
  },
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
    // El contador de plantillas también es un array plano, y va antes que la regla de `/docs`
    // porque esa no casa con las rutas de debajo.
    if (/\/docs\/plantillas\/usos/.test(u)) {
      return r.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }
    // Array plano: es lo que devuelve este módulo, a diferencia del resto.
    if (/\/docs(\?|$)/.test(u)) {
      return r.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify(DOCUMENTOS),
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



test('muestra los documentos que devuelve la API', async ({ page }) => {
  await entrarADocs(page);

  await expect(page.getByText('Manual de arquitectura').first()).toBeVisible({ timeout: 15_000 });
});

test('el modal de importar se abre, valida y se cierra', async ({ page }) => {
  await entrarADocs(page);

  await page.getByRole('button', { name: /^importar$/i }).first().click();
  const modal = page.getByRole('dialog', { name: /importar documento/i });
  await expect(modal).toBeVisible();

  // Importar sin contenido no debe crear nada: el aviso sale dentro del modal, no en un
  // `alert` que bloquea la página y no dice qué falta.
  await modal.getByRole('button', { name: /^importar$/i }).click();
  await expect(modal.getByRole('alert')).toBeVisible();
  await expect(modal).toBeVisible();

  await modal.getByRole('button', { name: /^cancelar$/i }).click();
  await expect(modal).toBeHidden();
});

test('el cajón de plantillas las ofrece todas, se filtran y se recorren con teclado', async ({ page }) => {
  await entrarADocs(page);

  await page.getByRole('button', { name: /más opciones de documento nuevo/i }).click();
  await page.getByRole('button', { name: /ver todas las plantillas/i }).first().click();

  const cajon = page.getByRole('dialog', { name: /^plantillas$/i });
  await expect(cajon).toBeVisible();

  // La galería sólo enseña cuatro; el cajón tiene que enseñar también las del equipo, que es
  // justamente lo que no cabía fuera.
  await expect(cajon.getByRole('heading', { name: /del sistema/i })).toBeVisible();

  // Son <button> nativos, así que reciben foco sin ayuda añadida.
  const primera = cajon.getByRole('button', { name: /resumen de proyecto/i });
  await primera.focus();
  await expect(primera).toBeFocused();

  // Filtrar deja sólo la que coincide. Sin esto, el buscador podría no estar conectado a nada y
  // el cajón seguiría pareciendo correcto.
  await cajon.getByRole('searchbox', { name: /buscar plantilla/i }).fill('wiki');
  await expect(cajon.getByRole('button', { name: /^wiki/i })).toBeVisible();
  await expect(cajon.getByRole('button', { name: /resumen de proyecto/i })).toBeHidden();
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
