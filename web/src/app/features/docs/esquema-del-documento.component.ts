import { Component, computed, input, signal } from '@angular/core';
import type { Editor } from '@tiptap/core';

/** Un encabezado del documento, con su nivel y dónde está. */
export interface EntradaDelEsquema {
  nivel: number;
  texto: string;
  /** Posición en el documento, que es como se navega hasta él. */
  posicion: number;
}

/**
 * El índice del documento: sus encabezados, navegables.
 *
 * <b>Se calcula del documento, no se guarda.</b> Un índice guardado se desincroniza en cuanto
 * alguien renombra un encabezado por otra vía, y entonces la barra lateral enseña títulos que ya
 * no están en el texto. Recorrer el documento es barato —son sus nodos de primer nivel— y no
 * puede mentir.
 *
 * <b>Y se recalcula al guardar, no en cada tecla.</b> Recalcularlo en cada pulsación redibuja la
 * barra lateral mientras se escribe el título, que parpadea y distrae justo cuando hace falta
 * concentración.
 */
@Component({
  selector: 'app-esquema-del-documento',
  standalone: true,
  template: `
    <div class="space-y-1">
      <h4 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground px-2" i18n>
        En esta página
      </h4>

      @if (entradas().length === 0) {
        <p class="text-xs text-muted-foreground px-2 py-1" i18n>
          Sin encabezados. Usa / para añadir uno.
        </p>
      } @else {
        @for (e of entradas(); track e.posicion) {
          <button
            type="button"
            (click)="irA(e)"
            [style.padding-left.rem]="0.5 + (e.nivel - 1) * 0.75"
            class="w-full text-left py-1 pr-2 text-xs rounded hover:bg-secondary transition-colors truncate"
            [class.font-medium]="e.nivel === 1"
            [class.text-muted-foreground]="e.nivel > 1"
            [title]="e.texto">
            {{ e.texto }}
          </button>
        }
      }
    </div>
  `
})
export class EsquemaDelDocumentoComponent {
  readonly editor = input.required<Editor>();

  /**
   * Cambia cuando el contenido se guarda, para volver a leer los encabezados.
   *
   * Es una señal que el componente padre incrementa: así el esquema no tiene que suscribirse a los
   * eventos del editor —que se disparan en cada tecla— y se refresca cuando de verdad hay algo
   * nuevo que enseñar.
   */
  readonly version = input(0);

  private readonly recalculo = signal(0);

  readonly entradas = computed<EntradaDelEsquema[]>(() => {
    // Se leen las dos señales para que el cálculo dependa de ellas: `version` desde fuera y
    // `recalculo` desde el propio componente al forzar un refresco.
    this.version();
    this.recalculo();

    const editor = this.editor();
    if (!editor) return [];

    const entradas: EntradaDelEsquema[] = [];

    editor.state.doc.descendants((nodo, posicion) => {
      if (nodo.type.name !== 'heading') return;

      const texto = nodo.textContent.trim();

      // Un encabezado vacío —recién creado, todavía sin escribir— no entra: aparecería en el
      // índice como una línea en blanco que no lleva a ninguna parte.
      if (texto.length === 0) return;

      entradas.push({ nivel: nodo.attrs['level'] ?? 1, texto, posicion });
    });

    return entradas;
  });

  /**
   * Lleva el cursor al encabezado y lo deja a la vista.
   *
   * Se mueve la selección además de desplazar la pantalla: quien pulsa un título del índice
   * normalmente va a escribir ahí, y dejar el cursor donde estaba obligaría a hacer clic otra vez.
   */
  irA(entrada: EntradaDelEsquema): void {
    this.editor()
      .chain()
      .focus()
      .setTextSelection(entrada.posicion + 1)
      .scrollIntoView()
      .run();
  }

  /** Fuerza un repaso, por si el padre necesita refrescarlo sin cambiar la versión. */
  refrescar(): void {
    this.recalculo.update(v => v + 1);
  }
}
