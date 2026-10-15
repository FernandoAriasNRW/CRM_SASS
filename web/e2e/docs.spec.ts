import { test, expect, type Page } from '@playwright/test';
import { createSession, shellResponse, submitLogin } from './support/auth';

/**
 * Humo de la sección de documentos.
 *
 * Es la feature más grande del frontend —705 líneas de plantilla y 625 de componente— y
 * no tenía ninguna cobertura.
 *
 * El detalle que costó acertar: `GET /api/v1/docs` devuelve un **array plano**, no el
 * objeto `{items, totalCount}` que usan proyectos, tareas y tickets. Simular la forma
 * equivocada dejaba la vista vacía sin ningún error, que es la clase de fallo que hace
 * perder una tarde.
 */

const SESSION = createSession({ id: '1', name: 'Admin', email: 'admin@acme.com', role: 'Admin', tenantId: 'ff' });

/** Ajustado a `DocumentDto`: `type` es numérico (1 List, 2 Wiki, 3 MeetingNote, 4 Template). */
const DOCUMENTS = [
  {
    id: '00000000-0000-0000-0000-0000000000d1',
    title: 'Manual de arquitectura',
    description: 'Cómo está montado el sistema',
    type: 1,
    ownerId: '00000000-0000-0000-0000-000000000001',
    createdAtUtc: new Date().toISOString(),
    updatedAtUtc: new Date().toISOString(),
  },
];

async function openDocs(page: Page) {
  await page.route(/\/api\/v1\/auth\/login/, r =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(SESSION) }));

  await page.route(/\/api\/v1\//, r => {
    const u = r.request().url();
    if (/\/auth\/login/.test(u)) return r.fallback();
    const shell = shellResponse(u, SESSION);
    if (shell !== undefined) return r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(shell) });
    if (/\/views\//.test(u)) {
      return r.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }
    // El contador de plantillas también es un array plano, y va antes que la regla de `/docs`
    // porque esa no casa con las rutas de debajo.
    if (/\/docs\/plantillas\/usos/.test(u)) {
      return r.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }
    // Array plano: es lo que devuelve este módulo, a diferencia del resto.
    if (/\/docs(\?|$)/.test(u)) {
      return r.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify(DOCUMENTS),
      });
    }
    return r.fulfill({ status: 200, contentType: 'application/json', body: '{"items":[],"totalCount":0}' });
  });

  await submitLogin(page);

  await page.keyboard.press('Control+k');
  await page.keyboard.type('docs');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/docs/, { timeout: 30_000 });
}

test('the documents section loads without console errors', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', e => errors.push(e.message));

  await openDocs(page);

  // Un fallo al construir cualquiera de sus piezas aparecería aquí antes que en pantalla.
  expect(errors).toEqual([]);
});



test('shows the documents the API returns', async ({ page }) => {
  await openDocs(page);

  await expect(page.getByText('Manual de arquitectura').first()).toBeVisible({ timeout: 15_000 });
});

test('the import modal opens, validates and closes', async ({ page }) => {
  await openDocs(page);

  await page.getByRole('button', { name: /^importar$/i }).first().click();
  const modal = page.getByRole('dialog', { name: /importar documento/i });
  await expect(modal).toBeVisible();

  // Importar sin contenido no debe crear nada: el aviso sale dentro del modal, no en un
  // `alert` que bloquea la página y no dice qué falta.
  await modal.getByRole('button', { name: /^importar$/i }).click();
  await expect(modal.getByRole('alert')).toBeVisible();
  await expect(modal).toBeVisible();

  await modal.getByRole('button', { name: /^cancelar$/i }).click();
  await expect(modal).toBeHidden();
});

