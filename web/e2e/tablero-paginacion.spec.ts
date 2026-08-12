import { test, expect, type Page } from '@playwright/test';

/**
 * Paginación por columna.
 *
 * El tablero trae hasta 1000 tareas de una vez y las pintaba todas. Ahora pinta 25 por
 * columna y revela el resto por tandas.
 *
 * El detalle que importa: el recorte se hace al repartir, no en la plantilla, porque
 * `cdkDropListData` y los índices del arrastre deben referirse al mismo array que se
 * pinta. Pintar un `slice` mientras el arrastre opera sobre la lista completa dejaría las
 * tarjetas en posiciones equivocadas.
 */

const SESION = {
  accessToken: 'token-de-prueba',
  refreshToken: 'refresco-de-prueba',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 864e5).toISOString(),
  user: {
    id: '00000000-0000-0000-0000-000000000001',
    name: 'Admin', email: 'admin@acme.com', role: 'Admin',
    tenantId: '00000000-0000-0000-0000-0000000000ff',
  },
};

/** 60 tareas en una sola columna: más de dos tandas. */
const TAREAS = Array.from({ length: 60 }, (_, i) => ({
  id: `00000000-0000-0000-0000-${String(i).padStart(12, '0')}`,
  title: `Tarea ${i + 1}`,
  description: '',
  status: 'To Do',
  projectId: 'p1',
  assigneeId: null,
  estimatedHours: 1,
  dueDate: new Date().toISOString(),
  tagIds: [],
}));

async function entrarAlTablero(page: Page) {
  await page.route(/\/api\/v1\/auth\/login/, r =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(SESION) }));

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    if (/\/auth\/login/.test(url)) return r.fallback();
    if (/\/views\//.test(url)) {
      return r.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }
    if (/\/tasks(\?|$)/.test(url)) {
      return r.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({ items: TAREAS, totalCount: TAREAS.length }),
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
  await page.keyboard.type('tareas');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tasks/);
  await expect(page.getByText('Tarea 1', { exact: true })).toBeVisible({ timeout: 15_000 });
}

const tarjetas = (page: Page) => page.locator('[cdkdroplist] [cdkdrag], [cdkDropList] [cdkDrag]');

test('pinta sólo la primera tanda, no las 60 tareas', async ({ page }) => {
  await entrarAlTablero(page);

  // Lo que se pinta es lo que cuesta: 60 tarjetas arrastrables se notan al desplazarse.
  await expect(page.getByText('Tarea 25', { exact: true })).toBeVisible();
  await expect(page.getByText('Tarea 26', { exact: true })).toBeHidden();
});

test('el contador de la columna muestra el total, no lo pintado', async ({ page }) => {
  await entrarAlTablero(page);

  // Si el contador dijera 25, el tablero estaría ocultando trabajo sin avisar.
  await expect(page.getByText('60', { exact: true }).first()).toBeVisible();
});

test('«mostrar más» revela la siguiente tanda', async ({ page }) => {
  await entrarAlTablero(page);

  await page.getByRole('button', { name: /mostrar 25 más/i }).click();

  await expect(page.getByText('Tarea 50', { exact: true })).toBeVisible();
  await expect(page.getByText('Tarea 51', { exact: true })).toBeHidden();
});

test('el botón desaparece al no quedar nada por mostrar', async ({ page }) => {
  await entrarAlTablero(page);

  await page.getByRole('button', { name: /mostrar .* más/i }).click();
  await page.getByRole('button', { name: /mostrar .* más/i }).click();

  await expect(page.getByText('Tarea 60', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /mostrar .* más/i })).toHaveCount(0);
});

test('el botón es alcanzable con el teclado', async ({ page }) => {
  await entrarAlTablero(page);

  // Es un <button> nativo, no un div con (click): recibe foco y se activa con Enter sin
  // necesidad de nada añadido.
  const boton = page.getByRole('button', { name: /mostrar .* más/i }).first();
  await boton.focus();
  await page.keyboard.press('Enter');

  await expect(page.getByText('Tarea 50', { exact: true })).toBeVisible();
});
