/**
 * Cambio de idioma.
 *
 * Con traducción en tiempo de compilación cada idioma es una aplicación distinta servida
 * bajo su propio prefijo (`/es/`, `/en/`), así que cambiar de idioma **no** es cambiar una
 * señal: es ir a la otra aplicación. Eso implica recargar y perder el estado en memoria
 * —incluido el token de acceso, que vive ahí por decisión de seguridad—, de modo que hay
 * que volver a iniciar sesión.
 *
 * Es la contrapartida de haber elegido `@angular/localize`: a cambio, no se descarga
 * ningún catálogo en el navegador y los textos no pueden faltar en tiempo de ejecución.
 */

export type Idioma = 'es' | 'en';

export const IDIOMAS: ReadonlyArray<{ codigo: Idioma; nombre: string }> = [
  { codigo: 'es', nombre: 'Español' },
  { codigo: 'en', nombre: 'English' },
];

/**
 * Idioma que se está sirviendo, leído del `<base href>` que escribe la compilación.
 *
 * No se usa el idioma del navegador: eso dice qué prefiere el usuario, no qué está viendo.
 * En desarrollo no hay prefijo y se asume el idioma de origen.
 */
export function idiomaActual(documento: Document = document): Idioma {
  const base = documento.querySelector('base')?.getAttribute('href') ?? '/';
  const codigo = base.split('/').filter(Boolean)[0];
  return codigo === 'en' ? 'en' : 'es';
}

/**
 * Devuelve la URL equivalente en otro idioma, conservando la ruta.
 *
 * Se conserva para que cambiar de idioma desde una pantalla concreta no devuelva al
 * inicio, que es lo que hace casi cualquier implementación descuidada.
 */
export function urlEnIdioma(destino: Idioma, ubicacion: Pick<Location, 'pathname' | 'search'> = location): string {
  const partes = ubicacion.pathname.split('/').filter(Boolean);

  // Quitar el prefijo de idioma si lo hay; en desarrollo no existe.
  if (partes[0] === 'es' || partes[0] === 'en') {
    partes.shift();
  }

  return `/${destino}/${partes.join('/')}${ubicacion.search}`;
}