test('the template drawer offers them all, filters them and is keyboard navigable', async ({ page }) => {
  await openDocs(page);

  await page.getByRole('button', { name: /más opciones de documento nuevo/i }).click();
  await page.getByRole('button', { name: /ver todas las plantillas/i }).first().click();

  const cajon = page.getByRole('dialog', { name: /^plantillas$/i });
  await expect(cajon).toBeVisible();

  // La galería sólo enseña cuatro; el cajón tiene que enseñar también las del equipo, que es
  // justamente lo que no cabía fuera.
  await expect(cajon.getByRole('heading', { name: /del sistema/i })).toBeVisible();

  // Son <button> nativos, así que reciben foco sin ayuda añadida.
  const first = cajon.getByRole('button', { name: /resumen de proyecto/i });
  await first.focus();
  await expect(first).toBeFocused();

  // Filtrar deja sólo la que coincide. Sin esto, el buscador podría no estar conectado a nada y
  // el cajón seguiría pareciendo correcto.
  await cajon.getByRole('searchbox', { name: /buscar plantilla/i }).fill('wiki');
  await expect(cajon.getByRole('button', { name: /^wiki/i })).toBeVisible();
  await expect(cajon.getByRole('button', { name: /resumen de proyecto/i })).toBeHidden();
});

/**
 * El fallo más grave que tenía el editor: el guardado se suscribía **sin manejador de error**, y
 * la cabecera decía «Saved just now» —una cadena escrita a mano— pasara lo que pasara. Se podía
 * escribir media hora con la sesión caducada y perderlo entero al recargar.
 *
 * La prueba fuerza el fallo del servidor. Una que sólo comprobara el camino bueno la pasaba
 * también la versión rota.
 */
test('if saving fails, the header says so and offers to retry', async ({ page }) => {
  const PAGE = {
    id: '00000000-0000-0000-0000-0000000000a1',
    documentId: DOCUMENTS[0].id,
    parentPageId: null,
    title: 'Página de prueba',
    content: '<p>Contenido</p>',
    order: 0,
  };

  let saveRequests = 0;

  await openDocs(page);

  // Después de `entrarADocs` a propósito: en Playwright gana la ruta registrada más tarde, y la
  // de dentro es un comodín sobre `/api/v1/` que si no se tragaría éstas.
  await page.route(/\/api\/v1\/docs\/[^/]+\/pages/, r =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([PAGE]) }));

  await page.route(/\/api\/v1\/docs\/pages\//, r => {
    saveRequests++;
    // Falla siempre: lo que se comprueba es que el fallo llega a la pantalla, no que se recupere.
    return r.fulfill({ status: 500, contentType: 'application/json', body: '"Error del servidor"' });
  });

  await page.getByText('Manual de arquitectura').first().click();
  await expect(page.getByRole('textbox', { name: /título del documento/i })).toBeVisible({ timeout: 15_000 });

  await page.locator('.ProseMirror').click();
  await page.keyboard.type('esto no se va a poder guardar');

  const reintentar = page.getByRole('button', { name: /no se guardó\. reintentar/i });
  await expect(reintentar).toBeVisible({ timeout: 15_000 });

  // Y el texto sigue en pantalla: perderlo al fallar sería el mismo desastre con otro cartel.
  await expect(page.locator('.ProseMirror')).toContainText('esto no se va a poder guardar');

  const before = saveRequests;
  await reintentar.click();
  await expect.poll(() => saveRequests).toBeGreaterThan(before);
});

/**
 * El árbol de páginas no existía: `POST /docs/{id}/pages` estaba publicado y la plantilla no lo
 * llamaba desde ningún sitio, así que cada documento se quedaba con la página que le creó la
 * plantilla y no había forma de organizarlo.
 */
test('the tree allows creating a new page', async ({ page }) => {
  const PAGE = {
    id: '00000000-0000-0000-0000-0000000000a1',
    documentId: DOCUMENTS[0].id,
    parentPageId: null,
    title: 'Primera página',
    content: '<p>Contenido</p>',
    order: 0,
  };

  let creations = 0;

  await openDocs(page);

  await page.route(/\/api\/v1\/docs\/[^/]+\/pages/, r => {
    if (r.request().method() === 'POST') {
      creations++;
      return r.fulfill({ status: 200, contentType: 'application/json', body: '"00000000-0000-0000-0000-0000000000a2"' });
    }
    return r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([PAGE]) });
  });

  await page.getByText('Manual de arquitectura').first().click();

  const tree = page.getByRole('button', { name: /^nueva página$/i });
  await expect(tree).toBeVisible({ timeout: 15_000 });
  await tree.click();

  await expect.poll(() => creations).toBe(1);
});

