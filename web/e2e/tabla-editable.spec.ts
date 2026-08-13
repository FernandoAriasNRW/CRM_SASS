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

const USUARIO = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const SESION = {
  accessToken: 'token-de-prueba',
  accessTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  refreshToken: 'refresco-de-prueba',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 864e5).toISOString(),
  user: USUARIO,
};

const TAREA = {
  id: 'aaaaaaaa-0000-0000-0000-000000000001',
  title: 'Configurar alertas', description: '', status: 'To Do', priority: 'Normal',
  projectId: 'p1', assigneeId: null, estimatedHours: 8,
  dueDate: '2026-08-15T00:00:00', tagIds: [],
};

const json = (cuerpo: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(cuerpo),
});

type Respuestas = { patch?: { status: number; cuerpo: unknown } };

/** Lo que se mandó en cada PATCH, para poder comprobar el cuerpo y no sólo el estado. */
type Enviado = { url: string; cuerpo: Record<string, unknown> };

async function entrar(page: Page, respuestas: Respuestas = {}): Promise<Enviado[]> {
  const enviados: Enviado[] = [];

  await page.route(/\/api\/v1\/auth\/login/, r => r.fulfill(json(SESION)));

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    const metodo = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fallback();
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USUARIO));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USUARIO]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/views\//.test(url)) return r.fulfill(json([]));
    if (/\/custom-fields/.test(url)) return r.fulfill(json([]));

    if (metodo === 'PATCH') {
      enviados.push({ url, cuerpo: JSON.parse(r.request().postData() ?? '{}') });
      const respuesta = respuestas.patch;
      return r.fulfill(respuesta
        ? { status: respuesta.status, contentType: 'application/json', body: JSON.stringify(respuesta.cuerpo) }
        : json({}));
    }

    if (/\/tasks(\?|$)/.test(url)) return r.fulfill(json({ items: [TAREA], totalCount: 1 }));

    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

  return enviados;
}

/** Se navega por dentro: `page.goto` recargaría y el token, que vive en memoria, se perdería. */
async function irALaLista(page: Page) {
  await page.keyboard.press('Control+k');
  await page.keyboard.type('tareas');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tasks/, { timeout: 15_000 });

  await page.getByRole('button', { name: 'Lista', exact: true }).click();
  await expect(page.getByRole('cell', { name: 'Configurar alertas' })).toBeVisible({ timeout: 15_000 });
}

const celdaDeHoras = (page: Page) => page.getByRole('row').filter({ hasText: 'Configurar alertas' }).getByRole('cell').nth(4);

test('sólo las columnas editables ofrecen editarse', async ({ page }) => {
  await entrar(page);
  await irALaLista(page);

  await expect(page.getByRole('button', { name: 'Editar Hours' })).toHaveCount(1);
  await expect(page.getByRole('button', { name: 'Editar Title' })).toHaveCount(1);
  // El responsable tiene su propio endpoint porque una tarea admite varios: no cabe en una celda.
  await expect(page.getByRole('button', { name: 'Editar Asignado' })).toHaveCount(0);
});

test('editar una celda manda sólo el campo que cambió', async ({ page }) => {
  const enviados = await entrar(page);
  await irALaLista(page);

  await page.getByRole('button', { name: 'Editar Hours' }).click();
  await page.getByLabel('Hours', { exact: true }).fill('13.5');
  await page.getByLabel('Hours', { exact: true }).press('Tab');

  await expect.poll(() => enviados.length).toBe(1);
  expect(enviados[0].cuerpo).toEqual({ estimatedHours: 13.5 });
  await expect(celdaDeHoras(page)).toContainText('13.5');
});

test('el estado se edita con un desplegable de los estados que existen', async ({ page }) => {
  const enviados = await entrar(page);
  await irALaLista(page);

  await page.getByRole('button', { name: 'Editar Status' }).click();
  await expect(page.getByLabel('Status', { exact: true })).toBeVisible();
  await page.getByLabel('Status', { exact: true }).selectOption('In Progress');

  await expect.poll(() => enviados.length).toBe(1);
  expect(enviados[0].cuerpo).toEqual({ status: 'In Progress' });
});

test('escapar no guarda nada', async ({ page }) => {
  const enviados = await entrar(page);
  await irALaLista(page);

  await page.getByRole('button', { name: 'Editar Hours' }).click();
  await page.getByLabel('Hours', { exact: true }).fill('99');
  await page.getByLabel('Hours', { exact: true }).press('Escape');

  await expect(page.getByLabel('Hours', { exact: true })).toBeHidden();
  expect(enviados).toEqual([]);
  await expect(celdaDeHoras(page)).toContainText('8');
});

test('dejar el mismo valor no gasta una petición', async ({ page }) => {
  const enviados = await entrar(page);
  await irALaLista(page);

  await page.getByRole('button', { name: 'Editar Hours' }).click();
  await page.getByLabel('Hours', { exact: true }).press('Tab');

  await expect(page.getByLabel('Hours', { exact: true })).toBeHidden();
  expect(enviados).toEqual([]);
});

test('si el servidor rechaza, la celda vuelve a lo que había y dice por qué', async ({ page }) => {
  await entrar(page, {
    patch: { status: 400, cuerpo: 'Las horas estimadas no pueden ser negativas' },
  });
  await irALaLista(page);

  await page.getByRole('button', { name: 'Editar Hours' }).click();
  await page.getByLabel('Hours', { exact: true }).fill('-5');
  await page.getByLabel('Hours', { exact: true }).press('Tab');

  await expect(page.getByText('Las horas estimadas no pueden ser negativas').first()).toBeVisible();
  // Dejar el −5 en pantalla haría creer que la estimación quedó cambiada.
  await expect(celdaDeHoras(page)).toContainText('8');
});

/**
 * El aviso tiene que traer la explicación del dominio, no la cadena de Angular «Http failure
 * response for http://localhost:8080/…», que enseña la dirección interna de la API y no dice
 * nada que quien la lee pueda usar.
 */
test('el aviso no enseña la dirección interna de la API', async ({ page }) => {
  await entrar(page, {
    patch: { status: 400, cuerpo: 'Las horas estimadas no pueden ser negativas' },
  });
  await irALaLista(page);

  await page.getByRole('button', { name: 'Editar Hours' }).click();
  await page.getByLabel('Hours', { exact: true }).fill('-5');
  await page.getByLabel('Hours', { exact: true }).press('Tab');

  await expect(page.getByText('Las horas estimadas no pueden ser negativas').first()).toBeVisible();
  await expect(page.getByText(/Http failure response/)).toHaveCount(0);
});
