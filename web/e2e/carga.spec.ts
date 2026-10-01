import { test, expect, type Page } from '@playwright/test';

/**
 * Vista de carga de trabajo.
 *
 * Lo que cuidan estas pruebas es que la tabla **no esconda trabajo**: las tareas sin fecha
 * límite no se reparten en ninguna semana, pero se cuentan y se dicen, porque una carga que
 * parece holgada por omisión es el error que hace decir «vamos bien» justo antes de un retraso.
 */

const USER = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const OTHER = {
  id: '00000000-0000-0000-0000-000000000002',
  name: 'Luisa Pérez', email: 'luisa@acme.com', role: 'Member',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const SESSION = {
  accessToken: 'token-de-prueba',
  accessTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  refreshToken: 'refresco-de-prueba',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 864e5).toISOString(),
  user: USER,
};

const base = {
  description: '', status: 'To Do', priority: 'Normal', projectId: 'p1', tagIds: [],
};

/** Fechas fijas: una vista de calendario con fechas relativas se rompería sola el mes que viene. */
const ADMIN_TASK = { ...base, id: 'a1', title: 'Tarea de Admin', assigneeId: USER.id, estimatedHours: 4, startDate: '2026-08-18', dueDate: '2026-08-20' };
const LUISA_TASK = { ...base, id: 'a2', title: 'Tarea de Luisa', assigneeId: OTHER.id, estimatedHours: 12, startDate: null, dueDate: '2026-08-19' };
const NO_DATE = { ...base, id: 'a3', title: 'Tarea sin plazo', assigneeId: USER.id, estimatedHours: 20, startDate: null, dueDate: null };
const COMPLETED = { ...base, id: 'a4', title: 'Tarea hecha', status: 'Done', assigneeId: USER.id, estimatedHours: 40, startDate: null, dueDate: '2026-08-19' };

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

async function signIn(page: Page, tasks: unknown[]) {
  await page.route(/\/api\/v1\/auth\/login/, r => r.fulfill(json(SESSION)));

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    if (/\/auth\/login/.test(url)) return r.fallback();
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USER));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USER, OTHER]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/custom-fields/.test(url)) return r.fulfill(json([]));
    if (/\/tasks\/dependencies/.test(url)) return r.fulfill(json([]));
    if (/\/tasks(\?|$)/.test(url)) return r.fulfill(json({ items: tasks, totalCount: tasks.length }));
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
  await signIn(page, [ADMIN_TASK, LUISA_TASK]);

  const names = page.locator('app-workload tbody tr td:first-child');
  await expect(names).toHaveText(['Luisa Pérez', 'Admin Administrator']);
});

test('los totales son las horas de cada uno', async ({ page }) => {
  await signIn(page, [ADMIN_TASK, LUISA_TASK]);

  const totals = page.locator('app-workload tbody tr td:last-child');
  await expect(totals).toHaveText(['12', '4']);
});

test('una tarea completada no cuenta como carga futura', async ({ page }) => {
  await signIn(page, [ADMIN_TASK, COMPLETED]);

  // Sin descartarla, el total del administrador serían 44 en lugar de 4.
  await expect(page.locator('app-workload tbody tr td:last-child')).toHaveText(['4']);
});

test('las tareas sin fecha límite no se esconden: se cuentan y se dicen', async ({ page }) => {
  await signIn(page, [ADMIN_TASK, NO_DATE]);

  await expect(page.getByText(/sin fecha límite/i)).toBeVisible();
  await expect(page.getByText(/la carga real es mayor/i)).toBeVisible();
});

test('sin tareas repartibles lo dice, en lugar de enseñar una tabla vacía', async ({ page }) => {
  await signIn(page, [NO_DATE]);

  await expect(page.getByText('Nada que repartir todavía')).toBeVisible();
});

/**
 * No hay línea de capacidad porque el producto no sabe la jornada de nadie. Pintar una sería
 * inventarse el dato que decide si algo está sobrecargado.
 */
test('la vista avisa de que el reparto es una estimación', async ({ page }) => {
  await signIn(page, [ADMIN_TASK]);

  await expect(page.getByText(/es una estimación, no un registro de dedicación/i)).toBeVisible();
});