test('has no serious accessibility violations', async ({ page }) => {
  const { default: AxeBuilder } = await import('@axe-core/playwright');
  await openDocs(page);

  const result = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  const graves = result.violations.filter(
    v => v.impact === 'critical' || v.impact === 'serious');

  expect(graves.map(v => `${v.id} (${v.impact}) ×${v.nodes.length}: ${v.help}`)).toEqual([]);
});

/**
 * Los enlaces de fuera —«Mencionado en», los documentos adjuntos de una tarea, una mención en un
 * comentario— llevan a `/docs?doc=<id>`. Nadie leía ese parámetro: se abría la lista y no el
 * documento pedido.
 */
test('a link with ?doc= opens that document', async ({ page }) => {
  await openDocs(page);

  await page.route(/\/api\/v1\/docs\/[^/]+\/pages/, r => r.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify([{ id: '00000000-0000-0000-0000-0000000000a1', documentId: DOCUMENTS[0].id, parentPageId: null, title: 'Página', content: '<p>x</p>', order: 0 }]),
  }));

  // Navegación de la propia aplicación, como al pulsar un enlace: `page.goto` recargaría y
  // perdería la sesión, que vive en memoria.
  await page.evaluate(id => {
    history.pushState({}, '', `/docs?doc=${id}`);
    dispatchEvent(new PopStateEvent('popstate'));
  }, DOCUMENTS[0].id);

  await expect(page.getByRole('textbox', { name: /título del documento/i }))
    .toHaveValue('Manual de arquitectura', { timeout: 15_000 });
});

/** Cada plantilla del cajón pinta su icono: ya no se registra el juego entero de Lucide. */
test('every template in the drawer shows its icon', async ({ page }) => {
  await openDocs(page);

  await page.getByRole('button', { name: /más opciones de documento nuevo/i }).click();
  await page.getByRole('button', { name: /ver todas las plantillas/i }).first().click();
  const cajon = page.getByRole('dialog', { name: /^plantillas$/i });
  await expect(cajon.getByRole('heading', { name: /del sistema/i })).toBeVisible();

  const icons = cajon.locator('ng-icon');
  const count = await icons.count();
  expect(count).toBeGreaterThan(3);
  for (let i = 0; i < count; i++) {
    await expect(icons.nth(i).locator('svg')).toHaveCount(1);
  }
});

/**
 * El selector de emojis se carga al primer clic, y abre a la primera: si picmo pusiera además su
 * propio manejador en el botón, ese clic lo abriría y lo cerraría a la vez.
 */
test('the emoji picker loads on first click and opens', async ({ page }) => {
  await openDocs(page);
  await page.route(/\/api\/v1\/docs\/[^/]+\/pages/, r => r.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify([{ id: '00000000-0000-0000-0000-0000000000a1', documentId: DOCUMENTS[0].id, parentPageId: null, title: 'Primera', content: '<p>Hola</p>', order: 0 }]),
  }));

  await page.getByText('Manual de arquitectura').first().click();
  const button = page.getByRole('button', { name: 'Insertar emoji' });
  await expect(button).toBeVisible({ timeout: 15_000 });
  await button.click();

  await expect(page.locator('.picmo__popupContainer, .popupContainer, [class*="popupContainer"]').first()).toBeVisible({ timeout: 10_000 });
});

/**
 * Abre el primer documento con una página de un párrafo y deja simuladas sus anotaciones:
 * crear una la añade a la lista que devuelve el servidor, como haría la API.
 */
