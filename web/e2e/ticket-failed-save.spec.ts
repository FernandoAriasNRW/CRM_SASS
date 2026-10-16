import { test, expect, type Page } from '@playwright/test';
import { createSession, submitLogin } from './support/auth';

/**
 * Si el servidor rechaza un cambio de la ficha del ticket, la ficha lo deshace.
 *
 * El aviso global de errores decía el porqué, pero la ficha seguía enseñando el cambio como hecho:
 * el estado nuevo en su desplegable, la clasificación en su campo. Y cada cambio guardado se
 * emitía sobre el ticket tal como se abrió la ficha, así que uno deshacía en la lista el anterior.
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

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

async function openTicket(page: Page, { reject = true, status = 400, delayMs = 0 } = {}) {
  await page.route(/\/api\/v1\//, async r => {
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
    if (/\/tickets\/tk1\/attachments$/.test(url)) return r.fulfill(json([]));
    // El servidor rechaza cualquier cambio del ticket.
    if (/\/tickets\/tk1$/.test(url) && method === 'PATCH' && !reject) {
      await new Promise(resolve => setTimeout(resolve, delayMs));
      return r.fulfill({ status: 204, body: '' });
    }
    if (/\/tickets\/tk1$/.test(url) && method === 'PATCH')
      return r.fulfill(json({ detail: 'La clasificación no es válida' }, status));
    if (/\/tickets(\?|$)/.test(url)) return r.fulfill(json({ items: [TICKET], totalCount: 1 }));

    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await submitLogin(page);

  // Se navega por dentro: `page.goto` recargaría y perdería el token, que vive en memoria.
  await page.keyboard.press('Control+k');
  await page.keyboard.type('tickets');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tickets/);

  await page.getByText('No carga la factura').first().click();
  await expect(page.locator('#ticket-classification')).toBeVisible({ timeout: 15_000 });
}

test('a status change the server rejects is reported and undone', async ({ page }) => {
  await openTicket(page);

  const status = page.getByLabel('Estado', { exact: true });
  await status.selectOption('Resolved');

  await expect(page.getByText('La clasificación no es válida')).toBeVisible();
  await expect(status).toHaveValue('Open');
});

test('a classification the server rejects is reported and undone', async ({ page }) => {
  await openTicket(page);

  const classification = page.locator('#ticket-classification');
  await classification.fill('Facturación');
  await classification.blur();

  await expect(page.getByText('La clasificación no es válida')).toBeVisible();
  await expect(classification).toHaveValue('');
});

test('a later change does not undo in the list what was saved before', async ({ page }) => {
  await openTicket(page, { reject: false });

  const classification = page.locator('#ticket-classification');
  await classification.fill('Facturación');
  await classification.blur();
  const statusSaved = page.waitForResponse(r => r.request().method() === 'PATCH' && /status/.test(r.request().postData() ?? ''));
  await page.getByLabel('Estado', { exact: true }).selectOption('Resolved');
  await statusSaved;

  // Al reabrir, la ficha sale del ticket de la lista. Cada cambio se emitía sobre el ticket tal como
  // se abrió la ficha, así que el último en responder deshacía en la lista el otro.
  await page.keyboard.press('Escape');
  await expect(classification).toBeHidden();
  await page.getByText('No carga la factura').first().click();
  await expect(page.locator('#ticket-classification')).toHaveValue('Facturación');
  await expect(page.getByLabel('Estado', { exact: true })).toHaveValue('Resolved');
});

test('a forbidden change keeps you on the ticket and is undone', async ({ page }) => {
  await openTicket(page, { status: 403 });

  const status = page.getByLabel('Estado', { exact: true });
  await status.selectOption('Resolved');

  // Antes, cualquier 403 llevaba a Inicio y cerraba la ficha con lo que tuvieras a medias.
  await expect(page.getByText('No tienes permisos para realizar esta acción.')).toHaveCount(1);
  await expect(page).toHaveURL(/\/tickets/);
  await expect(status).toHaveValue('Open');
});

test('closing before a change is answered does not lose it from the board', async ({ page }) => {
  await openTicket(page, { reject: false, delayMs: 1500 });

  // Se cierra con el cambio aún en vuelo: la ficha espera a la respuesta. Antes se cerraba al
  // momento, la respuesta llegaba a una ficha que ya no existía, y el tablero seguía con «Abierto».
  await page.getByLabel('Estado', { exact: true }).selectOption('Resolved');
  await page.keyboard.press('Escape');
  await expect(page.locator('#ticket-classification')).toBeHidden({ timeout: 5_000 });

  await page.getByText('No carga la factura').first().click();
  await expect(page.getByLabel('Estado', { exact: true })).toHaveValue('Resolved');
});
