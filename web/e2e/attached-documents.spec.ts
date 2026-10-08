import { test, expect, type Page, type Route } from '@playwright/test';

/**
 * Documentos adjuntos a una tarea, desde los dos lados.
 *
 * En la ficha de la tarea había un botón «Adjuntar archivo» que no hacía nada. Ahora se adjunta
 * un documento del módulo de documentos, y desde el documento se ve —y se elige— a qué tareas
 * pertenece. Lo que se comprueba es lo que se manda al servidor y lo que se pinta con su respuesta.
 */

const SESSION = {
  accessToken: 't', refreshToken: 'r',
  accessTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  refreshTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  user: {
    id: '00000000-0000-0000-0000-000000000001', name: 'Admin', email: 'admin@acme.com', role: 'Admin',
    tenantId: '00000000-0000-0000-0000-0000000000ff',
  },
};

const DOC = {
  id: '00000000-0000-0000-0000-0000000000d1', title: 'Especificación del pago', description: '',
  type: 1, ownerId: SESSION.user.id, createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString(),
};

const TASK = {
  id: '00000000-0000-0000-0000-000000000101', title: 'Integrar la pasarela', description: '',
  status: 'To Do', projectId: 'p1', assigneeId: null, estimatedHours: 1,
  dueDate: new Date().toISOString(), tagIds: [], assignees: [],
};

const json = (body: unknown, status = 200) => ({ status, contentType: 'application/json', body: JSON.stringify(body) });

type Sent = { method: string; url: string; body: unknown };

async function signIn(page: Page, handle: (route: Route, url: string, method: string) => Promise<void> | void): Promise<Sent[]> {
  const sent: Sent[] = [];

  await page.route(/\/api\/v1\//, async r => {
    const url = r.request().url();
    const method = r.request().method();
    if (/\/auth\/login/.test(url)) return r.fulfill(json(SESSION));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([SESSION.user]));
    if (/\/teams(\?|$)/.test(url)) return r.fulfill(json([]));
    if (/\/docs\/templates\/usage/.test(url)) return r.fulfill(json([]));
    // Lo que pide la ficha de la tarea, cada cosa con la forma que devuelve la API.
    if (/\/tasks\/[^/]+\/(checklist|subtasks)/.test(url)) return r.fulfill(json(/subtasks/.test(url) ? { items: [], totalCount: 0 } : []));
    if (/\/tasks\/[^/]+\/dependencies/.test(url)) return r.fulfill(json({ blockedBy: [], blocks: [] }));
    if (/\/(tags|comments|docs\/mentions|custom-fields)/.test(url)) return r.fulfill(json([]));
    if (method !== 'GET') sent.push({ method, url, body: r.request().postDataJSON?.() ?? null });
    return handle(r, url, method);
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });
  return sent;
}

test('a document is attached from the task and can be removed', async ({ page }) => {
  let attached: unknown[] = [];

  const sent = await signIn(page, (r, url, method) => {
    if (/\/tasks\/[^/]+\/documents\/[^/]+$/.test(url) && method === 'DELETE') {
      attached = [];
      return r.fulfill({ status: 204, body: '' });
    }
    if (/\/tasks\/[^/]+\/documents$/.test(url)) {
      if (method === 'POST') {
        attached = [{ documentId: DOC.id, title: DOC.title, documentUpdatedAtUtc: DOC.updatedAtUtc, attachedById: SESSION.user.id, attachedAtUtc: new Date().toISOString() }];
        return r.fulfill(json(attached[0]));
      }
      return r.fulfill(json(attached));
    }
    if (/\/docs(\?|$)/.test(url)) return r.fulfill(json([DOC]));
    if (/\/tasks(\?|$)/.test(url)) return r.fulfill(json({ items: [TASK], totalCount: 1 }));
    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.keyboard.press('Control+k');
  await page.keyboard.type('tareas');
  await page.keyboard.press('Enter');
  await page.getByText(TASK.title, { exact: true }).first().click();

  await expect(page.getByText('Esta tarea no tiene documentos adjuntos.')).toBeVisible({ timeout: 15_000 });
  await page.getByRole('button', { name: 'Adjuntar documento' }).click();
  await page.getByRole('button', { name: DOC.title }).click();

  await expect(page.getByRole('link', { name: DOC.title })).toBeVisible();
  expect(sent.find(s => s.method === 'POST')).toMatchObject({ body: { documentId: DOC.id } });

  await page.getByRole('button', { name: 'Quitar de la tarea' }).click();
  await expect(page.getByText('Esta tarea no tiene documentos adjuntos.')).toBeVisible();
  expect(sent.some(s => s.method === 'DELETE' && s.url.endsWith(`/tasks/${TASK.id}/documents/${DOC.id}`))).toBe(true);
});

test('the document shows its tasks and is attached to another from there', async ({ page }) => {
  const OTHER = { ...TASK, id: '00000000-0000-0000-0000-000000000102', title: 'Probar los reembolsos' };

  const sent = await signIn(page, (r, url, method) => {
    if (/\/tasks\/with-document\//.test(url)) {
      return r.fulfill(json([{ taskId: TASK.id, projectId: 'p1', title: TASK.title, status: 'To Do' }]));
    }
    if (/\/tasks\/[^/]+\/documents$/.test(url) && method === 'POST') {
      return r.fulfill(json({ documentId: DOC.id, title: DOC.title, documentUpdatedAtUtc: DOC.updatedAtUtc, attachedById: SESSION.user.id, attachedAtUtc: new Date().toISOString() }));
    }
    if (/\/tasks(\?|$)/.test(url)) return r.fulfill(json({ items: [TASK, OTHER], totalCount: 2 }));
    if (/\/docs\/[^/]+\/pages/.test(url)) {
      return r.fulfill(json([{ id: '00000000-0000-0000-0000-0000000000a1', documentId: DOC.id, parentPageId: null, title: 'Página', content: '<p>x</p>', order: 0 }]));
    }
    if (/\/docs(\?|$)/.test(url)) return r.fulfill(json([DOC]));
    return r.fulfill(json([]));
  });

  await page.keyboard.press('Control+k');
  await page.keyboard.type('docs');
  await page.keyboard.press('Enter');
  await page.getByText(DOC.title).first().click();
  await expect(page.getByRole('textbox', { name: /título del documento/i })).toBeVisible({ timeout: 15_000 });

  await page.getByRole('tab', { name: 'Tareas' }).click();
  await expect(page.getByRole('link', { name: new RegExp(TASK.title) })).toBeVisible();

  await page.getByLabel('Adjuntar a una tarea').fill('reembolsos');
  // La tarea que ya lo tiene no se ofrece otra vez.
  await expect(page.getByRole('button', { name: TASK.title })).toHaveCount(0);
  await page.getByRole('button', { name: OTHER.title }).click();

  await expect(page.getByRole('link', { name: new RegExp(OTHER.title) })).toBeVisible();
  expect(sent.find(s => s.method === 'POST' && s.url.endsWith(`/tasks/${OTHER.id}/documents`))).toMatchObject({ body: { documentId: DOC.id } });
});
