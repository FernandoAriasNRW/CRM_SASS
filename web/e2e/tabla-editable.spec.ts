import { test, expect, type Page } from '@playwright/test';

/**
 * Edición en la propia tabla de tareas.
 *
 * Lo que cuidan estas pruebas es lo de siempre en este proyecto: **la pantalla no puede enseñar
 * un valor que el servidor no aceptó**. La celda se pinta antes de tener respuesta, así que un
 * rechazo tiene que devolverla a lo que había y decir por qué.
 *
 * Y una que se aprendió cara: hasta hace poco `PATCH /tasks/{id}` devolvía 200 sin guardar el
 * título, la descripción, las horas ni la fecha. Por eso aquí no basta con mirar el código de
 * estado —eso era exactamente lo que no veía el defecto—: se comprueba qué se mandó.
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

const TASK = {
  id: 'aaaaaaaa-0000-0000-0000-000000000001',
  title: 'Configurar alertas', description: '', status: 'To Do', priority: 'Normal',
  projectId: 'p1', assigneeId: null, estimatedHours: 8,
  dueDate: '2026-08-15T00:00:00', tagIds: [],
};

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

type Responses = { patch?: { status: number; body: unknown } };

/** Lo que se mandó en cada PATCH, para poder comprobar el cuerpo y no sólo el estado. */
type SentRequest = { url: string; body: Record<string, unknown> };

async function signIn(page: Page, responses: Responses = {}): Promise<SentRequest[]> {
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

    if (method === 'PATCH') {
      sent.push({ url, body: JSON.parse(r.request().postData() ?? '{}') });
      const response = responses.patch;
      return r.fulfill(response
        ? { status: response.status, contentType: 'application/json', body: JSON.stringify(response.body) }
        : json({}));
    }

    if (/\/tasks(\?|$)/.test(url)) return r.fulfill(json({ items: [TASK], totalCount: 1 }));

    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

  return sent;
}

/** Se navega por dentro: `page.goto` recargaría y el token, que vive en memoria, se perdería. */
async function goToList(page: Page) {
  await page.keyboard.press('Control+k');
  await page.keyboard.type('tareas');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tasks/, { timeout: 15_000 });

  await page.getByRole('button', { name: 'Lista', exact: true }).click();
  await expect(page.getByRole('cell', { name: 'Configurar alertas' })).toBeVisible({ timeout: 15_000 });
}

const hoursCell = (page: Page) => page.getByRole('row').filter({ hasText: 'Configurar alertas' }).getByRole('cell').nth(4);

test('sólo las columnas editables ofrecen editarse', async ({ page }) => {
  await signIn(page);
  await goToList(page);

  await expect(page.getByRole('button', { name: 'Editar Horas' })).toHaveCount(1);
  await expect(page.getByRole('button', { name: 'Editar Título' })).toHaveCount(1);
  // El responsable tiene su propio endpoint porque una tarea admite varios: no cabe en una celda.
  await expect(page.getByRole('button', { name: 'Editar Asignado' })).toHaveCount(0);
});

test('editar una celda manda sólo el campo que cambió', async ({ page }) => {
  const sent = await signIn(page);
  await goToList(page);

  await page.getByRole('button', { name: 'Editar Horas' }).click();
  await page.getByLabel('Horas', { exact: true }).fill('13.5');
  await page.getByLabel('Horas', { exact: true }).press('Tab');

  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0].body).toEqual({ estimatedHours: 13.5 });
  await expect(hoursCell(page)).toContainText('13.5');
});

test('el estado se edita con un desplegable de los estados que existen', async ({ page }) => {
  const sent = await signIn(page);
  await goToList(page);

  await page.getByRole('button', { name: 'Editar Estado' }).click();
  await expect(page.getByLabel('Estado', { exact: true })).toBeVisible();
  await page.getByLabel('Estado', { exact: true }).selectOption('In Progress');

  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0].body).toEqual({ status: 'In Progress' });
});

test('escapar no guarda nada', async ({ page }) => {
  const sent = await signIn(page);
  await goToList(page);

  await page.getByRole('button', { name: 'Editar Horas' }).click();
  await page.getByLabel('Horas', { exact: true }).fill('99');
  await page.getByLabel('Horas', { exact: true }).press('Escape');

  await expect(page.getByLabel('Horas', { exact: true })).toBeHidden();
  expect(sent).toEqual([]);
  await expect(hoursCell(page)).toContainText('8');
});

test('dejar el mismo valor no gasta una petición', async ({ page }) => {
  const sent = await signIn(page);
  await goToList(page);

  await page.getByRole('button', { name: 'Editar Horas' }).click();
  await page.getByLabel('Horas', { exact: true }).press('Tab');

  await expect(page.getByLabel('Horas', { exact: true })).toBeHidden();
  expect(sent).toEqual([]);
});

test('si el servidor rechaza, la celda vuelve a lo que había y dice por qué', async ({ page }) => {
  await signIn(page, {
    patch: { status: 400, body: 'Las horas estimadas no pueden ser negativas' },
  });
  await goToList(page);

  await page.getByRole('button', { name: 'Editar Horas' }).click();
  await page.getByLabel('Horas', { exact: true }).fill('-5');
  await page.getByLabel('Horas', { exact: true }).press('Tab');

  await expect(page.getByText('Las horas estimadas no pueden ser negativas').first()).toBeVisible();
  // Dejar el −5 en pantalla haría creer que la estimación quedó cambiada.
  await expect(hoursCell(page)).toContainText('8');
});

/**
 * Un fallo, un aviso. El interceptor de errores levanta el suyo por cada petición fallida, y
 * esta pantalla levanta el suyo con el nombre de la tarea que se revirtió: el mismo texto salía
 * dos veces. Ahora la petición se reserva explicarlo y el interceptor se calla.
 */
test('un solo fallo levanta un solo aviso', async ({ page }) => {
  await signIn(page, {
    patch: { status: 400, body: 'Las horas estimadas no pueden ser negativas' },
  });
  await goToList(page);

  await page.getByRole('button', { name: 'Editar Horas' }).click();
  await page.getByLabel('Horas', { exact: true }).fill('-5');
  await page.getByLabel('Horas', { exact: true }).press('Tab');

  await expect(page.getByText('Las horas estimadas no pueden ser negativas')).toHaveCount(1);
});

/**
 * El aviso tiene que traer la explicación del dominio, no la cadena de Angular «Http failure
 * response for http://localhost:8080/…», que enseña la dirección interna de la API y no dice
 * nada que quien la lee pueda usar.
 */
test('el aviso no enseña la dirección interna de la API', async ({ page }) => {
  await signIn(page, {
    patch: { status: 400, body: 'Las horas estimadas no pueden ser negativas' },
  });
  await goToList(page);

  await page.getByRole('button', { name: 'Editar Horas' }).click();
  await page.getByLabel('Horas', { exact: true }).fill('-5');
  await page.getByLabel('Horas', { exact: true }).press('Tab');

  await expect(page.getByText('Las horas estimadas no pueden ser negativas').first()).toBeVisible();
  await expect(page.getByText(/Http failure response/)).toHaveCount(0);
});
