import { Component, input, output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import * as lucide from '@ng-icons/lucide';
import type { DocumentDto } from '../docs.service';

/** Plantilla predefinida del sistema, identificada por clave. */
export interface PlantillaPredefinida {
  key: string;
  title: string;
  description: string;
  icon: string;
  iconBg: string;
}

/**
 * Elige una plantilla para crear un documento.
 *
 * A diferencia de los otros dos modales, este **no** llama al servicio: sólo comunica qué
 * se ha elegido. La creación tiene efectos que le corresponden al padre —recargar el
 * listado y abrir el documento nuevo—, y devolver ese control aquí obligaría a duplicar
 * ambos.
 */
@Component({
  selector: 'app-selector-plantilla-modal',
  standalone: true,
  imports: [NgIcon],
  providers: [provideIcons(lucide as unknown as Record<string, string>)],
  template: `
    <div class="fixed inset-0 z-50 bg-foreground/50 backdrop-blur-sm flex items-center justify-center p-4">
      <div role="dialog" aria-modal="true" aria-labelledby="titulo-plantillas"
           (keydown.escape)="cerrar.emit()"
           class="bg-card border border-border rounded-2xl shadow-2xl max-w-2xl w-full p-6">

        <div class="flex items-center justify-between mb-4">
          <div class="flex items-center gap-2">
            <div class="w-8 h-8 rounded-lg bg-primary-subtle text-primary-subtle-fg flex items-center justify-center">
              <ng-icon name="lucideWand2" class="w-4 h-4" aria-hidden="true" />
            </div>
            <h3 id="titulo-plantillas" class="text-base font-bold text-foreground" i18n>Apply a Template</h3>
          </div>
          <button type="button" (click)="cerrar.emit()"
                  i18n-aria-label aria-label="Cerrar"
                  class="text-muted-foreground hover:text-foreground">
            <ng-icon name="lucideX" class="w-4 h-4" aria-hidden="true" />
          </button>
        </div>

        <!-- Botones nativos y no divs con (click): son acciones, así que reciben foco y
             responden a Enter sin necesidad de añadir nada. -->
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 max-h-96 overflow-y-auto pr-1">
          @for (tmpl of predefinidas(); track tmpl.key) {
            <button type="button" (click)="elegirPredefinida.emit(tmpl.key)"
                    class="text-left border border-border rounded-xl p-3.5 bg-card transition-all
                           hover:border-primary focus:outline-none focus:ring-2 focus:ring-ring">
              <div class="flex items-center gap-2.5 mb-1.5">
                <div class="w-7 h-7 rounded-md flex items-center justify-center text-xs {{ tmpl.iconBg }}">
                  <ng-icon [name]="tmpl.icon" class="w-3.5 h-3.5" aria-hidden="true" />
                </div>
                <h4 class="text-xs font-bold text-foreground">{{ tmpl.title }}</h4>
              </div>
              <p class="text-[11px] text-muted-foreground">{{ tmpl.description }}</p>
            </button>
          }

          @for (propia of propias(); track propia.id) {
            <button type="button" (click)="elegirPropia.emit(propia.id)"
                    class="text-left border border-primary rounded-xl p-3.5 bg-primary-subtle/40 transition-all
                           hover:border-primary focus:outline-none focus:ring-2 focus:ring-ring">
              <div class="flex items-center gap-2.5 mb-1.5">
                <div class="w-7 h-7 rounded-md bg-primary text-primary-foreground flex items-center justify-center text-xs">
                  <ng-icon name="lucideLayoutTemplate" class="w-3.5 h-3.5" aria-hidden="true" />
                </div>
                <h4 class="text-xs font-bold text-foreground">{{ propia.title }}</h4>
              </div>
              <p class="text-[11px] text-muted-foreground">
                {{ propia.description || plantillaGuardada }}
              </p>
            </button>
          }
        </div>
      </div>
    </div>
  `,
})
export class SelectorPlantillaModalComponent {
  readonly predefinidas = input.required<PlantillaPredefinida[]>();
  /** Plantillas creadas por el equipo: documentos de tipo 4. */
  readonly propias = input.required<DocumentDto[]>();

  readonly cerrar = output<void>();
  readonly elegirPredefinida = output<string>();
  readonly elegirPropia = output<string>();

  protected readonly plantillaGuardada = $localize`Saved custom template`;
}
