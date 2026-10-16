import { test, expect, type Page } from '@playwright/test';
import { createSession, submitLogin } from './support/auth';

/**
 * Si el servidor rechaza un cambio de la ficha de una tarea, se avisa una vez y se deshace.
 *
 * La ficha levantaba su propio aviso y el interceptor el suyo: dos avisos para un fallo. Y el
 * estado rechazado se quedaba en el desplegable como si se hubiera guardado.
 */

const USER = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const SESSION = createSession(USER);

const TASK = {
  id: 't1', title: 'Preparar la demo', description: '', status: 'To Do', priority: 'Medium',
  projectId: 'p1', createdAt: new Date().toISOString(), tagIds: [], assignees: [],
};

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

async function openTask(page: Page) {
  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    const method = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fulfill(json(SESSION));
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USER));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USER]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/tags/.test(url)) return r.fulfill(json([]));
    if (/\/comments/.test(url)) return r.fulfill(json([]));
    if (/\/custom-fields/.test(url)) return r.fulfill(json([]));
    if (/\/tasks\/t1\/move/.test(url) && method === 'POST')
      return r.fulfill(json({ detail: 'La tarea está bloqueada por otra' }, 400));
    if (/\/tasks\/t1\/checklist/.test(url)) return r.fulfill(json([]));
    if (/\/tasks\/t1\/dependencies/.test(url)) return r.fulfill(json({ blockedBy: [], blocking: [] }));
    if (/\/tasks\/t1\/subtasks/.test(url)) return r.fulfill(json({ items: [], totalCount: 0 }));
    // El resto de lo que cuelga de la tarea son listas.
    if (/\/tasks\/t1\//.test(url)) return r.fulfill(json([]));
    if (/\/tasks\/t1$/.test(url)) return r.fulfill(json(TASK));
    if (/\/tasks(\?|$)/.test(url)) return r.fulfill(json({ items: [TASK], totalCount: 1 }));

    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await submitLogin(page);

  // Se navega por dentro: `page.goto` recargaría y perdería el token, que vive en memoria.
  await page.keyboard.press('Control+k');
  await page.keyboard.type('tareas');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tasks/);

  await page.getByText('Preparar la demo').first().click();
  await expect(page.getByLabel('Estado', { exact: true })).toBeVisible({ timeout: 15_000 });
}

test('a status change the server rejects is reported once and undone', async ({ page }) => {
  await openTask(page);

  const status = page.getByLabel('Estado', { exact: true });
  await status.selectOption('Done');

  // Un solo aviso, con lo que falló y el porqué que da el servidor.
  const toast = page.locator('app-toast-container .toast');
  await expect(toast.filter({ hasText: 'No se pudo cambiar el estado' })).toContainText('La tarea está bloqueada por otra');
  await expect(toast).toHaveCount(1);
  await expect(status).toHaveValue('To Do');
});
