import { Component, inject, input, output, effect, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideLayoutTemplate, lucideX } from '@ng-icons/lucide';
import { DocsService, type DocumentDto } from '../docs.service';

/**
 * Convierte un documento en plantilla reutilizable.
 *
 * Se lleva su propia llamada al servicio en lugar de emitir los datos al padre. Es lo que
 * hace que extraerlo sirva de algo: si sólo moviera el HTML, el componente de documentos
 * seguiría cargando con la lógica y sólo habría cambiado de sitio el desorden.
 */
@Component({
  selector: 'app-guardar-plantilla-modal',
  standalone: true,
  imports: [FormsModule, NgIcon],
  viewProviders: [provideIcons({ lucideLayoutTemplate, lucideX })],
  template: `
    <div class="fixed inset-0 z-50 bg-foreground/50 backdrop-blur-sm flex items-center justify-center p-4">
      <div role="dialog" aria-modal="true" aria-labelledby="titulo-guardar-plantilla"
           (keydown.escape)="cerrar.emit()"
           class="bg-card border border-border rounded-2xl shadow-2xl max-w-md w-full p-6">

        <div class="flex items-center justify-between mb-4">
          <div class="flex items-center gap-2">
            <div class="w-8 h-8 rounded-lg bg-primary-subtle text-primary-subtle-fg flex items-center justify-center">
              <ng-icon name="lucideLayoutTemplate" class="w-4 h-4" aria-hidden="true" />
            </div>
            <h3 id="titulo-guardar-plantilla" class="text-base font-bold text-foreground" i18n>Save as Template</h3>
          </div>
          <button type="button" (click)="cerrar.emit()"
                  i18n-aria-label aria-label="Cerrar"
                  class="text-muted-foreground hover:text-foreground">
            <ng-icon name="lucideX" class="w-4 h-4" aria-hidden="true" />
          </button>
        </div>

        <p class="text-xs text-muted-foreground mb-4" i18n>
          Convert this document into a reusable template for your workspace.
        </p>

        <div class="space-y-3">
          <div>
            <label for="plantilla-titulo" class="block text-xs font-medium text-muted-foreground mb-1" i18n>Template Title</label>
            <input id="plantilla-titulo" type="text" [(ngModel)]="titulo"
                   class="w-full px-3 py-2 text-sm bg-muted border border-border rounded-lg
                          focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>

          <div>
            <label for="plantilla-descripcion" class="block text-xs font-medium text-muted-foreground mb-1" i18n>Description</label>
            <textarea id="plantilla-descripcion" rows="2" [(ngModel)]="descripcion"
                      i18n-placeholder placeholder="Briefly describe what this template is used for..."
                      class="w-full px-3 py-2 text-sm bg-muted border border-border rounded-lg
                             focus:outline-none focus:ring-2 focus:ring-ring"></textarea>
          </div>
        </div>

        @if (error()) {
          <p role="alert" class="mt-3 text-xs text-destructive">{{ error() }}</p>
        }

        <div class="flex items-center justify-end gap-2 mt-6">
          <button type="button" (click)="cerrar.emit()"
                  class="px-4 py-2 text-xs font-medium text-muted-foreground hover:bg-muted rounded-lg
                         focus:outline-none focus:ring-2 focus:ring-ring" i18n>
            Cancel
          </button>
          <button type="button" (click)="guardar()" [disabled]="guardando()"
                  class="px-4 py-2 text-xs font-medium text-primary-foreground bg-primary rounded-lg shadow-sm
                         hover:bg-primary/90 disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-ring">
            @if (guardando()) { <ng-container i18n>Saving...</ng-container> }
            @else { <ng-container i18n>Save Template</ng-container> }
          </button>
        </div>
      </div>
    </div>
  `,
})
export class GuardarPlantillaModalComponent {
  private readonly docsService = inject(DocsService);

  /** Documento de origen. El modal sólo se muestra cuando hay uno. */
  readonly documento = input.required<DocumentDto>();

  readonly cerrar = output<void>();
  /** Se emite tras guardar, para que el padre recargue el listado. */
  readonly guardado = output<void>();

  protected titulo = '';
  protected descripcion = '';
  protected readonly guardando = signal(false);
  protected readonly error = signal('');

  constructor() {
    // Los campos se rellenan a partir del documento en cuanto llega, no en el padre:
    // así el modal es utilizable desde cualquier sitio sin preparar nada antes.
    effect(() => {
      const doc = this.documento();
      this.titulo = `${doc.title} Template`;
      this.descripcion = doc.description ?? '';
      this.error.set('');
    });
  }

  protected guardar(): void {
    if (!this.titulo.trim()) {
      this.error.set($localize`El título es obligatorio`);
      return;
    }

    this.guardando.set(true);
    this.docsService
      .saveAsTemplate(this.documento().id, {
        customTitle: this.titulo,
        description: this.descripcion,
      })
      .subscribe({
        next: () => {
          this.guardando.set(false);
          this.guardado.emit();
        },
        // El error se muestra dentro del modal en lugar de un `alert`, que bloquea la
        // página y no dice en qué campo está el problema.
        error: () => {
          this.guardando.set(false);
          this.error.set($localize`No se pudo guardar la plantilla. Inténtalo de nuevo.`);
        },
      });
  }
}
