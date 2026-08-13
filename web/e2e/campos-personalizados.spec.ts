import { test, expect, type Page } from '@playwright/test';

/**
 * Campos personalizados: la pestaña que los define y el formulario que los rellena.
 *
 * Lo que estas pruebas cuidan es que **la pantalla no enseñe nada que el servidor no haya
 * aceptado**. El formulario pinta el valor nuevo antes de tener respuesta, así que un rechazo
 * tiene que revertirlo y explicar por qué; y el alta de una definición tiene que quedarse
 * abierta con lo escrito si el servidor la rechaza, o se pierde el trabajo y el mensaje se queda
 * sin nada a lo que referirse.
 *
 * La API va simulada, como en el resto de la suite: así el rechazo se provoca a voluntad en
 * lugar de depender de qué haya sembrado en la base.
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

const DEFINICIONES = [
  {
    id: 'dddddddd-0000-0000-0000-000000000002', nombre: 'Canal de entrada', tipo: 'Seleccion',
    entidadDestino: 'Tarea', obligatorio: true, opciones: ['Web', 'Teléfono'], posicion: 5,
  },
  {
    id: 'dddddddd-0000-0000-0000-000000000001', nombre: 'Cliente facturable', tipo: 'Texto',
    entidadDestino: 'Tarea', obligatorio: false, opciones: [], posicion: 0,
  },
];

const TAREA = {
  id: 'aaaaaaaa-0000-0000-0000-000000000001',
  title: 'Tarea con campos', description: '', status: 'To Do', priority: 'Normal',
  projectId: 'p1', assigneeId: null, estimatedHours: 4,
  dueDate: new Date().toISOString(), tagIds: [],
};

const VALORES = [
  {
    definitionId: DEFINICIONES[1].id, nombre: 'Cliente facturable', tipo: 'Texto',
    obligatorio: false, opciones: [], posicion: 0, valor: 'Acme',
  },
  {
    definitionId: DEFINICIONES[0].id, nombre: 'Canal de entrada', tipo: 'Seleccion',
    obligatorio: true, opciones: ['Web', 'Teléfono'], posicion: 5, valor: null,
  },
];

const json = (cuerpo: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(cuerpo),
});

/**
 * Lo que el backend devuelve al rechazar es una cadena suelta —`BadRequest(result.Error)`—, no un
 * ProblemDetails. Las simulaciones lo imitan porque de ahí sale el mensaje que se enseña.
 */
type Respuestas = {
  definiciones?: unknown;
  valores?: unknown;
  alta?: { status: number; cuerpo: unknown };
  guardadoDeValor?: { status: number; cuerpo: unknown };
};

async function entrar(page: Page, respuestas: Respuestas = {}) {
  await page.route(/\/api\/v1\/auth\/login/, r => r.fulfill(json(SESION)));

  await page.route(/\/api\/v1\//, async r => {
    const url = r.request().url();
    const metodo = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fallback();

    // El rol sale de aquí, no del token: sin esto `isAdmin()` es falso, no hay enlace a
    // administración y el guard la deja fuera.
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USUARIO));

    // Estos dos devuelven un array, no un objeto paginado. Contestarles con `{items: []}` deja a
    // `UsersService` guardando un objeto donde espera una lista, y el `computed` que lo recorre
    // revienta en cada render: la aplicación entera se queda en blanco y el fallo no señala aquí.
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USUARIO]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));

    if (/\/custom-fields\/values\//.test(url)) {
      if (metodo === 'PUT') {
        const respuesta = respuestas.guardadoDeValor;
        return r.fulfill(respuesta
          ? { status: respuesta.status, contentType: 'application/json', body: JSON.stringify(respuesta.cuerpo) }
          : json({}));
      }
      return r.fulfill(json(respuestas.valores ?? VALORES));
    }

    if (/\/custom-fields/.test(url)) {
      if (metodo === 'POST') {
        const respuesta = respuestas.alta;
        return r.fulfill(respuesta
          ? { status: respuesta.status, contentType: 'application/json', body: JSON.stringify(respuesta.cuerpo) }
          : json(DEFINICIONES[1], 201));
      }
      if (metodo === 'DELETE') return r.fulfill({ status: 204, body: '' });
      if (metodo === 'PUT') return r.fulfill(json({}));
      return r.fulfill(json(respuestas.definiciones ?? DEFINICIONES));
    }

    if (/\/views\//.test(url)) return r.fulfill(json([]));

    // Lo que pide el panel de detalle. Cada uno tiene su forma, y equivocarla no da un error
    // legible: el `@for` de la plantilla recibe un objeto donde espera una lista, revienta el
    // ciclo de detección de cambios y **el resto del panel se queda a medio pintar**. El fallo
    // aparece entonces en el trozo que se estuviera probando, que no tiene nada que ver.
    if (/\/subtasks/.test(url)) return r.fulfill(json([]));
    if (/\/checklist/.test(url)) return r.fulfill(json([]));
    if (/\/comments/.test(url)) return r.fulfill(json([]));
    if (/\/dependencies/.test(url)) return r.fulfill(json({ bloqueadaPor: [], bloqueaA: [] }));

    if (/\/tasks(\?|$)/.test(url)) return r.fulfill(json({ items: [TAREA], totalCount: 1 }));

    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });
}

/**
 * Se navega por dentro de la aplicación, nunca con `page.goto`.
 *
 * El token vive en memoria y no en `localStorage` —decisión de seguridad, §5 de CONTINUACION—,
 * así que una recarga devuelve al login y la prueba falla por un motivo que no tiene nada que ver
 * con lo que quiere comprobar.
 */
