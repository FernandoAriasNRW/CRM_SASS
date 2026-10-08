import { test, expect, type Page } from '@playwright/test';

/**
 * El ámbito del tablero de tareas: toda la organización, mis tareas, un proyecto o un equipo.
 *
 * Lo que se comprueba es lo que pide la pantalla al servidor al elegir cada ámbito, y que desde
 * un proyecto se llegue a su tablero. El selector de proyecto se sacaba de las tareas cargadas y
 * enseñaba identificadores como nombre; aquí sale de la lista de proyectos, con sus nombres.
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

const PROJECTS = [
  { id: 'p-web', name: 'Web corporativa', status: 'Active', description: '', ownerId: SESSION.user.id, spaceId: 's1' },
  { id: 'p-app', name: 'App móvil', status: 'Active', description: '', ownerId: SESSION.user.id, spaceId: 's1' },
];
const TEAMS = [{ id: 't-soporte', name: 'Soporte', description: '', memberCount: 1, memberIds: ['u1'] }];

const TASK = {
  id: '00000000-0000-0000-0000-000000000101', title: 'Tarea del tablero', description: '',
  status: 'To Do', projectId: 'p-web', assigneeId: null, estimatedHours: 1,
  dueDate: new Date().toISOString(), tagIds: [],
};

const json = (body: unknown) => ({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });

async function signIn(page: Page): Promise<URL[]> {
  const taskRequests: URL[] = [];

  await page.route(/\/api\/v1\/auth\/login/, r => r.fulfill(json(SESSION)));
  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    if (/\/auth\/login/.test(url)) return r.fallback();
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([SESSION.user]));
    if (/\/teams(\?|$)/.test(url)) return r.fulfill(json(TEAMS));
    if (/\/projects(\?|$)/.test(url)) return r.fulfill(json({ items: PROJECTS, totalCount: PROJECTS.length }));
    if (/\/tasks(\?|$)/.test(url)) {
      taskRequests.push(new URL(url));
      return r.fulfill(json({ items: [TASK], totalCount: 1 }));
    }
    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

  return taskRequests;
}

async function openTasks(page: Page) {
  await page.keyboard.press('Control+k');
  await page.keyboard.type('tareas');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tasks/);
  await expect(page.getByText('Tarea del tablero', { exact: true })).toBeVisible({ timeout: 15_000 });
}

const last = (requests: URL[]) => requests[requests.length - 1];

test('choosing a project or a team asks the server for that board', async ({ page }) => {
  const requests = await signIn(page);
  await openTasks(page);

  const project = page.getByLabel('Tablero de un proyecto');
  await expect(project.locator('option', { hasText: 'Web corporativa' })).toHaveCount(1);
  await project.selectOption({ label: 'App móvil' });
  await expect.poll(() => last(requests).searchParams.get('projectId')).toBe('p-app');

  await page.getByLabel('Tablero de un equipo').selectOption({ label: 'Soporte' });
  await expect.poll(() => last(requests).searchParams.get('teamId')).toBe('t-soporte');
  expect(last(requests).searchParams.get('projectId'), 'un ámbito sustituye al anterior').toBeNull();

  await page.getByRole('button', { name: 'Mis tareas' }).click();
  await expect.poll(() => last(requests).searchParams.get('filter')).toBe('mine');
  expect(last(requests).searchParams.get('teamId')).toBeNull();

  await page.getByRole('button', { name: 'Toda la organización' }).click();
  await expect.poll(() => last(requests).searchParams.get('filter')).toBeNull();
});

test('from a project you go to its board', async ({ page }) => {
  const requests = await signIn(page);

  await page.keyboard.press('Control+k');
  await page.keyboard.type('proyectos');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/projects/);

  await page.getByRole('button', { name: 'Ver tablero' }).first().click();

  await expect(page).toHaveURL(/\/tasks\?.*projectId=p-web/);
  await expect.poll(() => requests.length > 0 && last(requests).searchParams.get('projectId')).toBe('p-web');
  await expect(page.getByLabel('Tablero de un proyecto')).toHaveValue('p-web');
});
