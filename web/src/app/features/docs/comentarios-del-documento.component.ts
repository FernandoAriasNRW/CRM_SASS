import { Component, computed, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck, lucideMessageSquare, lucideRotateCcw, lucideTrash2 } from '@ng-icons/lucide';
import { ComentariosComponent } from '../../shared/ui/comentarios.component';
import type { AnotacionDto } from './docs.service';

/**
 * El panel de comentarios en línea de una página.
 *
 * <b>El hilo lo pinta el componente que ya existe</b> —el mismo de tareas, tickets y proyectos—
 * pasándole `Anotacion` como entidad. Comentar es la misma operación con las mismas reglas: quién
 * edita, quién borra, un solo nivel de respuestas. Escribir aquí un hilo propio habría sido un
 * cuarto sitio donde arreglar el mismo fallo.
 *
 * Lo que sí es de aquí es <b>el anclaje</b>: qué se citó, si está resuelto, y llevar a quien lo
 * lea hasta el trozo de texto señalado.
 *
 * Las resueltas se esconden detrás de un contador en vez de quitarse. Un comentario que
 * desaparece al marcarlo como atendido se lleva por delante el motivo del cambio, y en una
 * revisión el motivo es justo lo que se busca semanas después.
 */
@Component({
  selector: 'app-comentarios-del-documento',
  standalone: true,
  imports: [DatePipe, NgIcon, ComentariosComponent],
  viewProviders: [provideIcons({ lucideCheck, lucideMessageSquare, lucideRotateCcw, lucideTrash2 })],
  template: `
    <div class="flex flex-col gap-3 min-w-0">
      <div class="flex items-center justify-between gap-2 px-1">
        <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground" i18n>
          Comentarios
        </span>

        @if (resueltas().length > 0) {
          <button type="button" (click)="verResueltas.set(!verResueltas())"
                  class="text-xs text-primary hover:underline focus:outline-none focus:ring-2
                         focus:ring-ring rounded px-1">
            {{ verResueltas() ? ocultarResueltas : textoDeResueltas() }}
          </button>
        }
      </div>

      @if (abiertas().length === 0 && !verResueltas()) {
        <p class="text-xs text-muted-foreground px-1" i18n>
          Selecciona texto y pulsa el bocadillo para comentar sobre él.
        </p>
      }

      @for (anotacion of visibles(); track anotacion.id) {
        <article class="rounded-lg border border-border bg-card p-3 flex flex-col gap-2"
                 [class.opacity-60]="!!anotacion.resueltaUtc"
                 [class.border-primary]="anotacion.id === anotacionActivaId()">

          <div class="flex items-start justify-between gap-2">
            <!--
              El texto citado sale de la anotación, no del documento. Si alguien reescribe el
              párrafo, el panel sigue pudiendo decir sobre qué se comentó.
            -->
            <button type="button" (click)="irA.emit(anotacion)"
                    class="flex-1 min-w-0 text-left focus:outline-none focus:ring-2 focus:ring-ring rounded">
              <p class="text-xs text-muted-foreground border-l-2 border-warning pl-2 line-clamp-3 italic">
                {{ anotacion.textoCitado }}
              </p>
            </button>

            <div class="flex items-center gap-0.5 shrink-0">
              <button type="button" (click)="resolver.emit(anotacion)"
                      [title]="anotacion.resueltaUtc ? reabrir : darPorResuelta"
                      [attr.aria-label]="anotacion.resueltaUtc ? reabrir : darPorResuelta"
                      class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-accent
                             focus:outline-none focus:ring-2 focus:ring-ring">
                <ng-icon [name]="anotacion.resueltaUtc ? 'lucideRotateCcw' : 'lucideCheck'"
                         class="w-3.5 h-3.5" aria-hidden="true" />
              </button>

              <button type="button" (click)="borrar.emit(anotacion)"
                      i18n-title title="Quitar el comentario"
                      i18n-aria-label aria-label="Quitar el comentario"
                      class="p-1 rounded text-muted-foreground hover:text-destructive hover:bg-accent
                             focus:outline-none focus:ring-2 focus:ring-ring">
                <ng-icon name="lucideTrash2" class="w-3.5 h-3.5" aria-hidden="true" />
              </button>
            </div>
          </div>

          @if (anotacion.resueltaUtc; as cuando) {
            <p class="text-[11px] text-muted-foreground flex items-center gap-1">
              <ng-icon name="lucideCheck" class="w-3 h-3" aria-hidden="true" />
              <span i18n>Resuelto el {{ cuando | date:'d MMM, HH:mm' }}</span>
            </p>
          }

          <app-comentarios entidad="Anotacion" [entityId]="anotacion.id" />
        </article>
      }
    </div>
  `
})
export class ComentariosDelDocumentoComponent {
  readonly anotaciones = input.required<readonly AnotacionDto[]>();

  /** Cuál está señalada ahora mismo en el texto, para destacarla en el panel. */
  readonly anotacionActivaId = input<string | null>(null);

  readonly irA = output<AnotacionDto>();
  readonly resolver = output<AnotacionDto>();
  readonly borrar = output<AnotacionDto>();

  protected readonly verResueltas = signal(false);

  protected readonly reabrir = $localize`Volver a abrir`;
  protected readonly darPorResuelta = $localize`Dar por resuelta`;
  protected readonly ocultarResueltas = $localize`Ocultar las resueltas`;

  protected readonly abiertas = computed(() => this.anotaciones().filter(a => !a.resueltaUtc));
  protected readonly resueltas = computed(() => this.anotaciones().filter(a => !!a.resueltaUtc));

  protected readonly visibles = computed(() =>
    this.verResueltas() ? [...this.abiertas(), ...this.resueltas()] : this.abiertas());

  protected textoDeResueltas(): string {
    const cuantas = this.resueltas().length;
    return cuantas === 1
      ? $localize`Ver 1 resuelta`
      : $localize`Ver ${cuantas} resueltas`;
  }
}
