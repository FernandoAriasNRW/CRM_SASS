import { Component, computed, input, output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideChevronDown, lucideChevronRight, lucideChevronsDown, lucideChevronsUp,
  lucideFileText, lucideIndentDecrease, lucideIndentIncrease, lucidePlus, lucideTrash2
} from '@ng-icons/lucide';
import type { PageDto } from './docs.service';

/** Un movimiento pedido desde el árbol. El componente no mueve nada: lo pide. */
export interface MovimientoDePagina {
  pagina: PageDto;
  /** Dónde acaba colgada. `null` es «del documento», directamente. */
  padreId: string | null;
  /** Posición entre sus nuevas hermanas. */
  orden: number;
}

/** Una página con sus hijas ya colgadas, para pintar el árbol. */
interface Rama {
  pagina: PageDto;
  nivel: number;
  hijas: Rama[];
}

/**
 * El árbol de páginas de un documento.
 *
 * <b>No existía.</b> `Page.ParentPageId` y `Page.Order` están en el dominio desde el primer día, la
 * API publicaba `POST /docs/{id}/pages`, y la plantilla no lo llamaba desde ningún sitio: cada
 * documento se quedaba para siempre con la única página que le creó la plantilla. Toda la
 * jerarquía existía en la base de datos y era inalcanzable. Ésa era la respuesta literal a «es
 * complicado organizarlo».
 *
 * <b>Se mueve con botones y no arrastrando.</b> Arrastrar dentro de un árbol anidado es la
 * interacción más difícil de acertar de todo esto —dónde suelta, si entra dentro o al lado, qué
 * pasa al pasar por encima de una rama cerrada— y hecha a medias no se puede usar con teclado. Con
 * subir, bajar, meter dentro y sacar se llega a cualquier orden, se entiende sin explicación y
 * funciona con el tabulador. Arrastrar se puede añadir encima; no al revés.
 */
@Component({
  selector: 'app-arbol-de-paginas',
  standalone: true,
  imports: [NgIcon],
  viewProviders: [provideIcons({
    lucideChevronDown, lucideChevronRight, lucideChevronsDown, lucideChevronsUp,
    lucideFileText, lucideIndentDecrease, lucideIndentIncrease, lucidePlus, lucideTrash2
  })],
  template: `
    <div class="flex flex-col gap-1 min-w-0">
      <div class="flex items-center justify-between gap-2 px-1">
        <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground" i18n>
          Páginas
        </span>
        <button type="button" (click)="crear.emit(null)"
                i18n-title title="Nueva página"
                i18n-aria-label aria-label="Nueva página"
                class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                       focus:outline-none focus:ring-2 focus:ring-ring transition-colors">
          <ng-icon name="lucidePlus" class="w-3.5 h-3.5" aria-hidden="true" />
        </button>
      </div>

      <ul class="flex flex-col gap-0.5 list-none p-0 m-0">
        @for (rama of ramas(); track rama.pagina.id) {
          <li>
            <div class="group relative flex items-center rounded transition-colors hover:bg-accent/60"
                 [class.bg-accent]="rama.pagina.id === paginaActivaId()"
                 [style.padding-left.rem]="0.25 + rama.nivel * 0.75">

              <button type="button" (click)="abrir.emit(rama.pagina)"
                      class="flex-1 min-w-0 flex items-center gap-1.5 py-1.5 pr-2 text-left text-sm
                             rounded focus:outline-none focus:ring-2 focus:ring-ring"
                      [class.font-medium]="rama.pagina.id === paginaActivaId()">
                <ng-icon name="lucideFileText" class="w-3.5 h-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
                <span class="truncate">{{ rama.pagina.title || sinTitulo }}</span>
              </button>

              <!--
                Los controles aparecen al pasar el ratón **y al recibir foco**. Sólo con el ratón
                encima, quien navega con el tabulador enfoca botones invisibles y no sabe dónde
                está. (Sin acentos graves en este comentario: la plantilla es una cadena con
                acentos graves y uno suelto aquí dentro la corta en dos.)
              -->
              <!--
                Los controles se superponen a la derecha en vez de ocupar sitio en la fila. En
                línea se comían la mitad del ancho y el título quedaba en «Ge...» aunque la
                columna tuviera espacio de sobra: seis botones invisibles seguían midiendo.

                Con opacidad y no con display:none, para que el tabulador siga llegando a ellos.
              -->
              <div class="absolute right-1 top-1/2 -translate-y-1/2 flex items-center gap-0.5
                          rounded bg-card shadow-sm px-0.5
                          opacity-0 group-hover:opacity-100 focus-within:opacity-100
                          transition-opacity">
                <button type="button" (click)="subir(rama.pagina)" [disabled]="!puedeSubir(rama.pagina)"
                        i18n-title title="Subir"
                        i18n-aria-label aria-label="Subir"
                        class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                               disabled:opacity-30 disabled:pointer-events-none
                               focus:outline-none focus:ring-2 focus:ring-ring">
                  <ng-icon name="lucideChevronsUp" class="w-3 h-3" aria-hidden="true" />
                </button>
                <button type="button" (click)="bajar(rama.pagina)" [disabled]="!puedeBajar(rama.pagina)"
                        i18n-title title="Bajar"
                        i18n-aria-label aria-label="Bajar"
                        class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                               disabled:opacity-30 disabled:pointer-events-none
                               focus:outline-none focus:ring-2 focus:ring-ring">
                  <ng-icon name="lucideChevronsDown" class="w-3 h-3" aria-hidden="true" />
                </button>
                <button type="button" (click)="meterDentro(rama.pagina)" [disabled]="!puedeSubir(rama.pagina)"
                        i18n-title title="Convertir en subpágina de la anterior"
                        i18n-aria-label aria-label="Convertir en subpágina de la anterior"
                        class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                               disabled:opacity-30 disabled:pointer-events-none
                               focus:outline-none focus:ring-2 focus:ring-ring">
                  <ng-icon name="lucideIndentIncrease" class="w-3 h-3" aria-hidden="true" />
                </button>
                <button type="button" (click)="sacarFuera(rama.pagina)" [disabled]="!rama.pagina.parentPageId"
                        i18n-title title="Sacar un nivel"
                        i18n-aria-label aria-label="Sacar un nivel"
                        class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                               disabled:opacity-30 disabled:pointer-events-none
                               focus:outline-none focus:ring-2 focus:ring-ring">
                  <ng-icon name="lucideIndentDecrease" class="w-3 h-3" aria-hidden="true" />
                </button>
                <button type="button" (click)="crear.emit(rama.pagina.id)"
                        i18n-title title="Nueva subpágina"
                        i18n-aria-label aria-label="Nueva subpágina"
                        class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                               focus:outline-none focus:ring-2 focus:ring-ring">
                  <ng-icon name="lucidePlus" class="w-3 h-3" aria-hidden="true" />
                </button>
                <button type="button" (click)="borrar.emit(rama.pagina)" [disabled]="esLaUnica()"
                        [title]="esLaUnica() ? noSePuedeBorrar : enviarAPapelera"
                        [attr.aria-label]="esLaUnica() ? noSePuedeBorrar : enviarAPapelera"
                        class="p-1 rounded text-muted-foreground hover:text-destructive hover:bg-accent
                               disabled:opacity-30 disabled:pointer-events-none
                               focus:outline-none focus:ring-2 focus:ring-ring">
                  <ng-icon name="lucideTrash2" class="w-3 h-3" aria-hidden="true" />
                </button>
              </div>
            </div>
          </li>
        }
      </ul>
    </div>
  `
})
export class ArbolDePaginasComponent {
  readonly paginas = input.required<readonly PageDto[]>();
  readonly paginaActivaId = input<string | null>(null);

