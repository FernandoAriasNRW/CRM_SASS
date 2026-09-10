import { Component, ElementRef, computed, effect, input, output, signal, viewChild } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideImage, lucideLink, lucidePaperclip, lucideX, lucideYoutube } from '@ng-icons/lucide';

/** Qué se está insertando. Cambia el título, el icono y lo que se acepta. */
export type ClaseDeUrl = 'imagen' | 'video' | 'adjunto' | 'enlace';

/**
 * Pide una dirección web para insertar algo en el documento.
 *
 * <b>Sustituye a dos `window.prompt`.</b> El menú `/` pedía así la dirección de una imagen y la de
 * un vídeo: bloquea la pestaña entera, no se puede dar estilo, no valida nada, y quien lo usa sale
 * del editor y vuelve sin saber si pasó algo.
 *
 * Valida antes de aceptar, y valida lo que importa: que sea una dirección con esquema `http` o
 * `https`. Un `javascript:` escrito aquí acabaría dentro del documento y se ejecutaría al pulsarlo
 * —el nodo de adjunto lo pinta como un enlace de verdad—, así que no basta con mirar si el texto
 * parece una dirección.
 */
@Component({
  selector: 'app-pedir-url-modal',
  standalone: true,
  imports: [NgIcon],
  viewProviders: [provideIcons({ lucideImage, lucideLink, lucidePaperclip, lucideX, lucideYoutube })],
  template: `
    <div class="fixed inset-0 z-50 bg-foreground/50 backdrop-blur-sm flex items-center justify-center p-4">
      <div role="dialog" aria-modal="true" aria-labelledby="titulo-pedir-url"
           (keydown.escape)="cancelar.emit()"
           class="bg-card border border-border rounded-2xl shadow-2xl max-w-lg w-full p-6">

        <div class="flex items-center justify-between mb-4">
          <div class="flex items-center gap-2">
            <div class="w-8 h-8 rounded-lg bg-primary-subtle text-primary-subtle-fg flex items-center justify-center">
              <ng-icon [name]="icono()" class="w-4 h-4" aria-hidden="true" />
            </div>
            <h3 id="titulo-pedir-url" class="text-base font-bold text-foreground">{{ titulo() }}</h3>
          </div>
          <button type="button" (click)="cancelar.emit()"
                  i18n-aria-label aria-label="Cerrar"
                  class="text-muted-foreground hover:text-foreground">
            <ng-icon name="lucideX" class="w-4 h-4" aria-hidden="true" />
          </button>
        </div>

        <form (submit)="aceptar($event)" class="space-y-4">
          <div>
            <label for="pedir-url-campo" class="block text-xs font-medium text-muted-foreground mb-1" i18n>
              Dirección web
            </label>
            <input #campo id="pedir-url-campo" type="url" [value]="valor()"
                   (input)="alEscribir($event)"
                   placeholder="https://"
                   [attr.aria-invalid]="error() ? 'true' : null"
                   [attr.aria-describedby]="error() ? 'pedir-url-error' : null"
                   class="w-full px-3 py-2 text-sm bg-muted border border-border rounded-lg
                          focus:outline-none focus:ring-2 focus:ring-ring" />

            @if (error(); as mensaje) {
              <p id="pedir-url-error" role="alert" class="mt-1.5 text-xs text-destructive">{{ mensaje }}</p>
            }

            <p class="mt-1.5 text-xs text-muted-foreground">{{ ayuda() }}</p>
          </div>

          <div class="flex items-center justify-end gap-2">
            <button type="button" (click)="cancelar.emit()"
                    class="px-3 py-2 text-sm rounded-lg text-muted-foreground hover:bg-accent
                           focus:outline-none focus:ring-2 focus:ring-ring" i18n>
              Cancelar
            </button>
            <button type="submit"
                    class="px-3 py-2 text-sm font-medium rounded-lg bg-primary text-primary-foreground
                           hover:opacity-90 focus:outline-none focus:ring-2 focus:ring-ring" i18n>
              Insertar
            </button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class PedirUrlModalComponent {
  readonly clase = input.required<ClaseDeUrl>();

  readonly cancelar = output<void>();
  readonly aceptado = output<string>();

  protected readonly valor = signal('');
  protected readonly error = signal<string | null>(null);

  private readonly campo = viewChild<ElementRef<HTMLInputElement>>('campo');

  constructor() {
    // El foco entra solo: quien acaba de escribir «/imagen» está escribiendo, y obligarle a pulsar
    // en el campo rompe el ritmo. `autofocus` no vale porque el elemento se crea después de la
    // carga y el navegador ya no lo mira.
    effect(() => this.campo()?.nativeElement.focus());
  }

  protected readonly titulo = computed(() => {
    switch (this.clase()) {
      case 'imagen': return $localize`Insertar imagen`;
      case 'video': return $localize`Insertar vídeo de YouTube`;
      case 'enlace': return $localize`Enlazar`;
      default: return $localize`Insertar adjunto`;
    }
  });

  protected readonly ayuda = computed(() => {
    switch (this.clase()) {
      case 'imagen': return $localize`Pega la dirección de una imagen ya publicada.`;
      case 'video': return $localize`Pega el enlace del vídeo tal como aparece en YouTube.`;
      case 'enlace': return $localize`El texto seleccionado llevará a esta dirección.`;
      default: return $localize`Pega la dirección del fichero. Se enseñará como una tarjeta.`;
    }
  });

  protected readonly icono = computed(() => {
    switch (this.clase()) {
      case 'imagen': return 'lucideImage';
      case 'video': return 'lucideYoutube';
      case 'enlace': return 'lucideLink';
      default: return 'lucidePaperclip';
    }
  });

  protected alEscribir(evento: Event): void {
    this.valor.set((evento.target as HTMLInputElement).value);
    if (this.error()) this.error.set(null);
  }

  protected aceptar(evento: Event): void {
    evento.preventDefault();

    const texto = this.valor().trim();
    if (!texto) {
      this.error.set($localize`Hace falta una dirección.`);
      return;
    }

    if (!esDireccionSegura(texto)) {
      this.error.set($localize`Sólo se admiten direcciones http:// o https://`);
      return;
    }

    this.aceptado.emit(texto);
  }
}

/**
 * Si la dirección se puede meter en el documento sin riesgo.
 *
 * Sólo `http` y `https`. `javascript:` y `data:` acabarían dentro del contenido guardado y se
 * ejecutarían al pulsarlos, y el contenido de un documento lo puede ver todo el equipo.
 */
function esDireccionSegura(texto: string): boolean {
  try {
    const url = new URL(texto);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}
