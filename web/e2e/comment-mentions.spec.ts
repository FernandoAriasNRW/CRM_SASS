import { test, expect, type Page } from '@playwright/test';

/**
 * Menciones en los comentarios de una tarea.
 *
 * Se escribe `@` o `#`, se elige del desplegable y en el cuadro queda el nombre; al enviar viaja
 * con el formato que lee el servidor, y en el hilo la mención se pinta como enlace a su sitio.
 * También la vuelta: la tarea mencionada dice en «Mencionado en» qué comentario habla de ella.
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

const ANA = { id: '00000000-0000-0000-0000-0000000000a1', name: 'Ana Pérez', email: 'ana@acme.com' };
const TASK = {
  id: '00000000-0000-0000-0000-000000000101', title: 'Integrar la pasarela', description: '',
  status: 'To Do', projectId: 'p1', assigneeId: null, estimatedHours: 1,
  dueDate: new Date().toISOString(), tagIds: [], assignees: [],
};
const OTHER = { ...TASK, id: '00000000-0000-0000-0000-000000000102', title: 'Probar los reembolsos' };

const json = (body: unknown, status = 200) => ({ status, contentType: 'application/json', body: JSON.stringify(body) });

async function openTask(page: Page, mentioning: unknown[] = []): Promise<{ posted: string[] }> {
  const posted: string[] = [];
  const thread: unknown[] = [];

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    const method = r.request().method();
    if (/\/auth\/login/.test(url)) return r.fulfill(json(SESSION));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([SESSION.user, ANA]));
    if (/\/users(\?|$)/.test(url)) return r.fulfill(json([ANA]));
    if (/\/teams(\?|$)/.test(url)) return r.fulfill(json([]));
    if (/\/comments\/mentions\//.test(url)) return r.fulfill(json(mentioning));
    if (/\/comments\/Task\//.test(url)) {
      if (method === 'POST') {
        const text = r.request().postDataJSON().text as string;
        posted.push(text);
        const comment = { id: 'c1', authorId: SESSION.user.id, text, createdAtUtc: new Date().toISOString(), editedAtUtc: null, replyToId: null };
        thread.push(comment);
        return r.fulfill(json(comment, 201));
      }
      return r.fulfill(json(thread));
    }
    if (/\/tasks\/[^/]+\/(checklist|documents)/.test(url)) return r.fulfill(json([]));
    if (/\/tasks\/[^/]+\/subtasks/.test(url)) return r.fulfill(json({ items: [], totalCount: 0 }));
    if (/\/tasks\/[^/]+\/dependencies/.test(url)) return r.fulfill(json({ blockedBy: [], blocks: [] }));
    if (/\/(tags|docs|custom-fields)/.test(url)) return r.fulfill(json([]));
    if (/\/tasks\?/.test(url) && /search=/.test(url)) return r.fulfill(json({ items: [OTHER], totalCount: 1 }));
    if (/\/tasks(\?|$)/.test(url)) return r.fulfill(json({ items: [TASK, OTHER], totalCount: 2 }));
    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

  await page.keyboard.press('Control+k');
  await page.keyboard.type('tareas');
  await page.keyboard.press('Enter');
  await page.getByText(TASK.title, { exact: true }).first().click();
  await expect(page.getByText('Todavía no hay comentarios.')).toBeVisible({ timeout: 15_000 });

  return { posted };
}

test('a person and a task are mentioned from the comment box and shown as links', async ({ page }) => {
  const { posted } = await openTask(page);
  const box = page.getByRole('combobox', { name: 'Nuevo comentario' });

  await box.click();
  await box.pressSequentially('Hola @an');
  await page.getByRole('option', { name: /@Ana Pérez/ }).click();
  await expect(box).toHaveValue('Hola @Ana Pérez ');

  await box.pressSequentially('mira #reemb');
  // Con el teclado, como se elige de verdad mientras se escribe.
  await expect(page.getByRole('option', { name: /#Probar los reembolsos/ })).toBeVisible();
  await box.press('Enter');
  await expect(box).toHaveValue('Hola @Ana Pérez mira #Probar los reembolsos ');

  await page.getByRole('button', { name: 'Publicar el comentario' }).click();

  await expect.poll(() => posted.length).toBe(1);
  expect(posted[0]).toBe(`Hola @[Ana Pérez](Person:${ANA.id}) mira @[Probar los reembolsos](Task:${OTHER.id})`);

  // En el hilo se ve el nombre, y la tarea lleva a su ficha.
  const link = page.getByRole('link', { name: '#Probar los reembolsos' });
  await expect(link).toHaveAttribute('href', new RegExp(`/tasks\\?task=${OTHER.id}`));
  await expect(page.getByText('@Ana Pérez', { exact: true })).toBeVisible();
});

test('the mentioned task says which comment talks about it', async ({ page }) => {
  await openTask(page, [{
    commentId: 'c9', entityType: 'Task', entityId: OTHER.id, authorId: ANA.id,
    excerpt: 'Depende de #Integrar la pasarela', createdAtUtc: new Date().toISOString(),
  }]);

  await expect(page.getByText('En un comentario de una tarea')).toBeVisible();
  await expect(page.getByRole('link', { name: /Depende de #Integrar la pasarela/ }))
    .toHaveAttribute('href', new RegExp(`/tasks\\?task=${OTHER.id}`));
});
