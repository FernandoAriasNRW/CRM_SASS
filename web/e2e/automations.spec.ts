import { test, expect, type Page } from '@playwright/test';

/**
 * La pantalla de automatizaciones.
 *
 * Lo que se comprueba aquí es que **el formulario se construye con el vocabulario que sirve el
 * servidor**: si esta pantalla llevara su propia lista de disparadores, se desincronizaría el día
 * que se añada uno y dejaría configurar algo que el servidor no entiende.
 */

const USER = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const SESSION = {
  accessToken: 'token-de-prueba',
  accessTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  refreshToken: 'refresco-de-prueba',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 864e5).toISOString(),
  user: USER,
};

const VOCABULARY = {
  triggers: ['TaskCreated', 'TaskStatusChanged'],
  fields: ['Status', 'AssigneeId'],
  operators: ['EqualTo', 'IsEmpty'],
  actions: ['ChangeStatus', 'ChangePriority'],
  fieldsByTrigger: {
    TaskCreated: ['AssigneeId'],
    TaskStatusChanged: ['Status', 'AssigneeId'],
  },
};

const RULE = {
  id: 'r1', name: 'Bajar al cerrar', trigger: 'TaskStatusChanged', isActive: true,
  conditions: [{ field: 'Status', operator: 'EqualTo', value: 'Done' }],
  actions: [{ type: 'ChangePriority', value: 'Low' }],
  executionCount: 3, lastExecutedAtUtc: '2026-08-14T10:00:00Z',
};

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

type SentRequest = { method: string; url: string; body: Record<string, unknown> };

async function signIn(
  page: Page,
  rules: unknown[] = [],
  createResponse?: { status: number; body: unknown },
): Promise<SentRequest[]> {
  const sent: SentRequest[] = [];

  await page.route(/\/api\/v1\/auth\/login/, r => r.fulfill(json(SESSION)));

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    const method = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fallback();
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USER));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USER]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/custom-fields/.test(url)) return r.fulfill(json([]));

    if (/\/automations\/vocabulary/.test(url)) return r.fulfill(json(VOCABULARY));

    if (/\/automations/.test(url)) {
      if (method === 'GET') return r.fulfill(json(rules));

      sent.push({ method, url, body: JSON.parse(r.request().postData() ?? '{}') });

      if (method === 'POST') {
        return r.fulfill(createResponse
          ? { status: createResponse.status, contentType: 'application/json', body: JSON.stringify(createResponse.body) }
          : json(RULE, 201));
      }
      if (method === 'DELETE') return r.fulfill({ status: 204, body: '' });
      return r.fulfill(json({}));
    }

    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

  // Se navega por dentro: `page.goto` recargaría y perdería el token, que vive en memoria.
  await page.getByRole('link', { name: 'Admin' }).click();
  await expect(page).toHaveURL(/\/admin/, { timeout: 15_000 });
  await page.getByRole('button', { name: 'Automatizaciones' }).click();

  return sent;
}

test('the list says what each rule does and how many times it ran', async ({ page }) => {
  await signIn(page, [RULE]);

  await expect(page.getByRole('cell', { name: 'Bajar al cerrar', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: /Estado es igual a Done/ })).toBeVisible();
  await expect(page.getByRole('cell', { name: '3', exact: true })).toBeVisible();
});

test('without automations it says so instead of showing an empty table', async ({ page }) => {
  await signIn(page, []);

  await expect(page.getByText('Todavía no hay automatizaciones.')).toBeVisible();
});

/**
 * Si la pantalla llevara su propia lista, se desincronizaría el día que se añada un disparador
 * y dejaría configurar algo que el servidor no entiende.
 */
test('the dropdowns are filled from the server vocabulary', async ({ page }) => {
  await signIn(page, []);
  await page.getByRole('button', { name: 'Nueva automatización' }).click();

  const when = page.getByLabel('Cuándo');
  await expect(when.locator('option')).toHaveText(['Se crea una tarea', 'Cambia el estado de una tarea']);
});

test('it does not save an automation without a name, and says why', async ({ page }) => {
  await signIn(page, []);
  await page.getByRole('button', { name: 'Nueva automatización' }).click();

  await expect(page.getByText('La automatización necesita un nombre')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Guardar' })).toBeDisabled();
});

test('create sends the rule exactly as configured', async ({ page }) => {
  const sent = await signIn(page, []);
  await page.getByRole('button', { name: 'Nueva automatización' }).click();

  await page.getByLabel('Nombre', { exact: true }).fill('Bajar al cerrar');
  await page.getByLabel('Cuándo').selectOption('TaskStatusChanged');
  await page.getByRole('button', { name: '+ Condición' }).click();
  await page.getByLabel('Campo', { exact: true }).selectOption('Status');
  await page.getByLabel('Operador', { exact: true }).selectOption('EqualTo');
  await page.getByLabel('Valor de la condición').fill('Done');
  await page.getByLabel('Acción', { exact: true }).selectOption('ChangePriority');
  await page.getByLabel('Valor de la acción').fill('Low');

  await page.getByRole('button', { name: 'Guardar' }).click();

  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0].body).toEqual({
    name: 'Bajar al cerrar',
    trigger: 'TaskStatusChanged',
    conditions: [{ field: 'Status', operator: 'EqualTo', value: 'Done' }],
    actions: [{ type: 'ChangePriority', value: 'Low' }],
  });
});

/** «Está vacío» no compara contra nada: pedir un valor sería pedir algo que se va a descartar. */
test('an operator that does not compare asks for no value', async ({ page }) => {
  await signIn(page, []);
  await page.getByRole('button', { name: 'Nueva automatización' }).click();
  await page.getByRole('button', { name: '+ Condición' }).click();

  await page.getByLabel('Operador', { exact: true }).selectOption('IsEmpty');

  await expect(page.getByLabel('Valor de la condición')).toHaveCount(0);
  await expect(page.getByText('sin valor')).toBeVisible();
});

test('if the server rejects the creation, the form stays open with what was typed', async ({ page }) => {
  await signIn(page, [], { status: 400, body: 'Ya hay una automatización con ese nombre' });
  await page.getByRole('button', { name: 'Nueva automatización' }).click();
  await page.getByLabel('Nombre', { exact: true }).fill('Repetida');
  await page.getByLabel('Valor de la acción').fill('Low');

  await page.getByRole('button', { name: 'Guardar' }).click();

  await expect(page.getByText('Ya hay una automatización con ese nombre').first()).toBeVisible();
  await expect(page.getByLabel('Nombre', { exact: true })).toHaveValue('Repetida');
});

test('turning a rule off tells the server', async ({ page }) => {
  const sent = await signIn(page, [RULE]);

  await page.getByRole('checkbox', { name: 'Bajar al cerrar' }).uncheck();

  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0].body).toEqual({ isActive: false });
});

test('deleting asks for confirmation in the row itself', async ({ page }) => {
  await signIn(page, [RULE]);

  await page.getByRole('button', { name: 'Borrar la automatización' }).click();

  await expect(page.getByText('¿Borrar la automatización?')).toBeVisible();
});
