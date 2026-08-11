import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/**
 * La guía de diseño renderiza los componentes reales, así que auditarla equivale a
 * auditar la biblioteca entera de una vez. Es la red que evita repetir lo que pasó con
 * `empty-state`: dos errores latentes durante meses porque ninguna vista lo importaba y
 * por tanto nunca se compilaba.
 */

const SESION = {
  accessToken: 't', refreshToken: 'r',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
  user: { id: '1', name: 'Admin', email: 'admin@acme.com', role: 'Admin', tenantId: 'ff' },
};

test.beforeEach(async ({ page }) => {
  await page.route(/\/api\/v1\/auth\/login/, r =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(SESION) }));
  await page.route(/\/api\/v1\//, r => {
    const u = r.request().url();
    if (/\/auth\/login/.test(u)) return r.fallback();
    if (/\/views\//.test(u)) return r.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    return r.fulfill({ status: 200, contentType: 'application/json', body: '{"items":[],"totalCount":0}' });
  });

  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill('admin@acme.com');
  await page.getByPlaceholder('••••••••').fill('admin123');
  await page.getByRole('button', { name: /ingresar/i }).click();
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

  await page.keyboard.press('Control+k');
  await page.keyboard.type('sistema de diseño');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/design-system/, { timeout: 30_000 });
});

test('renderiza la biblioteca sin errores', async ({ page }) => {
  await expect(page.getByRole('heading', { name: 'Sistema de diseño', level: 1 })).toBeVisible();

  // Si un componente reventara al construirse, su sección quedaría vacía.
  for (const seccion of ['Color', 'Botones', 'Etiquetas', 'Tarjetas', 'Carga y vacío']) {
    await expect(page.getByRole('heading', { name: seccion, level: 2 })).toBeVisible();
  }
});

test('no tiene violaciones graves de accesibilidad', async ({ page }) => {
  const resultado = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  const graves = resultado.violations.filter(
    v => v.impact === 'critical' || v.impact === 'serious');

  expect(graves.map(v => `${v.id} (${v.impact}) ×${v.nodes.length}: ${v.help}`)).toEqual([]);
});

test('la directiva de teclado funciona en la propia guía', async ({ page }) => {
  const caja = page.getByText(/Actívame con el ratón/);

  await caja.focus();
  await page.keyboard.press('Enter');

  await expect(page.getByText(/Actívame con el ratón.*1/)).toBeVisible();
});
