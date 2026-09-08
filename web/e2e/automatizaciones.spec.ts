import { test, expect, type Page } from '@playwright/test';

/**
 * La pantalla de automatizaciones.
 *
 * Lo que se comprueba aquí es que **el formulario se construye con el vocabulario que sirve el
 * servidor**: si esta pantalla llevara su propia lista de disparadores, se desincronizaría el día
 * que se añada uno y dejaría configurar algo que el servidor no entiende.
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

const VOCABULARIO = {
  disparadores: ['TareaCreada', 'TareaCambiaDeEstado'],
  campos: ['Estado', 'ResponsableId'],
  operadores: ['Igual', 'EstaVacio'],
  acciones: ['CambiarEstado', 'CambiarPrioridad'],
};

const REGLA = {
  id: 'r1', nombre: 'Bajar al cerrar', disparador: 'TareaCambiaDeEstado', activa: true,
  condiciones: [{ campo: 'Estado', operador: 'Igual', valor: 'Done' }],
  acciones: [{ tipo: 'CambiarPrioridad', valor: 'Low' }],
  vecesEjecutada: 3, ultimaEjecucionUtc: '2026-08-14T10:00:00Z',
};

const json = (cuerpo: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(cuerpo),
});

type Enviado = { metodo: string; url: string; cuerpo: Record<string, unknown> };

async function entrar(
  page: Page,
  reglas: unknown[] = [],
  respuestaAlCrear?: { status: number; cuerpo: unknown },
): Promise<Enviado[]> {
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

    if (/\/automations\/vocabulario/.test(url)) return r.fulfill(json(VOCABULARIO));

    if (/\/automations/.test(url)) {
      if (metodo === 'GET') return r.fulfill(json(reglas));

      enviados.push({ metodo, url, cuerpo: JSON.parse(r.request().postData() ?? '{}') });

      if (metodo === 'POST') {
        return r.fulfill(respuestaAlCrear
          ? { status: respuestaAlCrear.status, contentType: 'application/json', body: JSON.stringify(respuestaAlCrear.cuerpo) }
          : json(REGLA, 201));
      }
      if (metodo === 'DELETE') return r.fulfill({ status: 204, body: '' });
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

  return enviados;
}

test('la lista dice qué hace cada regla y cuántas veces se ha ejecutado', async ({ page }) => {
  await entrar(page, [REGLA]);

  await expect(page.getByRole('cell', { name: 'Bajar al cerrar', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: /Estado Igual Done/ })).toBeVisible();
  await expect(page.getByRole('cell', { name: '3', exact: true })).toBeVisible();
});

test('sin automatizaciones lo dice, en lugar de enseñar una tabla vacía', async ({ page }) => {
  await entrar(page, []);

  await expect(page.getByText('Todavía no hay automatizaciones.')).toBeVisible();
});

/**
 * Si la pantalla llevara su propia lista, se desincronizaría el día que se añada un disparador
 * y dejaría configurar algo que el servidor no entiende.
 */
test('los desplegables se llenan con el vocabulario del servidor', async ({ page }) => {
  await entrar(page, []);
  await page.getByRole('button', { name: 'Nueva automatización' }).click();

  const cuando = page.getByLabel('Cuándo');
  await expect(cuando.locator('option')).toHaveText(['TareaCreada', 'TareaCambiaDeEstado']);
});

test('no deja guardar una automatización sin nombre, y dice por qué', async ({ page }) => {
  await entrar(page, []);
  await page.getByRole('button', { name: 'Nueva automatización' }).click();

  await expect(page.getByText('La automatización necesita un nombre')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Guardar' })).toBeDisabled();
});

test('crear manda la regla tal y como se configuró', async ({ page }) => {
  const enviados = await entrar(page, []);
  await page.getByRole('button', { name: 'Nueva automatización' }).click();

  await page.getByLabel('Nombre', { exact: true }).fill('Bajar al cerrar');
  await page.getByLabel('Cuándo').selectOption('TareaCambiaDeEstado');
  await page.getByRole('button', { name: '+ Condición' }).click();
  await page.getByLabel('Campo', { exact: true }).selectOption('Estado');
  await page.getByLabel('Operador', { exact: true }).selectOption('Igual');
  await page.getByLabel('Valor de la condición').fill('Done');
  await page.getByLabel('Acción', { exact: true }).selectOption('CambiarPrioridad');
  await page.getByLabel('Valor de la acción').fill('Low');

  await page.getByRole('button', { name: 'Guardar' }).click();

  await expect.poll(() => enviados.length).toBe(1);
  expect(enviados[0].cuerpo).toEqual({
    nombre: 'Bajar al cerrar',
    disparador: 'TareaCambiaDeEstado',
    condiciones: [{ campo: 'Estado', operador: 'Igual', valor: 'Done' }],
    acciones: [{ tipo: 'CambiarPrioridad', valor: 'Low' }],
  });
});

/** «Está vacío» no compara contra nada: pedir un valor sería pedir algo que se va a descartar. */
test('un operador que no compara no pide valor', async ({ page }) => {
  await entrar(page, []);
  await page.getByRole('button', { name: 'Nueva automatización' }).click();
  await page.getByRole('button', { name: '+ Condición' }).click();

  await page.getByLabel('Operador', { exact: true }).selectOption('EstaVacio');

  await expect(page.getByLabel('Valor de la condición')).toHaveCount(0);
  await expect(page.getByText('sin valor')).toBeVisible();
});

test('si el servidor rechaza el alta, el formulario sigue abierto con lo escrito', async ({ page }) => {
  await entrar(page, [], { status: 400, cuerpo: 'Ya hay una automatización con ese nombre' });
  await page.getByRole('button', { name: 'Nueva automatización' }).click();
  await page.getByLabel('Nombre', { exact: true }).fill('Repetida');
  await page.getByLabel('Valor de la acción').fill('Low');

  await page.getByRole('button', { name: 'Guardar' }).click();

  await expect(page.getByText('Ya hay una automatización con ese nombre').first()).toBeVisible();
  await expect(page.getByLabel('Nombre', { exact: true })).toHaveValue('Repetida');
});

test('apagar una regla avisa al servidor', async ({ page }) => {
  const enviados = await entrar(page, [REGLA]);

  await page.getByRole('checkbox', { name: 'Bajar al cerrar' }).uncheck();

  await expect.poll(() => enviados.length).toBe(1);
  expect(enviados[0].cuerpo).toEqual({ activa: false });
});

test('borrar pide confirmación en la propia fila', async ({ page }) => {
  await entrar(page, [REGLA]);

  await page.getByRole('button', { name: 'Borrar la automatización' }).click();

  await expect(page.getByText('¿Borrar la automatización?')).toBeVisible();
});