  readonly abrir = output<PageDto>();
  /** El identificador del padre, o `null` para una página de primer nivel. */
  readonly crear = output<string | null>();
  readonly borrar = output<PageDto>();
  readonly mover = output<MovimientoDePagina>();

  protected readonly sinTitulo = $localize`Sin título`;
  protected readonly enviarAPapelera = $localize`Enviar a la papelera`;
  protected readonly noSePuedeBorrar = $localize`Un documento no puede quedarse sin páginas`;

  /**
   * El árbol, aplanado para pintarlo.
   *
   * Se recorre en profundidad y cada entrada lleva su nivel, en vez de anidar `@for` dentro de
   * `@for`: con una lista plana el orden visual coincide con el orden del DOM, así que el
   * tabulador recorre las páginas en el mismo orden en el que se leen.
   */
  protected readonly ramas = computed<Rama[]>(() => {
    const todas = [...this.paginas()].sort((a, b) => a.order - b.order);
    const porPadre = new Map<string, PageDto[]>();

    for (const pagina of todas) {
      const clave = pagina.parentPageId ?? '';
      porPadre.set(clave, [...(porPadre.get(clave) ?? []), pagina]);
    }

    const aplanar = (padre: string, nivel: number): Rama[] =>
      (porPadre.get(padre) ?? []).flatMap(pagina => [
        { pagina, nivel, hijas: [] },
        ...aplanar(pagina.id, nivel + 1)
      ]);

    return aplanar('', 0);
  });

  protected esLaUnica(): boolean {
    return this.paginas().length <= 1;
  }

  protected puedeSubir(pagina: PageDto): boolean {
    return this.indiceEntreHermanas(pagina) > 0;
  }

  protected puedeBajar(pagina: PageDto): boolean {
    const hermanas = this.hermanasDe(pagina);
    return this.indiceEntreHermanas(pagina) < hermanas.length - 1;
  }

  protected subir(pagina: PageDto): void {
    this.mover.emit({
      pagina,
      padreId: pagina.parentPageId ?? null,
      orden: this.indiceEntreHermanas(pagina) - 1
    });
  }

  protected bajar(pagina: PageDto): void {
    this.mover.emit({
      pagina,
      padreId: pagina.parentPageId ?? null,
      orden: this.indiceEntreHermanas(pagina) + 1
    });
  }

  /** Se mete dentro de la hermana de encima, que es lo que hace el tabulador en cualquier lista. */
  protected meterDentro(pagina: PageDto): void {
    const hermanas = this.hermanasDe(pagina);
    const anterior = hermanas[this.indiceEntreHermanas(pagina) - 1];
    if (!anterior) return;

    const hijasDeLaAnterior = this.paginas().filter(p => p.parentPageId === anterior.id);
    this.mover.emit({ pagina, padreId: anterior.id, orden: hijasDeLaAnterior.length });
  }

  /** Sale al nivel del padre y se coloca justo detrás de él, no al final. */
  protected sacarFuera(pagina: PageDto): void {
    const padre = this.paginas().find(p => p.id === pagina.parentPageId);
    if (!padre) return;

    this.mover.emit({
      pagina,
      padreId: padre.parentPageId ?? null,
      orden: this.indiceEntreHermanas(padre) + 1
    });
  }

  private hermanasDe(pagina: PageDto): PageDto[] {
    return this.paginas()
      .filter(p => (p.parentPageId ?? null) === (pagina.parentPageId ?? null))
      .sort((a, b) => a.order - b.order);
  }

  private indiceEntreHermanas(pagina: PageDto): number {
    return this.hermanasDe(pagina).findIndex(p => p.id === pagina.id);
  }
}
