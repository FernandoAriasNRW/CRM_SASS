import { test, expect, type Page } from '@playwright/test';
import { createSession, submitLogin } from './support/auth';

/**
 * Mover y redimensionar los recuadros del panel.
 *
 * La disposición se guardaba y había endpoint para cambiarla, pero en pantalla sólo se podían
 * quitar recuadros. Y la fila guardada se ignoraba: la rejilla colocaba cada recuadro donde
 * cupiera. Aquí se arrastra con el ratón, se redimensiona con el teclado y se mira qué se guarda.
 */

const USER = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin Administrator', email: 'admin@acme.com', role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

const SESSION = createSession(USER);

const placement = (id: string, x: number, y: number) =>
  ({ id, reportId: `r-${id}`, x, y, width: 6, height: 4, visualization: 'table', title: null });

const data = (id: string, title: string) => ({
  widgetId: id, reportId: `r-${id}`, title, visualization: 'table', subtitle: null,
  columns: ['Estado', 'Tareas'], rows: [['Hecho', '3']], error: null,
});

const json = (body: unknown, status = 200) => ({
  status, contentType: 'application/json', body: JSON.stringify(body),
});

type Saved = { widgets: { id: string; x: number; y: number; width: number; height: number; reportId: string }[] };

async function openPanel(page: Page, isMine = true, { kpiFails = false } = {}): Promise<Saved[]> {
  const saved: Saved[] = [];

  await page.route(/\/api\/v1\//, r => {
    const url = r.request().url();
    const method = r.request().method();

    if (/\/auth\/login/.test(url)) return r.fulfill(json(SESSION));
    if (/\/auth\/users\/me/.test(url)) return r.fulfill(json(USER));
    if (/\/users\/tenant/.test(url)) return r.fulfill(json([USER]));
    if (/\/notifications/.test(url)) return r.fulfill(json([]));
    if (/\/dashboards\/mine/.test(url)) {
      return r.fulfill(json({
        id: 'p1', title: 'Mi panel', isMine, isPublic: false,
        widgets: [placement('a', 0, 0), placement('b', 6, 0), placement('c', 0, 8)],
      }));
    }
    if (/\/dashboards\/p1\/data/.test(url)) {
      return r.fulfill(json([data('a', 'Tareas por estado'), data('b', 'Tickets por prioridad'), data('c', 'Proyectos por estado')]));
    }
    if (/\/dashboards\/p1\/layout/.test(url) && method === 'PUT') {
      saved.push(JSON.parse(r.request().postData() ?? '{}'));
      return r.fulfill({ status: 204, body: '' });
    }
    if (/\/dashboards(\?|$)/.test(url)) return r.fulfill(json([]));
    if (/\/reports\/kpi/.test(url)) return r.fulfill(kpiFails ? json({}, 500) : json({}));
    if (/\/reports\//.test(url)) return r.fulfill(json([]));

    return r.fulfill(json({ items: [], totalCount: 0 }));
  });

  await submitLogin(page);

  // Se navega por dentro: `page.goto` recargaría y perdería el token, que vive en memoria.
  await page.keyboard.press('Control+k');
  await page.keyboard.type('panel');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/dashboard/);
  await expect(page.getByTestId('panel-grid')).toBeVisible({ timeout: 15_000 });
  return saved;
}

const box = (page: Page, title: string) => page.locator(`[data-widget="${title}"]`);

test('the saved row is respected, not just the height', async ({ page }) => {
  await openPanel(page);

  // «Proyectos» está guardado en la fila 8, con cuatro filas libres encima. Colocado
  // automáticamente quedaría pegado debajo de «Tareas», en la fila 4.
  const top = await box(page, 'Tareas por estado').boundingBox();
  const below = await box(page, 'Proyectos por estado').boundingBox();
  const gap = below!.y - (top!.y + top!.height);
  expect(gap).toBeGreaterThan(top!.height / 2);
});

test('dragging a widget onto another moves it there and pushes the other down', async ({ page }) => {
  const saved = await openPanel(page);

  const grip = page.getByRole('button', { name: 'Mover «Tickets por prioridad»' });
  const target = await box(page, 'Tareas por estado').boundingBox();
  const from = await grip.boundingBox();

  await page.mouse.move(from!.x + from!.width / 2, from!.y + from!.height / 2);
  await page.mouse.down();
  await page.mouse.move(target!.x + 10, from!.y + from!.height / 2, { steps: 8 });
  await page.mouse.up();

  await expect.poll(() => saved.length).toBe(1);
  const widgets = Object.fromEntries(saved[0].widgets.map(w => [w.id, w]));
  expect(widgets['b']).toEqual(expect.objectContaining({ x: 0, y: 0 }));
  expect(widgets['a'].y).toBeGreaterThanOrEqual(4);
  expect(widgets['b'].reportId, 'se reenvía entero').toBe('r-b');
});

test('the corner handle resizes with the keyboard and saves once', async ({ page }) => {
  const saved = await openPanel(page);

  const handle = page.getByRole('button', { name: 'Cambiar el tamaño de «Proyectos por estado»' });
  await handle.focus();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowDown');

  await expect.poll(() => saved.length).toBe(1);
  const c = saved[0].widgets.find(w => w.id === 'c')!;
  expect(c).toEqual(expect.objectContaining({ width: 8, height: 5 }));
});

test('a panel that is not mine cannot be rearranged', async ({ page }) => {
  await openPanel(page, false);

  await expect(box(page, 'Tareas por estado')).toBeVisible();
  await expect(page.getByRole('button', { name: /^Mover «/ })).toHaveCount(0);
  await expect(page.getByRole('button', { name: /^Cambiar el tamaño/ })).toHaveCount(0);
});

test('indicators that fail to load say so once, instead of a dash forever', async ({ page }) => {
  await openPanel(page, true, { kpiFails: true });

  // Antes se quedaban en «—», igual que mientras cargan, y nada decía que había un error.
  await expect(page.getByRole('alert').filter({ hasText: 'No se pudieron cargar los indicadores.' })).toBeVisible();
  // El panel lo explica él: el aviso global no lo repite.
  await expect(page.getByText('Error del servidor')).toHaveCount(0);
});
