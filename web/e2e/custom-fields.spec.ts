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

const DEFINITIONS = [
  {
    id: 'dddddddd-0000-0000-0000-000000000002', name: 'Canal de entrada', type: 'Select',
    targetEntity: 'Task', isRequired: true, options: ['Web', 'Teléfono'], position: 5,
  },
  {
    id: 'dddddddd-0000-0000-0000-000000000001', name: 'Cliente facturable', type: 'Text',
    targetEntity: 'Task', isRequired: false, options: [], position: 0,
  },
];

const TASK = {
  id: 'aaaaaaaa-0000-0000-0000-000000000001',
  title: 'Tarea con campos', description: '', status: 'To Do', priority: 'Normal',
  projectId: 'p1', assigneeId: null, estimatedHours: 4,
  dueDate: new Date().toISOString(), tagIds: [],
};

const VALUES = [
  {
    definitionId: DEFINITIONS[1].id, name: 'Cliente facturable', type: 'Text',
    isRequired: false, options: [], position: 0, value: 'Acme',
  },
  {
    definitionId: DEFINITIONS[0].id, name: 'Canal de entrada', type: 'Select',
    isRequired: true, options: ['Web', 'Teléfono'], position: 5, value: null,
  },
];

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

/**
 * Lo que el backend devuelve al rechazar es una cadena suelta —`BadRequest(result.Error)`—, no un
 * ProblemDetails. Las simulaciones lo imitan porque de ahí sale el mensaje que se enseña.
 */
type Responses = {
  definitions?: unknown;
  values?: unknown;
  created?: { status: number; body: unknown };
  savedValue?: { status: number; body: unknown };
};

async function signIn(page: Page, responses: Responses = {}) {
  await page.route(/\/api\/v1\/auth\/login/, r => r.fulfill(json(SESSION)));

  await page.route(/\/api\/v1\//, async r => {
    const url = r.request().url();
    const method = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fallback();

    // El rol sale de aquí, no del token: sin esto `isAdmin()` es falso, no hay enlace a
    // administración y el guard la deja fuera.
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USER));

    // Estos dos devuelven un array, no un objeto paginado. Contestarles con `{items: []}` deja a
    // `UsersService` guardando un objeto donde espera una lista, y el `computed` que lo recorre
    // revienta en cada render: la aplicación entera se queda en blanco y el fallo no señala aquí.
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USER]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));

    if (/\/custom-fields\/values\//.test(url)) {
      if (method === 'PUT') {
        const response = responses.savedValue;
        return r.fulfill(response
          ? { status: response.status, contentType: 'application/json', body: JSON.stringify(response.body) }
          : json({}));
      }
      return r.fulfill(json(responses.values ?? VALUES));
    }

    if (/\/custom-fields/.test(url)) {
      if (method === 'POST') {
        const response = responses.created;
        return r.fulfill(response
          ? { status: response.status, contentType: 'application/json', body: JSON.stringify(response.body) }
          : json(DEFINITIONS[1], 201));
      }
      if (method === 'DELETE') return r.fulfill({ status: 204, body: '' });
      if (method === 'PUT') return r.fulfill(json({}));
      return r.fulfill(json(responses.definitions ?? DEFINITIONS));
    }

    if (/\/views\//.test(url)) return r.fulfill(json([]));

    // Lo que pide el panel de detalle. Cada uno tiene su forma, y equivocarla no da un error
    // legible: el `@for` de la plantilla recibe un objeto donde espera una lista, revienta el
    // ciclo de detección de cambios y **el resto del panel se queda a medio pintar**. El fallo
    // aparece entonces en el trozo que se estuviera probando, que no tiene nada que ver.
    if (/\/subtasks/.test(url)) return r.fulfill(json([]));
    if (/\/checklist/.test(url)) return r.fulfill(json([]));
    if (/\/comments/.test(url)) return r.fulfill(json([]));
    if (/\/dependencies/.test(url)) return r.fulfill(json({ blockedBy: [], blocks: [] }));
    // El campo de etiquetas de la ficha pide la lista de la organización: un array, no paginado.
    if (/\/tags(\?|$)/.test(url)) return r.fulfill(json([]));

    if (/\/tasks(\?|$)/.test(url)) return r.fulfill(json({ items: [TASK], totalCount: 1 }));

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
async function goToFieldsTab(page: Page) {
  await page.getByRole('link', { name: 'Admin' }).click();
  await expect(page).toHaveURL(/\/admin/, { timeout: 15_000 });
  await page.getByRole('button', { name: 'Campos Personalizados' }).click();
}

async function goToTasks(page: Page) {
  await page.keyboard.press('Control+k');
  await page.keyboard.type('tareas');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tasks/, { timeout: 15_000 });
}

