import { Injectable, signal } from '@angular/core';

/** Un elemento de una sección propia del módulo: un documento favorito, una página reciente. */
export interface ElementoDeSeccion {
  readonly id: string;
  readonly etiqueta: string;
  readonly icono?: string;
  readonly alPulsar: () => void;
}

/** Una sección que un módulo añade a su panel, debajo de las entradas de navegación. */
export interface SeccionDelPanel {
  readonly titulo: string;
  readonly elementos: readonly ElementoDeSeccion[];
  /** Qué decir cuando la sección está vacía. Sin esto, la sección no se pinta. */
  readonly siNoHayNada?: string;
}

/**
 * Por dónde un módulo mete sus propias secciones en el panel de navegación.
 *
 * <b>Existe por Documentos.</b> Su panel tenía además de las pestañas una lista de favoritos y
 * otra de páginas recientes, y por eso era el único módulo con barra lateral propia: nadie de
 * fuera podía pintar esos datos. El resultado era el doble submenú —el panel compartido a la
 * izquierda y el suyo justo al lado—.
 *
 * La alternativa era que el armazón conociera a Documentos y le pidiera sus favoritos, y eso ata
 * el armazón a un módulo concreto: el siguiente que quiera una sección obligaría a tocarlo otra
 * vez. Así el módulo empuja lo suyo y el panel sólo sabe pintar secciones.
 *
 * <b>Quien registra, limpia.</b> La pantalla que las pone las quita al cerrarse; si no, al salir
 * de Documentos el panel de Tareas seguiría enseñando documentos favoritos.
 */
@Injectable({ providedIn: 'root' })
export class SeccionesDelPanelService {
  private readonly porModulo = signal<Record<string, readonly SeccionDelPanel[]>>({});

  /** Las secciones de un módulo, o vacío si no ha registrado ninguna. */
  readonly de = (modulo: string): readonly SeccionDelPanel[] => this.porModulo()[modulo] ?? [];

  /** Señal para que el panel se entere de los cambios. */
  readonly todas = this.porModulo.asReadonly();

  registrar(modulo: string, secciones: readonly SeccionDelPanel[]): void {
    this.porModulo.update(actuales => ({ ...actuales, [modulo]: secciones }));
  }

  limpiar(modulo: string): void {
    this.porModulo.update(actuales => {
      const copia = { ...actuales };
      delete copia[modulo];
      return copia;
    });
  }
}
