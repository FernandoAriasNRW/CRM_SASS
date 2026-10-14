import { expect, type Page } from '@playwright/test';

/**
 * Inicio de sesión común de los E2E.
 *
 * Cada spec simula la API con `page.route` y entraba con su propia copia del formulario.
 * Algunas copias devolvían una sesión sin `accessTokenExpiresAtUtc`, y eso bastaba para que
 * el login fallara de forma intermitente:
 *
 * 1. `new Date(undefined)` es una fecha inválida y `SessionManagerService` calcula un
 *    retraso `NaN`, que `setTimeout` trata como 0: pide `/auth/refresh` al instante.
 * 2. El simulador genérico de la spec contesta 200 con un cuerpo sin `accessToken`, y el
 *    refresco guarda un token vacío: el usuario deja de estar autenticado.
 * 3. Mientras tanto la navegación a `/` está cargando el componente perezoso. Si el
 *    refresco termina antes —lo normal cuando el servidor de desarrollo va cargado con
 *    varios workers—, `authGuard` ve la sesión vacía y devuelve a `/login`.
 *
 * Por eso aquí la sesión siempre lleva caducidad y `submitLogin` comprueba que la
 * respuesta simulada la traiga: una spec nueva que la olvide falla con el motivo, en lugar
 * de hacerlo una de cada muchas ejecuciones esperando 30 s a que cambie la URL.
 */

export interface SessionUser {
  id: string;
  name: string;
  email: string;
  role: string;
  tenantId: string;
}

export const DEFAULT_USER: SessionUser = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Admin',
  email: 'admin@acme.com',
  role: 'Admin',
  tenantId: '00000000-0000-0000-0000-0000000000ff',
};

/**
 * Respuesta de `POST /auth/login` tal como la da la API. El token caduca en un día: lejos
 * del minuto previo en el que `SessionManagerService` programa el refresco, así que
 * durante una prueba no se pide ninguno.
 */
export function createSession(user: SessionUser = DEFAULT_USER) {
  return {
    accessToken: 'token-de-prueba',
    accessTokenExpiresAtUtc: new Date(Date.now() + 864e5).toISOString(),
    refreshToken: 'refresco-de-prueba',
    refreshTokenExpiresAtUtc: new Date(Date.now() + 7 * 864e5).toISOString(),
    user,
  };
}

/**
 * Lo que pide el shell autenticado nada más pintarse, con la forma que espera.
 *
 * Devuelve `undefined` si la URL no es suya, para que el simulador genérico de la spec siga
 * con lo demás. Hace falta porque esos simuladores contestan `{items, totalCount}` a todo, y
 * `/users/tenant` es un array plano: con la forma equivocada el shell lanza
 * «_users is not iterable» y deja de detectar cambios, y el fallo aparece lejos —la paleta
 * no se abre, una vista no se pinta—. Antes no se notaba porque la sesión se perdía al
 * entrar y el shell ni llegaba a pintarse (ver arriba).
 */
export function shellResponse(url: string, session: { user: SessionUser }): unknown {
  // `users/me` es de donde sale el rol: sin él `isAdmin()` es falso.
  if (/\/auth\/users\/me(\?|$)/.test(url)) return session.user;
  if (/\/users\/tenant(\?|$)/.test(url)) return [session.user];
  return undefined;
}

const LOGIN_URL =/\/api\/v1\/auth\/login(\?|$)/;

/**
 * Rellena el formulario de `/login`, lo envía y espera a salir de él.
 *
 * Las rutas de la API tienen que estar ya simuladas: la respuesta del login la da la spec,
 * normalmente con `createSession`.
 */
export async function submitLogin(page: Page, email = 'admin@acme.com', password = 'admin123'): Promise<void> {
  await page.goto('/login');
  await page.getByPlaceholder('admin@acme.com').fill(email);
  await page.getByPlaceholder('••••••••').fill(password);

  // La espera se arma antes del clic para no perder una respuesta rápida.
  const loginResponse = page.waitForResponse(r => LOGIN_URL.test(r.url()) && r.request().method() === 'POST');
  await page.getByRole('button', { name: /ingresar/i }).click();
  const response = await loginResponse;

  expect(response.ok(), `POST /auth/login respondió ${response.status()}`).toBe(true);
  const body = await response.json() as { accessToken?: unknown; accessTokenExpiresAtUtc?: unknown };
  expect(typeof body.accessToken, 'la sesión simulada no trae accessToken').toBe('string');
  expect(
    Number.isNaN(Date.parse(String(body.accessTokenExpiresAtUtc))),
    'la sesión simulada no trae una accessTokenExpiresAtUtc válida: la aplicación pediría ' +
    '/auth/refresh al instante y perdería el token (usa createSession)',
  ).toBe(false);

  // Holgado a propósito: la primera visita compila la ruta perezosa en el servidor de desarrollo.
  await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });
}
