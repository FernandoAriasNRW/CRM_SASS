import { test, expect, type Page } from '@playwright/test';

/**
 * La campana de avisos y las preferencias.
 *
 * Un aviso dice de qué trata y lleva a ello: pulsar «Te han asignado una tarea» abre la tarea. Y las
 * preferencias salen del catálogo del servidor, agrupadas, con cada cambio guardado al momento.
 */

const SESSION = {
  accessToken: 't', refreshToken: 'r',
  accessTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  refreshTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  user: {
    id: '00000000-0000-0000-0000-000000000001', name: 'Ana', email: 'ana@acme.com', role: 'Member',
    tenantId: '00000000-0000-0000-0000-0000000000ff',
  },
};

const TASK_ID = '00000000-0000-0000-0000-000000000101';

const NOTIFICATION = {
  id: 'n1', recipientUserId: SESSION.user.id, subject: 'Te han asignado una tarea', body: '«Revisar el contrato»',
  status: 'Pending', kind: 'task.assigned', entityType: 'Task', entityId: TASK_ID,
  createdAt: new Date().toISOString(), readAt: null,
};

const PREFERENCES = {
  emailEnabled: true, pushEnabled: false, quietHoursEnabled: false, quietHoursStart: '22:00', quietHoursEnd: '08:00',
  types: [
    { kind: 'task.assigned', category: 'tasks', enabled: true, adminOnly: false },
    { kind: 'task.updated', category: 'tasks', enabled: false, adminOnly: false },
    { kind: 'mention', category: 'mentions', enabled: true, adminOnly: false },
  ],
};

const json = (body: unknown, status = 200) => ({ status, contentType: 'application/json', body: JSON.stringify(body) });

async function signIn(page: Page): Promise<{ method: string; url: string; body: unknown }[]> {
  const sent: { method: string; url: string; body: unknown }[] = [];

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    const method = r.request().method();
    if (/\/auth\/login/.test(url)) return r.fulfill(json(SESSION));
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(SESSION.user));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([SESSION.user]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));

    if (/\/notifications\/preferences/.test(url)) {
      if (method === 'PUT') {
        const body = r.request().postDataJSON();
        sent.push({ method, url, body });
        const types = PREFERENCES.types.map(t => {
          const change = (body.types as { kind: string; enabled: boolean }[]).find(c => c.kind === t.kind);
          return change ? { ...t, enabled: change.enabled } : t;
        });
        return r.fulfill(json({ ...PREFERENCES, types }));
      }
      return r.fulfill(json(PREFERENCES));
    }
    if (/\/notifications\/[^/]+\/read/.test(url)) {
      sent.push({ method, url, body: null });
      return r.fulfill(json({}));
    }
    if (/\/notifications(\?|$)/.test(url)) return r.fulfill(json({ items: [NOTIFICATION], totalCount: 1 }));
    if (/\/tasks\/[^/]+$/.test(url)) {
      return r.fulfill(json({ id: TASK_ID, title: 'Revisar el contrato', description: '', status: 'To Do', projectId: 'p1', assigneeId: SESSION.user.id, estimatedHours: 1, dueDate: new Date().toISOString(), tagIds: [], assignees: [] }));
    }
    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('ana@acme.com');
  await page.getByPlaceholder('••••••••').fill('secreto');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });
  return sent;
}

test('a notification takes you to what it is about and counts as read', async ({ page }) => {
  const sent = await signIn(page);

  await page.getByRole('button', { name: /Notificaciones \(1 sin leer\)/ }).click();
  await page.getByRole('button', { name: /Te han asignado una tarea/ }).click();

  await expect(page).toHaveURL(new RegExp(`/tasks\\?task=${TASK_ID}`));
  expect(sent.some(s => s.url.endsWith('/notifications/n1/read'))).toBe(true);
});

test('preferences come from the catalog and each change is saved', async ({ page }) => {
  const sent = await signIn(page);

  await page.getByRole('button', { name: /Notificaciones/ }).first().click();
  await page.getByRole('button', { name: 'Preferencias de avisos' }).click();

  const dialog = page.getByRole('dialog', { name: 'Preferencias de avisos' });
  await expect(dialog.getByText('Tareas', { exact: true })).toBeVisible();
  await expect(dialog.getByRole('checkbox', { name: 'Me asignan una tarea' })).toBeChecked();
  await expect(dialog.getByRole('checkbox', { name: 'Cualquier cambio en una tarea mía' })).not.toBeChecked();

  await dialog.getByRole('checkbox', { name: 'Me asignan una tarea' }).uncheck();

  await expect.poll(() => sent.find(s => s.method === 'PUT')?.body).toMatchObject({
    types: [{ kind: 'task.assigned', enabled: false }],
  });
  await expect(dialog.getByRole('checkbox', { name: 'Me asignan una tarea' })).not.toBeChecked();
});
