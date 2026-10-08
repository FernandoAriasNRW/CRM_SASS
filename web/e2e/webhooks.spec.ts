import { test, expect, type Page } from '@playwright/test';

/**
 * La pestaña de webhooks de administración.
 *
 * Estaba hecha contra una API que no existía: mandaba los eventos como texto separado por comas,
 * ofrecía eventos que nadie emitía y pedía reintentos y tiempos que no se guardaban. Lo que se
 * comprueba aquí es lo que la pantalla pide al servidor y lo que enseña con su respuesta.
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

const CATALOG = [
  { name: 'task.created', category: 'tasks' },
  { name: 'task.updated', category: 'tasks' },
  { name: 'ticket.created', category: 'tickets' },
  { name: 'user.created', category: 'users' },
];

const SUBSCRIPTION = {
  id: 'w1', name: 'Al CRM', url: 'https://hooks.example.com/crm', eventTypes: ['ticket.created'],
  isActive: true, createdAtUtc: new Date().toISOString(), updatedAtUtc: null,
  successCount: 3, failureCount: 1, pendingCount: 0, lastDeliveryAtUtc: new Date().toISOString(),
};

const json = (body: unknown, status = 200) => ({ status, contentType: 'application/json', body: JSON.stringify(body) });

type Sent = { method: string; url: string; body: Record<string, unknown> | null };

async function openWebhooks(page: Page, subscriptions: unknown[] = []): Promise<Sent[]> {
  const sent: Sent[] = [];

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    const method = r.request().method();
    if (/\/auth\/login/.test(url)) return r.fulfill(json(SESSION));
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(SESSION.user));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([SESSION.user]));
    if (/\/users(\?|$)/.test(url)) return r.fulfill(json([SESSION.user]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));

    if (/\/webhooks/.test(url)) {
      if (method !== 'GET') sent.push({ method, url, body: r.request().postDataJSON() });
      if (/\/webhooks\/events/.test(url)) return r.fulfill(json(CATALOG));
      if (/\/deliveries/.test(url)) {
        return r.fulfill(json([{ id: 'd1', eventName: 'webhook.test', status: 'Failed', attempts: 6, createdAtUtc: new Date().toISOString(), completedAtUtc: null, nextAttemptAtUtc: null, lastStatusCode: 500, lastError: 'El destino respondió 500' }]));
      }
      if (/\/test$/.test(url)) return r.fulfill(json({ id: 'd2', eventName: 'webhook.test', status: 'Pending', attempts: 0 }));
      if (method === 'POST') {
        const body = r.request().postDataJSON();
        return r.fulfill(json({ subscription: { ...SUBSCRIPTION, id: 'w2', ...body }, secret: 'whsec_nuevo' }, 201));
      }
      return r.fulfill(json(subscriptions));
    }
    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

  await page.getByRole('link', { name: 'Admin' }).click();
  await expect(page).toHaveURL(/\/admin/, { timeout: 15_000 });
  await page.getByRole('button', { name: 'Webhooks e Integraciones' }).click();

  return sent;
}

test('a webhook is created with the chosen events, none preselected, and its secret is shown', async ({ page }) => {
  const sent = await openWebhooks(page);

  await page.getByRole('button', { name: 'Nuevo webhook' }).click();
  await page.getByLabel('Nombre del webhook').fill('Avisos de tareas');
  await page.getByLabel('URL de destino').fill('https://hooks.example.com/tareas');

  // Ninguno marcado de entrada: no se recibe nada que no se elija.
  await expect(page.getByRole('checkbox', { name: 'task.created' })).not.toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'user.created' })).not.toBeChecked();

  // Guardar sin elegir nada se rechaza en la propia pantalla.
  await page.getByRole('button', { name: 'Crear Webhook', exact: true }).click();
  await expect(page.getByText(/Elige al menos un evento/)).toBeVisible();

  await page.getByRole('checkbox', { name: 'task.created' }).check();
  await page.getByRole('checkbox', { name: 'ticket.created' }).check();
  await page.getByRole('button', { name: 'Crear Webhook', exact: true }).click();

  await expect.poll(() => sent.find(s => s.method === 'POST')?.body).toEqual({
    name: 'Avisos de tareas', url: 'https://hooks.example.com/tareas',
    eventTypes: ['task.created', 'ticket.created'], isActive: true,
  });
  await expect(page.getByText('whsec_nuevo')).toBeVisible();
});

test('the delivery log says what failed and why, and a test can be sent', async ({ page }) => {
  const sent = await openWebhooks(page, [SUBSCRIPTION]);

  await expect(page.getByText('Al CRM')).toBeVisible();
  await page.getByRole('button', { name: 'Enviar prueba' }).click();
  await expect.poll(() => sent.some(s => s.method === 'POST' && s.url.endsWith('/webhooks/w1/test'))).toBe(true);

  await expect(page.getByRole('cell', { name: 'Fallido' })).toBeVisible();
  await expect(page.getByText('El destino respondió 500')).toBeVisible();
});
