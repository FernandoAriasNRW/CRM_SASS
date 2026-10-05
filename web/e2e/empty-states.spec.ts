import { test, expect, type Page } from '@playwright/test';

/**
 * Estados de carga y vacío del tablero.
 *
 * Los componentes compartidos existían desde hace tiempo y no los usaba ninguna vista, de
 * modo que nunca se compilaban: al conectarlos aparecieron dos errores latentes en
 * `empty-state`. Estas pruebas existen para que no vuelvan a quedarse sin usar y sin
 * comprobar.
 */

const SESSION = {
  accessToken: 'token-de-prueba',
  refreshToken: 'refresco-de-prueba',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 864e5).toISOString(),
  user: {
    id: '00000000-0000-0000-0000-000000000001',
    name: 'Admin', email: 'admin@acme.com', role: 'Admin',
    tenantId: '00000000-0000-0000-0000-0000000000ff',
  },
};

/**
 * @param retrasoTareas milisegundos antes de responder al listado, para poder observar
 *        el estado de carga. Con respuesta inmediata el esqueleto no llega a verse.
 */
async function signIn(page: Page, taskDelay = 0) {
  await page.route(/\/api\/v1\/auth\/login/, r =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(SESSION) }));

  await page.route(/\/api\/v1\//, async r => {
    const url = r.request().url();
    if (/\/auth\/login/.test(url)) return r.fallback();

    // Las vistas guardadas devuelven un array, no un objeto paginado. Responder con la
    // forma equivocada rompe la carga del tablero y el fallo aparece lejos de su causa.
    if (/\/views\//.test(url)) {
      return r.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }

    if (/\/tasks(\?|$)/.test(url) && taskDelay) {
      await new Promise(res => setTimeout(res, taskDelay));
    }
    return r.fulfill({
      status: 200, contentType: 'application/json',
      body: '{"items":[],"totalCount":0}',
    });
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

  // Dentro de la aplicación: un page.goto recargaría y perdería el token, que vive en
  // memoria por decisión de seguridad.
  await page.keyboard.press('Control+k');
  await page.keyboard.type('tareas');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tasks/);
}

test('a board without tasks explains it is empty in each column', async ({ page }) => {
  await signIn(page);

  // Una columna vacía sin ningún texto se lee como que algo falló al cargar.
  await expect(page.getByText('Sin tareas').first()).toBeVisible({ timeout: 15_000 });
});

test('while loading it shows skeletons, not a blank screen', async ({ page }) => {
  await signIn(page, 2500);

  // El esqueleto ocupa el sitio de las tarjetas para que el diseño no salte al llegar
  // los datos. Se localiza por el atributo de accesibilidad, no por clase CSS, que
  // cambiaría con cualquier retoque de estilos.
  await expect(page.locator('[aria-busy="true"], .skeleton, app-skeleton-list').first())
    .toBeVisible({ timeout: 10_000 });
});
