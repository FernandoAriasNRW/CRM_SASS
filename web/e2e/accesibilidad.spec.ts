import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/**
 * Auditoría automática de accesibilidad.
 *
 * axe detecta del orden de la mitad de los problemas reales de una página: sirve como red
 * de seguridad contra regresiones, no como certificado de conformidad. Lo que no puede
 * comprobar —orden de foco coherente, textos alternativos con sentido, si la navegación
 * por teclado lleva a alguna parte— se cubre con las pruebas de teclado de abajo y con
 * revisión manual.
 *
 * Se auditan las rutas públicas. El resto de la aplicación exige sesión y datos
 * sembrados; queda pendiente de cubrir cuando los E2E autenticados existan.
 */

const RUTAS_PUBLICAS = [
  { nombre: 'login', url: '/login' },
  { nombre: 'alta pública de tickets', url: '/support' },
];

for (const ruta of RUTAS_PUBLICAS) {
  test(`${ruta.nombre} no tiene violaciones graves de accesibilidad`, async ({ page }) => {
    await page.goto(ruta.url);

    const resultado = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    const graves = resultado.violations.filter(
      v => v.impact === 'critical' || v.impact === 'serious');

    // El mensaje enumera qué falla y dónde: un fallo que sólo diga "esperaba 0, hubo 3"
    // obliga a reproducirlo a mano para saber qué arreglar.
    expect(graves.map(v => `${v.id} (${v.impact}) en ${v.nodes.length}: ${v.help}`)).toEqual([]);
  });
}

test('el login se puede completar sólo con el teclado', async ({ page }) => {
  await page.route('**/api/v1/auth/login', route =>
    route.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));

  await page.goto('/login');
  await expect(page.getByPlaceholder('admin@acme.com')).toBeVisible();

  // El primer Tab se dirige al body: tras goto, en headless el documento no tiene el
  // foco y la pulsación se perdería sin llegar a la página.
  await page.locator('body').press('Tab');
  await expect(page.getByPlaceholder('admin@acme.com')).toBeFocused();
  await page.keyboard.type('admin@acme.com');

  await page.keyboard.press('Tab');
  await expect(page.getByPlaceholder('••••••••')).toBeFocused();
  await page.keyboard.type('admin123');

  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: /ingresar/i })).toBeFocused();
});

test('el foco es visible al navegar con el teclado', async ({ page }) => {
  await page.goto('/login');

  // Esperar a que el formulario exista antes de tabular. Sin esto la pulsación puede
  // llegar cuando Angular aún no ha montado la vista: no hay nada enfocable, el foco se
  // queda en el body y la prueba falla de forma intermitente según la carga de la máquina.
  await expect(page.getByPlaceholder('admin@acme.com')).toBeVisible();
  await page.locator('body').press('Tab');

  // Varias pantallas usan `focus:outline-none` confiando en un anillo propio. Si alguien
  // retira el anillo y deja el outline anulado, el foco desaparece y la navegación por
  // teclado se vuelve imposible de seguir sin que nada falle.
  const estilo = await page.locator(':focus').evaluate(el => {
    const s = getComputedStyle(el);
    return { outline: s.outlineStyle, ancho: s.outlineWidth, sombra: s.boxShadow };
  });

  const tieneIndicador =
    (estilo.outline !== 'none' && estilo.ancho !== '0px') ||
    (estilo.sombra !== 'none' && estilo.sombra !== '');

  expect(tieneIndicador, `el elemento enfocado no muestra indicador: ${JSON.stringify(estilo)}`).toBe(true);
});
