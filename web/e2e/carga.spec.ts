import { test, expect, type Page } from '@playwright/test';

/**
 * Vista de carga de trabajo.
 *
 * Lo que cuidan estas pruebas es que la tabla **no esconda trabajo**: las tareas sin fecha
 * límite no se reparten en ninguna semana, pero se cuentan y se dicen, porque una carga que
 * parece holgada por omisión es el error que hace decir «vamos bien» justo antes de un retraso.
 */

const USUARIO = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const OTRA = {
  id: '00000000-0000-0000-0000-000000000002',
  name: 'Luisa Pérez', email: 'luisa@acme.com', role: 'Member',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const SESION = {
  accessToken: 'token-de-prueba',
  accessTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  refreshToken: 'refresco-de-prueba',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 864e5).toISOString(),
  user: USUARIO,
};

const base = {
  description: '', status: 'To Do', priority: 'Normal', projectId: 'p1', tagIds: [],
};

/** Fechas fijas: una vista de calendario con fechas relativas se rompería sola el mes que viene. */
const DE_ADMIN = { ...base, id: 'a1', title: 'Tarea de Admin', assigneeId: USUARIO.id, estimatedHours: 4, startDate: '2026-08-18', dueDate: '2026-08-20' };
const DE_LUISA = { ...base, id: 'a2', title: 'Tarea de Luisa', assigneeId: OTRA.id, estimatedHours: 12, startDate: null, dueDate: '2026-08-19' };
const SIN_FECHA = { ...base, id: 'a3', title: 'Tarea sin plazo', assigneeId: USUARIO.id, estimatedHours: 20, startDate: null, dueDate: null };
const COMPLETADA = { ...base, id: 'a4', title: 'Tarea hecha', status: 'Done', assigneeId: USUARIO.id, estimatedHours: 40, startDate: null, dueDate: '2026-08-19' };

const json = (cuerpo: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(cuerpo),
});

async function entrar(page: Page, tareas: unknown[]) {
  await page.route(/\/api\/v1\/auth\/login/, r => r.fulfill(json(SESION)));

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    if (/\/auth\/login/.test(url)) return r.fallback();
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USUARIO));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USUARIO, OTRA]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/custom-fields/.test(url)) return r.fulfill(json([]));
    if (/\/tasks\/dependencies/.test(url)) return r.fulfill(json([]));
    if (/\/tasks(\?|$)/.test(url)) return r.fulfill(json({ items: tareas, totalCount: tareas.length }));
    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

  // Se navega por dentro: `page.goto` recargaría y perdería el token, que vive en memoria.
  await page.keyboard.press('Control+k');
  await page.keyboard.type('tareas');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tasks/, { timeout: 15_000 });

  await page.getByRole('button', { name: 'Carga', exact: true }).click();
}

test('reparte por persona y pone delante a quien más acumula', async ({ page }) => {
  await entrar(page, [DE_ADMIN, DE_LUISA]);

  const nombres = page.locator('app-carga tbody tr td:first-child');
  await expect(nombres).toHaveText(['Luisa Pérez', 'Admin Administrator']);
});

test('los totales son las horas de cada uno', async ({ page }) => {
  await entrar(page, [DE_ADMIN, DE_LUISA]);

  const totales = page.locator('app-carga tbody tr td:last-child');
  await expect(totales).toHaveText(['12', '4']);
});

test('una tarea completada no cuenta como carga futura', async ({ page }) => {
  await entrar(page, [DE_ADMIN, COMPLETADA]);

  // Sin descartarla, el total del administrador serían 44 en lugar de 4.
  await expect(page.locator('app-carga tbody tr td:last-child')).toHaveText(['4']);
});

test('las tareas sin fecha límite no se esconden: se cuentan y se dicen', async ({ page }) => {
  await entrar(page, [DE_ADMIN, SIN_FECHA]);

  await expect(page.getByText(/sin fecha límite/i)).toBeVisible();
  await expect(page.getByText(/la carga real es mayor/i)).toBeVisible();
});

test('sin tareas repartibles lo dice, en lugar de enseñar una tabla vacía', async ({ page }) => {
  await entrar(page, [SIN_FECHA]);

  await expect(page.getByText('Nada que repartir todavía')).toBeVisible();
});

/**
 * No hay línea de capacidad porque el producto no sabe la jornada de nadie. Pintar una sería
 * inventarse el dato que decide si algo está sobrecargado.
 */
test('la vista avisa de que el reparto es una estimación', async ({ page }) => {
  await entrar(page, [DE_ADMIN]);

  await expect(page.getByText(/es una estimación, no un registro de dedicación/i)).toBeVisible();
});