async function irALaPestanaDeCampos(page: Page) {
  await page.getByRole('link', { name: 'Admin' }).click();
  await expect(page).toHaveURL(/\/admin/, { timeout: 15_000 });
  await page.getByRole('button', { name: 'Campos Personalizados' }).click();
}

async function irALasTareas(page: Page) {
  await page.keyboard.press('Control+k');
  await page.keyboard.type('tareas');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tasks/, { timeout: 15_000 });
}

test.describe('la pestaña que define los campos', () => {
  test('los ordena por posición, no por el orden en que lleguen', async ({ page }) => {
    await entrar(page);
    await irALaPestanaDeCampos(page);

    const nombres = page.locator('tbody tr td:nth-child(2)');
    await expect(nombres).toHaveText(['Cliente facturable', 'Canal de entrada']);
  });

  test('sin campos definidos lo dice en lugar de enseñar una tabla vacía', async ({ page }) => {
    await entrar(page, { definiciones: [] });
    await irALaPestanaDeCampos(page);

    await expect(page.getByText(/todavía no hay campos definidos/i)).toBeVisible();
  });

  test('no deja guardar un campo sin nombre, y dice por qué', async ({ page }) => {
    await entrar(page);
    await irALaPestanaDeCampos(page);
    await page.getByRole('button', { name: 'Nuevo campo', exact: true }).click();

    await expect(page.getByText('El campo necesita un nombre')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Guardar' })).toBeDisabled();
  });

  test('un tipo de selección pide sus opciones', async ({ page }) => {
    await entrar(page);
    await irALaPestanaDeCampos(page);
    await page.getByRole('button', { name: 'Nuevo campo', exact: true }).click();

    await expect(page.getByLabel(/opciones, una por línea/i)).toBeHidden();

    await page.getByLabel('Tipo').selectOption('Seleccion');

    await expect(page.getByLabel(/opciones, una por línea/i)).toBeVisible();
  });

  test('si el servidor rechaza el alta, el formulario sigue abierto con lo escrito', async ({ page }) => {
    await entrar(page, {
      alta: { status: 400, cuerpo: 'Ya hay un campo con ese nombre para esa entidad' },
    });
    await irALaPestanaDeCampos(page);
    await page.getByRole('button', { name: 'Nuevo campo', exact: true }).click();
    await page.getByLabel('Nombre').fill('Cliente facturable');

    await page.getByRole('button', { name: 'Guardar' }).click();

    // `.first()`: el mensaje sale dos veces, en el formulario y en el aviso que levanta el
    // interceptor de errores. Que aparezca al menos una vez es lo que importa aquí.
    await expect(page.getByText('Ya hay un campo con ese nombre para esa entidad').first()).toBeVisible();
    await expect(page.getByLabel('Nombre')).toHaveValue('Cliente facturable');
  });

  test('al editar no se puede cambiar el tipo, y se explica', async ({ page }) => {
    await entrar(page);
    await irALaPestanaDeCampos(page);

    await page.getByRole('button', { name: 'Editar el campo' }).first().click();

    await expect(page.getByLabel('Tipo')).toBeDisabled();
    await expect(page.getByText(/el tipo no se puede cambiar/i)).toBeVisible();
  });

  test('borrar pide confirmación en la propia fila', async ({ page }) => {
    await entrar(page);
    await irALaPestanaDeCampos(page);

    await page.getByRole('button', { name: 'Borrar el campo' }).first().click();

    await expect(page.getByText(/¿borrar el campo y todos sus valores\?/i)).toBeVisible();
  });
});

test.describe('el formulario del detalle de tarea', () => {
  async function abrirLaTarea(page: Page) {
    await irALasTareas(page);
    await page.getByText('Tarea con campos').first().click();
    await expect(page.getByText('Campos personalizados')).toBeVisible({ timeout: 15_000 });
  }

  test('pinta cada campo con su valor', async ({ page }) => {
    await entrar(page);
    await abrirLaTarea(page);

    await expect(page.getByLabel('Cliente facturable')).toHaveValue('Acme');
    await expect(page.getByLabel('Canal de entrada')).toBeVisible();
  });

  test('un valor rechazado se revierte y el motivo sale junto al campo', async ({ page }) => {
    await entrar(page, {
      guardadoDeValor: { status: 400, cuerpo: '«Paloma mensajera» no está entre las opciones del campo' },
    });
    await abrirLaTarea(page);

    await page.getByLabel('Cliente facturable').fill('Globex');
    await page.getByLabel('Cliente facturable').blur();

    await expect(page.getByText('«Paloma mensajera» no está entre las opciones del campo').first()).toBeVisible();
    // Dejar «Globex» en pantalla sería enseñar algo que el servidor no guardó.
    await expect(page.getByLabel('Cliente facturable')).toHaveValue('Acme');
  });

  test('un inquilino sin campos definidos no ve ni el encabezado', async ({ page }) => {
    await entrar(page, { valores: [] });

    await irALasTareas(page);
    await page.getByText('Tarea con campos').first().click();
    // Se espera a que el panel esté pintado antes de comprobar una ausencia: si no, la prueba
    // pasaría simplemente porque todavía no había llegado nada.
    await expect(page.getByText('Descripción').first()).toBeVisible({ timeout: 15_000 });

    await expect(page.getByText('Campos personalizados')).toBeHidden();
  });
});