async function openPageWithText(page: Page) {
  const PAGE_ID = '00000000-0000-0000-0000-0000000000a1';
  const annotations: { id: string; documentId: string; pageId: string; quotedText: string; createdBy: string; createdAtUtc: string; resolvedAtUtc: null }[] = [];

  await openDocs(page);
  await page.route(/\/api\/v1\/docs\/[^/]+\/pages$/, r => r.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify([{ id: PAGE_ID, documentId: DOCUMENTS[0].id, parentPageId: null, title: 'Primera', content: '<p>El despliegue se hace los martes.</p><p>Segundo párrafo.</p>', order: 0 }]),
  }));
  await page.route(/\/api\/v1\/docs\/pages\/[^/]+\/annotations/, r => {
    if (r.request().method() === 'POST') {
      const body = JSON.parse(r.request().postData() ?? '{}');
      annotations.push({ id: '00000000-0000-0000-0000-0000000000c1', documentId: DOCUMENTS[0].id, pageId: PAGE_ID, quotedText: body.quotedText, createdBy: SESSION.user.id, createdAtUtc: new Date().toISOString(), resolvedAtUtc: null });
      return r.fulfill({ status: 200, contentType: 'application/json', body: '"00000000-0000-0000-0000-0000000000c1"' });
    }
    return r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(annotations) });
  });
  // Los hilos de comentarios son listas, no páginas: con `{ items }` el componente falla.
  await page.route(/\/api\/v1\/comments/, r => r.fulfill({ status: 200, contentType: 'application/json', body: '[]' }));
  await page.route(/\/api\/v1\/docs\/pages\/[^/]+$/, r => r.fulfill({ status: 200, body: '' }));

  await page.getByText('Manual de arquitectura').first().click();
  await expect(page.getByText('El despliegue se hace los martes.')).toBeVisible({ timeout: 15_000 });
  return annotations;
}

/**
 * Un comentario en línea queda pegado al texto elegido: se selecciona, se pulsa «Comentar» en el
 * menú flotante y el panel enseña la cita. La API estaba probada por integración; esto prueba
 * que la pantalla llega hasta ella.
 */
test('selecting text and pressing comment anchors an inline comment', async ({ page }) => {
  const annotations = await openPageWithText(page);

  await page.getByText('El despliegue se hace los martes.').click();
  await page.keyboard.press('Home');
  await page.keyboard.press('Shift+End');
  await page.getByRole('button', { name: 'Comentar', exact: true }).click();

  await expect.poll(() => annotations.length).toBe(1);
  expect(annotations[0].quotedText).toBe('El despliegue se hace los martes.');
  await expect(page.locator('app-document-comments').getByText('El despliegue se hace los martes.')).toBeVisible();
  await expect(page.locator('.ProseMirror [data-comment], .ProseMirror mark, .ProseMirror span[class*="comment"]').first()).toBeVisible();
});

/** Cada bloque se puede agarrar: al pasar por encima aparece su asa. */
test('hovering a block shows its drag handle', async ({ page }) => {
  await openPageWithText(page);

  await page.getByText('Segundo párrafo.').hover();
  await expect(page.locator('.asa-de-bloque')).toBeVisible();
});

/**
 * Archivar y la papelera. El documento tenía la columna de archivado y nada la usaba, y la papelera
 * no tenía salida. Además el menú lateral ofrecía «Archivados» sobre una lista que no filtraba.
 */
test('a document is archived from the list and restored from the trash', async ({ page }) => {
  const requests: string[] = [];
  await openDocs(page);
  await page.route(/\/api\/v1\/docs(\/|\?|$)/, r => {
    const url = r.request().url();
    const method = r.request().method();
    const action = url.match(/\/docs\/[^/]+\/(archive|unarchive|restore)$/);
    if (action && method === 'POST') { requests.push(action[1]); return r.fulfill({ status: 204, body: '' }); }
    if (/\/docs\/?(\?.*)?$/.test(url) && method === 'GET') {
      requests.push('list ' + (new URL(url).searchParams.get('filter') ?? 'all'));
      return r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(DOCUMENTS) });
    }
    return r.fallback();
  });

  await page.getByText('Manual de arquitectura').first().waitFor();
  await page.getByRole('button', { name: 'Acciones del documento' }).first().click();
  await page.getByRole('button', { name: 'Archivar' }).click();
  await expect.poll(() => requests).toContain('archive');

  // La papelera, desde el menú lateral: la lista se pide al servidor con su filtro.
  await page.getByRole('button', { name: 'Papelera' }).click();
  await expect.poll(() => requests).toContain('list trash');
  await page.getByRole('button', { name: 'Acciones del documento' }).first().click();
  await expect(page.getByRole('button', { name: 'Archivar' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Restaurar' }).click();
  await expect.poll(() => requests).toContain('restore');
});