test.describe('the tab that defines the fields', () => {
  test('sorts them by position, not by arrival order', async ({ page }) => {
    await signIn(page);
    await goToFieldsTab(page);

    const names = page.locator('tbody tr td:nth-child(2)');
    await expect(names).toHaveText(['Cliente facturable', 'Canal de entrada']);
  });

  test('without defined fields it says so instead of showing an empty table', async ({ page }) => {
    await signIn(page, { definitions: [] });
    await goToFieldsTab(page);

    await expect(page.getByText(/todavía no hay campos definidos/i)).toBeVisible();
  });

  test('it does not save a field without a name, and says why', async ({ page }) => {
    await signIn(page);
    await goToFieldsTab(page);
    await page.getByRole('button', { name: 'Nuevo campo', exact: true }).click();

    await expect(page.getByText('El campo necesita un nombre')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Guardar' })).toBeDisabled();
  });

  test('a select type asks for its options', async ({ page }) => {
    await signIn(page);
    await goToFieldsTab(page);
    await page.getByRole('button', { name: 'Nuevo campo', exact: true }).click();

    await expect(page.getByLabel(/opciones, una por línea/i)).toBeHidden();

    await page.getByLabel('Tipo').selectOption('Select');

    await expect(page.getByLabel(/opciones, una por línea/i)).toBeVisible();
  });

  test('if the server rejects the creation, the form stays open with what was typed', async ({ page }) => {
    await signIn(page, {
      created: { status: 400, body: 'Ya hay un campo con ese nombre para esa entidad' },
    });
    await goToFieldsTab(page);
    await page.getByRole('button', { name: 'Nuevo campo', exact: true }).click();
    await page.getByLabel('Nombre').fill('Cliente facturable');

    await page.getByRole('button', { name: 'Guardar' }).click();

    // `.first()`: el mensaje sale dos veces, en el formulario y en el aviso que levanta el
    // interceptor de errores. Que aparezca al menos una vez es lo que importa aquí.
    await expect(page.getByText('Ya hay un campo con ese nombre para esa entidad').first()).toBeVisible();
    await expect(page.getByLabel('Nombre')).toHaveValue('Cliente facturable');
  });

  test('the type cannot be changed when editing, and it explains why', async ({ page }) => {
    await signIn(page);
    await goToFieldsTab(page);

    await page.getByRole('button', { name: 'Editar el campo' }).first().click();

    await expect(page.getByLabel('Tipo')).toBeDisabled();
    await expect(page.getByText(/el tipo no se puede cambiar/i)).toBeVisible();
  });

  test('deleting asks for confirmation in the row itself', async ({ page }) => {
    await signIn(page);
    await goToFieldsTab(page);

    await page.getByRole('button', { name: 'Borrar el campo' }).first().click();

    await expect(page.getByText(/¿borrar el campo y todos sus valores\?/i)).toBeVisible();
  });
});

test.describe('the task detail form', () => {
  async function openTask(page: Page) {
    await goToTasks(page);
    await page.getByText('Tarea con campos').first().click();
    await expect(page.getByText('Campos personalizados')).toBeVisible({ timeout: 15_000 });
  }

  test('renders each field with its value', async ({ page }) => {
    await signIn(page);
    await openTask(page);

    await expect(page.getByLabel('Cliente facturable')).toHaveValue('Acme');
    await expect(page.getByLabel('Canal de entrada')).toBeVisible();
  });

  test('a rejected value reverts and the reason shows next to the field', async ({ page }) => {
    await signIn(page, {
      savedValue: { status: 400, body: '«Paloma mensajera» no está entre las opciones del campo' },
    });
    await openTask(page);

    await page.getByLabel('Cliente facturable').fill('Globex');
    await page.getByLabel('Cliente facturable').blur();

    await expect(page.getByText('«Paloma mensajera» no está entre las opciones del campo').first()).toBeVisible();
    // Dejar «Globex» en pantalla sería enseñar algo que el servidor no guardó.
    await expect(page.getByLabel('Cliente facturable')).toHaveValue('Acme');
  });

  test('a tenant without defined fields does not even see the heading', async ({ page }) => {
    await signIn(page, { values: [] });

    await goToTasks(page);
    await page.getByText('Tarea con campos').first().click();
    // Se espera a que el panel esté pintado antes de comprobar una ausencia: si no, la prueba
    // pasaría simplemente porque todavía no había llegado nada.
    await expect(page.getByText('Descripción').first()).toBeVisible({ timeout: 15_000 });

    await expect(page.getByText('Campos personalizados')).toBeHidden();
  });
});
