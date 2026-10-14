import { test, expect, type Page } from '@playwright/test';
import { createSession, submitLogin } from './support/auth';

/**
 * Archivar, mandar a la papelera, restaurar y compartir desde la ficha.
 *
 * La API existía y el menú enseñaba «Archivado» y «Papelera», pero ningún botón hacía nada de
 * esto: esas listas solo se llenaban desde la API. Se prueba sobre la ficha de un ticket; las de
 * tarea y proyecto usan el mismo componente.
 */

const USER = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};
const ANA = { id: 'u-ana', tenantId: USER.tenantId, name: 'Ana Pérez', email: 'ana@acme.com', role: 'Member', isActive: true };

const SESSION = createSession(USER);

const TICKET = {
  id: 'tk1', title: 'No carga la factura', description: '', priority: 'Normal', status: 'Open',
  createdAt: new Date().toISOString(), tagIds: [],
};

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

interface Calls { requests: string[]; sharedWith: string[] }

async function openTicket(page: Page): Promise<Calls> {
  const calls: Calls = { requests: [], sharedWith: [] };

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    const method = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fulfill(json(SESSION));
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USER));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USER, ANA]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/tags/.test(url)) return r.fulfill(json([]));
    // Listas y no páginas: con `{ items }` el componente de comentarios falla en cada pasada de
    // detección de cambios y la ficha deja de redibujarse.
    if (/\/comments/.test(url)) return r.fulfill(json([]));
    if (/\/docs\/mentions/.test(url)) return r.fulfill(json([]));

    const sharing = url.match(/\/sharing\/Ticket\/tk1(?:\/([^/?]+))?$/);
    if (sharing) {
      if (method === 'GET') return r.fulfill(json(calls.sharedWith));
      calls.requests.push(`${method} sharing ${sharing[1]} ${r.request().postData() ?? ''}`.trim());
      if (method === 'PUT') calls.sharedWith = [...calls.sharedWith, sharing[1]];
      if (method === 'DELETE') calls.sharedWith = calls.sharedWith.filter(id => id !== sharing[1]);
      return r.fulfill({ status: 204, body: '' });
    }

    const action = url.match(/\/tickets\/tk1\/(archive|unarchive|restore)$/);
    if (action && method === 'POST') {
      calls.requests.push(action[1]);
      return r.fulfill({ status: 204, body: '' });
    }
    if (/\/tickets\/tk1$/.test(url) && method === 'DELETE') {
      calls.requests.push('delete');
      return r.fulfill({ status: 204, body: '' });
    }
    if (/\/tickets\/tk1\/attachments$/.test(url)) return r.fulfill(json([]));
    if (/\/tickets(\?|$)/.test(url)) return r.fulfill(json({ items: [TICKET], totalCount: 1 }));

    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await submitLogin(page);

  // Se navega por dentro: `page.goto` recargaría y perdería el token, que vive en memoria.
  await page.keyboard.press('Control+k');
  await page.keyboard.type('tickets');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tickets/);
  return calls;
}

async function openCard(page: Page) {
  await page.getByText('No carga la factura').first().click();
  await expect(page.getByRole('button', { name: 'Archivar' })).toBeVisible({ timeout: 15_000 });
}

test('archiving closes the card and asks the api to archive', async ({ page }) => {
  const calls = await openTicket(page);
  await openCard(page);

  await page.getByRole('button', { name: 'Archivar' }).click();

  await expect(page.getByRole('button', { name: 'Archivar' })).toHaveCount(0);
  expect(calls.requests).toEqual(['archive']);
});

test('sending to the trash is confirmed on the button itself', async ({ page }) => {
  const calls = await openTicket(page);
  await openCard(page);

  await page.getByRole('button', { name: 'Papelera' }).last().click();
  expect(calls.requests, 'pedirlo no lo manda todavía').toEqual([]);
  await page.getByRole('button', { name: 'Sí, enviar' }).click();

  await expect(page.getByRole('button', { name: 'Sí, enviar' })).toHaveCount(0);
  expect(calls.requests).toEqual(['delete']);
});

test('sharing with someone lists them and can be undone', async ({ page }) => {
  const calls = await openTicket(page);
  await openCard(page);

  await page.getByRole('button', { name: 'Compartir' }).first().click();
  const panel = page.getByTestId('share-panel');
  await expect(panel.getByText('Todavía no está compartido con nadie.')).toBeVisible();

  await panel.getByLabel('Persona').selectOption({ label: 'Ana Pérez' });
  await panel.getByLabel('Nivel').selectOption('Edit');
  await panel.getByRole('button', { name: 'Compartir' }).click();

  await expect(panel.getByText('Ana Pérez')).toBeVisible();
  expect(calls.requests).toEqual(['PUT sharing u-ana {"level":"Edit"}']);

  await panel.getByRole('button', { name: 'Dejar de compartir con Ana Pérez' }).click();
  await expect(panel.getByText('Todavía no está compartido con nadie.')).toBeVisible();
  expect(calls.requests.at(-1)).toBe('DELETE sharing u-ana');
});

test('from the trash the only action is to restore', async ({ page }) => {
  const calls = await openTicket(page);

  await page.getByRole('button', { name: 'Papelera' }).first().click();
  await expect(page).toHaveURL(/filter=trash/);
  await openCardInTrash(page);

  await expect(page.getByRole('button', { name: 'Archivar' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Restaurar' }).click();
  expect(calls.requests).toEqual(['restore']);
});

async function openCardInTrash(page: Page) {
  await page.getByText('No carga la factura').first().click();
  await expect(page.getByRole('button', { name: 'Restaurar' })).toBeVisible({ timeout: 15_000 });
}
