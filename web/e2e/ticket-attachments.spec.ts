import { test, expect, type Page } from '@playwright/test';
import { createSession, submitLogin } from './support/auth';

/**
 * Quitar un adjunto desde la ficha de un ticket.
 *
 * No había forma: una captura subida por error se quedaba en el ticket para siempre. Quitar se
 * confirma en la propia tarjeta del adjunto, y solo al confirmar se llama a la API.
 */

const USER = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const SESSION = createSession(USER);

const TICKET = {
  id: 'tk1', title: 'No carga la factura', description: '', priority: 'Normal', status: 'Open',
  createdAt: new Date().toISOString(), tagIds: [],
};

const attachment = (id: string, name: string) => ({
  id, name, url: `/files/${name}`, contentType: 'image/png', size: 256,
  uploadedAtUtc: new Date().toISOString(), fromExternal: false,
});

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

async function openTicket(page: Page): Promise<string[]> {
  const removed: string[] = [];

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    const method = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fulfill(json(SESSION));
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USER));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USER]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/tags/.test(url)) return r.fulfill(json([]));

    const removal = url.match(/\/tickets\/tk1\/attachments\/([^/?]+)$/);
    if (removal && method === 'DELETE') {
      removed.push(removal[1]);
      return r.fulfill({ status: 204, body: '' });
    }
    if (/\/tickets\/tk1\/attachments$/.test(url))
      return r.fulfill(json([attachment('a1', 'buena.png'), attachment('a2', 'equivocada.png')]));
    if (/\/tickets(\?|$)/.test(url)) return r.fulfill(json({ items: [TICKET], totalCount: 1 }));

    return r.fulfill(json({ items: [], totalCount: 0 }));
  });
  await page.route(/\/files\//, r => r.fulfill({ status: 404, body: '' }));

  await submitLogin(page);

  // Se navega por dentro: `page.goto` recargaría y perdería el token, que vive en memoria.
  await page.keyboard.press('Control+k');
  await page.keyboard.type('tickets');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tickets/);

  await page.getByText('No carga la factura').first().click();
  await expect(page.getByTestId('ticket-attachments')).toBeVisible({ timeout: 15_000 });
  return removed;
}

test('an attachment is removed only after confirming on its card', async ({ page }) => {
  const removed = await openTicket(page);
  const list = page.getByTestId('ticket-attachments');

  await list.getByRole('button', { name: 'Quitar equivocada.png' }).click();
  expect(removed, 'pedir quitar no quita todavía').toEqual([]);

  await list.getByRole('button', { name: 'Quitar', exact: true }).click();

  await expect(list.getByText('equivocada.png')).toHaveCount(0);
  await expect(list.getByText('buena.png')).toBeVisible();
  expect(removed).toEqual(['a2']);
});

test('cancelling leaves the attachment where it was', async ({ page }) => {
  const removed = await openTicket(page);
  const list = page.getByTestId('ticket-attachments');

  await list.getByRole('button', { name: 'Quitar equivocada.png' }).click();
  await list.getByRole('button', { name: 'Cancelar' }).click();

  await expect(list.getByText('equivocada.png')).toBeVisible();
  expect(removed).toEqual([]);
});
