import { test, expect, type Page } from '@playwright/test';

/**
 * Vista de Gantt.
 *
 * Lo que comprueban estas pruebas es que el diagrama **pinta lo que hay y no lo que se podría
 * suponer**: una tarea sin fecha de inicio sale como un hito en su vencimiento, no como una
 * barra de duración inventada, y una tarea sin vencimiento no sale, porque no hay dónde ponerla.
 */

const USUARIO = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
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
  description: '', status: 'To Do', priority: 'Normal', projectId: 'p1',
  assigneeId: null, estimatedHours: 4, tagIds: [],
};

/** Fechas fijas: un Gantt con fechas relativas a hoy se rompería solo el mes que viene. */
const CON_BARRA = { ...base, id: 'aaaaaaaa-0000-0000-0000-000000000001', title: 'Tarea planificada', startDate: '2026-08-18', dueDate: '2026-08-20' };
const HITO = { ...base, id: 'aaaaaaaa-0000-0000-0000-000000000002', title: 'Tarea sin inicio', startDate: null, dueDate: '2026-08-25' };
const BLOQUEADA = { ...base, id: 'aaaaaaaa-0000-0000-0000-000000000003', title: 'Tarea bloqueada', startDate: '2026-08-19', dueDate: '2026-08-21', blockedByCount: 1 };
const SIN_FECHAS = { ...base, id: 'aaaaaaaa-0000-0000-0000-000000000004', title: 'Tarea sin fechas', startDate: null, dueDate: null };

const json = (cuerpo: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(cuerpo),
});

async function entrar(page: Page, tareas: unknown[]) {
  await page.route(/\/api\/v1\/auth\/login/, r => r.fulfill(json(SESION)));

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    if (/\/auth\/login/.test(url)) return r.fallback();
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USUARIO));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USUARIO]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/custom-fields/.test(url)) return r.fulfill(json([]));
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

  await page.getByRole('button', { name: 'Gantt', exact: true }).click();
}

test('una tarea con inicio sale como barra y una sin inicio como hito', async ({ page }) => {
  await entrar(page, [CON_BARRA, HITO]);

  await expect(page.getByRole('button', { name: /Tarea planificada, del .* al / })).toBeVisible();
  await expect(page.getByRole('button', { name: /Tarea sin inicio, vence el / })).toBeVisible();
});

test('una tarea sin fecha límite no se pinta: no hay dónde ponerla', async ({ page }) => {
  await entrar(page, [CON_BARRA, SIN_FECHAS]);

  // Aparece en la columna de nombres sólo lo que tiene sitio en el calendario.
  // `exact`: sin él, «Tarea planificada» también casa con la etiqueta de su barra, que empieza
  // igual y sigue con las fechas.
  await expect(page.getByRole('button', { name: 'Tarea planificada', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Tarea sin fechas', exact: true })).toHaveCount(0);
});

test('sin ninguna tarea con fechas lo dice, en lugar de enseñar un eje vacío', async ({ page }) => {
  await entrar(page, [SIN_FECHAS]);

  await expect(page.getByText('Nada que planificar todavía')).toBeVisible();
});

test('una tarea bloqueada se marca', async ({ page }) => {
  await entrar(page, [BLOQUEADA]);

  await expect(page.getByTitle('La bloquea otra tarea')).toBeVisible();
});

test('pulsar una tarea abre su detalle', async ({ page }) => {
  await entrar(page, [CON_BARRA]);

  await page.getByRole('button', { name: /Tarea planificada, del / }).click();

  await expect(page.getByRole('heading', { name: 'Tarea planificada' })).toBeVisible({ timeout: 15_000 });
});
