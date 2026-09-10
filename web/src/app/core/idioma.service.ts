import { Injectable, inject, LOCALE_ID } from '@angular/core';
import { DOCUMENT } from '@angular/common';

/** Los idiomas que la aplicación compila. Ver `angular.json` → `i18n`. */
export const IDIOMAS = [
  { codigo: 'es', nombre: 'Español' },
  { codigo: 'en', nombre: 'English' }
] as const;

export type CodigoDeIdioma = (typeof IDIOMAS)[number]['codigo'];

/** Cuánto dura la elección de idioma: un año, como cualquier preferencia de interfaz. */
const UN_ANO_EN_SEGUNDOS = 60 * 60 * 24 * 365;

/**
 * En qué idioma se está y cómo cambiarlo.
 *
 * <b>La traducción es en tiempo de compilación</b>, así que cambiar de idioma no es cambiar una
 * variable: es cargar otra copia de la aplicación. Cada idioma vive bajo su propio prefijo
 * (`/es/`, `/en/`) y el cambio se hace navegando, conservando la ruta y los parámetros para caer
 * en la misma pantalla.
 *
 * <b>Y la elección se guarda en una cookie que lee el servidor.</b> Antes el idioma lo decidía
 * únicamente el `Accept-Language` del navegador: quien lo tuviera en inglés se quedaba en inglés
 * para siempre, sin ninguna forma de cambiarlo desde la aplicación. La cookie es lo que hace que
 * entrar por la raíz respete lo que la persona eligió la última vez.
 *
 * Se usa cookie y no `localStorage` porque **quien decide a qué carpeta enviar la visita es
 * nginx**, y el servidor no ve `localStorage`.
 */
@Injectable({ providedIn: 'root' })
export class IdiomaService {
  private readonly documento = inject(DOCUMENT);

  /**
   * El idioma en el que se está.
   *
   * Sale de `LOCALE_ID`, que Angular rellena con el idioma con el que se compiló este paquete.
   * Es la única fuente que no puede mentir: la URL se puede manipular y la cookie puede ir por
   * detrás de la navegación.
   */
  readonly actual = (inject(LOCALE_ID) as string).split('-')[0] as CodigoDeIdioma;

  readonly disponibles = IDIOMAS;

  /**
   * Cambia de idioma recargando la aplicación en el otro prefijo.
   *
   * Se conserva la ruta y la consulta: quien cambia de idioma mirando un ticket filtrado espera
   * seguir mirando ese ticket filtrado, no volver al inicio.
   */
  cambiarA(codigo: CodigoDeIdioma): void {
    if (codigo === this.actual) return;

    this.recordar(codigo);

    const ventana = this.documento.defaultView;
    if (!ventana) return;

    ventana.location.href = `/${codigo}${this.rutaSinPrefijo()}`;
  }

  /**
   * La ruta actual sin el prefijo de idioma.
   *
   * En desarrollo (`ng serve`) no hay prefijo —el servidor sirve un solo idioma— así que la ruta
   * se devuelve tal cual y el cambio no lleva a ninguna parte útil. Es esperado: los dos idiomas
   * sólo existen en el artefacto compilado.
   */
  private rutaSinPrefijo(): string {
    const { pathname, search, hash } = this.documento.location;
    const sinPrefijo = pathname.replace(/^\/(es|en)(?=\/|$)/, '') || '/';
    return sinPrefijo + search + hash;
  }

  /**
   * Guarda la elección donde el servidor la lea.
   *
   * `SameSite=Lax` y no `Strict`: con `Strict` la cookie no viaja cuando se llega desde un enlace
   * externo, que es justo cuando más importa acertar el idioma. No lleva datos de nadie, así que
   * no necesita `HttpOnly` —de hecho la escribe el propio navegador—.
   */
  private recordar(codigo: CodigoDeIdioma): void {
    this.documento.cookie =
      `idioma=${codigo}; path=/; max-age=${UN_ANO_EN_SEGUNDOS}; SameSite=Lax`;
  }
}
