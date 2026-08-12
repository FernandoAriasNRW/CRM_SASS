import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideUpload, lucideX } from '@ng-icons/lucide';
import { DocsService } from '../docs.service';

/** Tipo de documento que espera la API: 1 List, 2 Wiki, 3 MeetingNote, 4 Template. */
const TIPO_DOCUMENTO = 1;

/**
 * Importa un documento desde un fichero de texto.
 *
 * Lee el fichero en el navegador y envía su contenido; no sube el fichero en sí. Eso
 * permite revisar y editar lo importado antes de crear nada, que es lo que evita acabar
 * con documentos a medias cuando el fichero no era lo que se creía.
 */
@Component({
  selector: 'app-importar-documento-modal',
  standalone: true,
  imports: [FormsModule, NgIcon],
  viewProviders: [provideIcons({ lucideUpload, lucideX })],
  template: `
    <div class="fixed inset-0 z-50 bg-foreground/50 backdrop-blur-sm flex items-center justify-center p-4">
      <div role="dialog" aria-modal="true" aria-labelledby="titulo-importar"
           (keydown.escape)="cerrar.emit()"
           class="bg-card border border-border rounded-2xl shadow-2xl max-w-lg w-full p-6">

        <div class="flex items-center justify-between mb-4">
          <div class="flex items-center gap-2">
            <div class="w-8 h-8 rounded-lg bg-primary-subtle text-primary-subtle-fg flex items-center justify-center">
              <ng-icon name="lucideUpload" class="w-4 h-4" aria-hidden="true" />
            </div>
            <h3 id="titulo-importar" class="text-base font-bold text-foreground" i18n>Import Document</h3>
          </div>
          <button type="button" (click)="cerrar.emit()"
                  i18n-aria-label aria-label="Cerrar"
                  class="text-muted-foreground hover:text-foreground">
            <ng-icon name="lucideX" class="w-4 h-4" aria-hidden="true" />
          </button>
        </div>

        <div class="space-y-4">
          <div>
            <label for="importar-fichero" class="block text-xs font-medium text-muted-foreground mb-1" i18n>
              Select File (.md, .txt, .html)
            </label>
            <input id="importar-fichero" type="file" accept=".md,.txt,.html"
                   (change)="alElegirFichero($event)"
                   class="w-full text-xs text-muted-foreground cursor-pointer
                          file:mr-4 file:py-2 file:px-4 file:rounded-lg file:border-0
                          file:text-xs file:font-semibold file:bg-primary-subtle
                          file:text-primary-subtle-fg" />
          </div>

          <div>
            <label for="importar-titulo" class="block text-xs font-medium text-muted-foreground mb-1" i18n>Document Title</label>
            <input id="importar-titulo" type="text" [(ngModel)]="titulo"
                   i18n-placeholder placeholder="Document title..."
                   class="w-full px-3 py-2 text-sm bg-muted border border-border rounded-lg
                          focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>

          <div>
            <label for="importar-contenido" class="block text-xs font-medium text-muted-foreground mb-1" i18n>
              Content Preview / Paste Text
            </label>
            <textarea id="importar-contenido" rows="5" [(ngModel)]="contenido"
                      i18n-placeholder placeholder="Paste raw Markdown, HTML, or plain text here..."
                      class="w-full px-3 py-2 text-xs font-mono bg-muted border border-border rounded-lg
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
          <button type="button" (click)="importar()" [disabled]="importando()"
                  class="px-4 py-2 text-xs font-medium text-primary-foreground bg-primary rounded-lg shadow-sm
                         hover:bg-primary/90 disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-ring">
            @if (importando()) { <ng-container i18n>Saving...</ng-container> }
            @else { <ng-container i18n>Import Now</ng-container> }
          </button>
        </div>
      </div>
    </div>
  `,
})
export class ImportarDocumentoModalComponent {
  private readonly docsService = inject(DocsService);

  readonly cerrar = output<void>();
  /** Id del documento creado, para que el padre lo abra. */
  readonly importado = output<string>();

  protected titulo = '';
  protected contenido = '';
  protected readonly importando = signal(false);
  protected readonly error = signal('');

  protected alElegirFichero(evento: Event): void {
    const fichero = (evento.target as HTMLInputElement).files?.[0];
    if (!fichero) return;

    // El nombre del fichero, sin extensión, es un título de partida razonable: casi
    // siempre es el que quiere quien importa, y sigue siendo editable.
    this.titulo = fichero.name.replace(/\.[^/.]+$/, '');

    const lector = new FileReader();
    lector.onload = e => this.contenido = (e.target?.result as string) ?? '';
    lector.onerror = () => this.error.set($localize`No se pudo leer el fichero.`);
    lector.readAsText(fichero);
  }

  protected importar(): void {
    if (!this.titulo.trim() || !this.contenido.trim()) {
      this.error.set($localize`Hacen falta un título y contenido para importar.`);
      return;
    }

    this.importando.set(true);
    this.docsService
      .importDocument({ title: this.titulo, content: this.contenido, type: TIPO_DOCUMENTO })
      .subscribe({
        next: id => {
          this.importando.set(false);
          this.importado.emit(id);
        },
        error: () => {
          this.importando.set(false);
          this.error.set($localize`No se pudo importar el documento. Inténtalo de nuevo.`);
        },
      });
  }
}
