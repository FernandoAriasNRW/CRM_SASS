import { Injectable, signal } from '@angular/core';

/** Un elemento de una sección propia del módulo: un documento favorito, una página reciente. */
export interface SectionItem {
  readonly id: string;
  readonly label: string;
  readonly icon?: string;
  readonly onSelect: () => void;
}

/** Una sección que un módulo añade a su panel, debajo de las entradas de navegación. */
export interface PanelSection {
  readonly title: string;
  readonly items: readonly SectionItem[];
  /** Qué decir cuando la sección está vacía. Sin esto, la sección no se pinta. */
  readonly fallback?: string;
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
export class PanelSectionsService {
  private readonly byModule = signal<Record<string, readonly PanelSection[]>>({});

  /** Las secciones de un módulo, o vacío si no ha registrado ninguna. */
  readonly forModule = (moduleKey: string): readonly PanelSection[] => this.byModule()[moduleKey] ?? [];

  /** Señal para que el panel se entere de los cambios. */
  readonly all = this.byModule.asReadonly();

  register(moduleKey: string, sections: readonly PanelSection[]): void {
    this.byModule.update(current => ({ ...current, [moduleKey]: sections }));
  }

  clear(moduleKey: string): void {
    this.byModule.update(current => {
      const copy = { ...current };
      delete copy[moduleKey];
      return copy;
    });
  }
}
