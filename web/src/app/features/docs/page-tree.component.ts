import { Component, computed, input, output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideChevronDown, lucideChevronRight, lucideChevronsDown, lucideChevronsUp,
  lucideFileText, lucideIndentDecrease, lucideIndentIncrease, lucidePlus, lucideTrash2
} from '@ng-icons/lucide';
import type { PageDto } from './docs.service';

/** Un movimiento pedido desde el árbol. El componente no mueve nada: lo pide. */
export interface PageMove {
  page: PageDto;
  /** Dónde acaba colgada. `null` es «del documento», directamente. */
  parentId: string | null;
  /** Posición entre sus nuevas hermanas. */
  order: number;
}

/** Una página con sus hijas ya colgadas, para pintar el árbol. */
interface TreeBranch {
  page: PageDto;
  level: number;
  children: TreeBranch[];
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
  selector: 'app-page-tree',
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
        <button type="button" (click)="create.emit(null)"
                i18n-title title="Nueva página"
                i18n-aria-label aria-label="Nueva página"
                class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                       focus:outline-none focus:ring-2 focus:ring-ring transition-colors">
          <ng-icon name="lucidePlus" class="w-3.5 h-3.5" aria-hidden="true" />
        </button>
      </div>

      <ul class="flex flex-col gap-0.5 list-none p-0 m-0">
        @for (branch of branches(); track branch.page.id) {
          <li>
            <div class="group relative flex items-center rounded transition-colors hover:bg-accent/60"
                 [class.bg-accent]="branch.page.id === activePageId()"
                 [style.padding-left.rem]="0.25 + branch.level * 0.75">

              <button type="button" (click)="open.emit(branch.page)"
                      class="flex-1 min-w-0 flex items-center gap-1.5 py-1.5 pr-2 text-left text-sm
                             rounded focus:outline-none focus:ring-2 focus:ring-ring"
                      [class.font-medium]="branch.page.id === activePageId()">
                <ng-icon name="lucideFileText" class="w-3.5 h-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
                <span class="truncate">{{ branch.page.title || untitled }}</span>
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
                <button type="button" (click)="moveUp(branch.page)" [disabled]="!canMoveUp(branch.page)"
                        i18n-title title="Subir"
                        i18n-aria-label aria-label="Subir"
                        class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                               disabled:opacity-30 disabled:pointer-events-none
                               focus:outline-none focus:ring-2 focus:ring-ring">
                  <ng-icon name="lucideChevronsUp" class="w-3 h-3" aria-hidden="true" />
                </button>
                <button type="button" (click)="moveDown(branch.page)" [disabled]="!canMoveDown(branch.page)"
                        i18n-title title="Bajar"
                        i18n-aria-label aria-label="Bajar"
                        class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                               disabled:opacity-30 disabled:pointer-events-none
                               focus:outline-none focus:ring-2 focus:ring-ring">
                  <ng-icon name="lucideChevronsDown" class="w-3 h-3" aria-hidden="true" />
                </button>
                <button type="button" (click)="indent(branch.page)" [disabled]="!canMoveUp(branch.page)"
                        i18n-title title="Convertir en subpágina de la anterior"
                        i18n-aria-label aria-label="Convertir en subpágina de la anterior"
                        class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                               disabled:opacity-30 disabled:pointer-events-none
                               focus:outline-none focus:ring-2 focus:ring-ring">
                  <ng-icon name="lucideIndentIncrease" class="w-3 h-3" aria-hidden="true" />
                </button>
                <button type="button" (click)="outdent(branch.page)" [disabled]="!branch.page.parentPageId"
                        i18n-title title="Sacar un nivel"
                        i18n-aria-label aria-label="Sacar un nivel"
                        class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                               disabled:opacity-30 disabled:pointer-events-none
                               focus:outline-none focus:ring-2 focus:ring-ring">
                  <ng-icon name="lucideIndentDecrease" class="w-3 h-3" aria-hidden="true" />
                </button>
                <button type="button" (click)="create.emit(branch.page.id)"
                        i18n-title title="Nueva subpágina"
                        i18n-aria-label aria-label="Nueva subpágina"
                        class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                               focus:outline-none focus:ring-2 focus:ring-ring">
                  <ng-icon name="lucidePlus" class="w-3 h-3" aria-hidden="true" />
                </button>
                <button type="button" (click)="delete.emit(branch.page)" [disabled]="isOnlyPage()"
                        [title]="isOnlyPage() ? cannotDelete : moveToTrash"
                        [attr.aria-label]="isOnlyPage() ? cannotDelete : moveToTrash"
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
export class PageTreeComponent {
  readonly pages = input.required<readonly PageDto[]>();
  readonly activePageId = input<string | null>(null);

  readonly open = output<PageDto>();
  /** El identificador del padre, o `null` para una página de primer nivel. */
  readonly create = output<string | null>();
  readonly delete = output<PageDto>();
  readonly move = output<PageMove>();

  protected readonly untitled = $localize`Sin título`;
  protected readonly moveToTrash = $localize`Enviar a la papelera`;
  protected readonly cannotDelete = $localize`Un documento no puede quedarse sin páginas`;

  /**
   * El árbol, aplanado para pintarlo.
   *
   * Se recorre en profundidad y cada entrada lleva su nivel, en vez de anidar `@for` dentro de
   * `@for`: con una lista plana el orden visual coincide con el orden del DOM, así que el
   * tabulador recorre las páginas en el mismo orden en el que se leen.
   */
  protected readonly branches = computed<TreeBranch[]>(() => {
    const all = [...this.pages()].sort((a, b) => a.order - b.order);
    const byParent = new Map<string, PageDto[]>();

    for (const page of all) {
      const key = page.parentPageId ?? '';
      byParent.set(key, [...(byParent.get(key) ?? []), page]);
    }

    const aplanar = (parent: string, level: number): TreeBranch[] =>
      (byParent.get(parent) ?? []).flatMap(page => [
        { page, level, children: [] },
        ...aplanar(page.id, level + 1)
      ]);

    return aplanar('', 0);
  });

  protected isOnlyPage(): boolean {
    return this.pages().length <= 1;
  }

  protected canMoveUp(page: PageDto): boolean {
    return this.indexAmongSiblings(page) > 0;
  }

  protected canMoveDown(page: PageDto): boolean {
    const siblings = this.siblingsOf(page);
    return this.indexAmongSiblings(page) < siblings.length - 1;
  }

  protected moveUp(page: PageDto): void {
    this.move.emit({
      page,
      parentId: page.parentPageId ?? null,
      order: this.indexAmongSiblings(page) - 1
    });
  }

  protected moveDown(page: PageDto): void {
    this.move.emit({
      page,
      parentId: page.parentPageId ?? null,
      order: this.indexAmongSiblings(page) + 1
    });
  }

  /** Se mete dentro de la hermana de encima, que es lo que hace el tabulador en cualquier lista. */
  protected indent(page: PageDto): void {
    const siblings = this.siblingsOf(page);
    const previous = siblings[this.indexAmongSiblings(page) - 1];
    if (!previous) return;

    const previousChildren = this.pages().filter(p => p.parentPageId === previous.id);
    this.move.emit({ page, parentId: previous.id, order: previousChildren.length });
  }

  /** Sale al nivel del padre y se coloca justo detrás de él, no al final. */
  protected outdent(page: PageDto): void {
    const parent = this.pages().find(p => p.id === page.parentPageId);
    if (!parent) return;

    this.move.emit({
      page,
      parentId: parent.parentPageId ?? null,
      order: this.indexAmongSiblings(parent) + 1
    });
  }

  private siblingsOf(page: PageDto): PageDto[] {
    return this.pages()
      .filter(p => (p.parentPageId ?? null) === (page.parentPageId ?? null))
      .sort((a, b) => a.order - b.order);
  }

  private indexAmongSiblings(page: PageDto): number {
    return this.siblingsOf(page).findIndex(p => p.id === page.id);
  }
}
