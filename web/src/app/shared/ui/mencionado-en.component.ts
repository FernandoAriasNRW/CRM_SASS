import { Component, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { MencionesService, type MentioningDocument } from '../../features/docs/menciones.service';

/**
 * «Mencionado en»: los documentos que hablan de esta tarea, ticket o proyecto.
 *
 * <b>Es la vuelta del diferencial.</b> El plan lo dice así: «mencionar un ticket dentro de un
 * documento y que el ticket muestre el documento es algo que ClickUp hace a medias». Escribir la
 * mención lo hace el editor; esto es la otra mitad, y es la que da valor: quien abre una tarea ve
 * en qué actas, especificaciones o notas se ha hablado de ella sin tener que buscarlas.
 *
 * <b>La sección se enseña siempre, también vacía.</b> Un apartado que aparece y desaparece según
 * los datos hace pensar que la aplicación se comporta distinto cada día; y estando siempre, «nadie
 * ha escrito sobre esto» es una respuesta, no un hueco.
 */
@Component({
  selector: 'app-mencionado-en',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="space-y-2">
      <h4 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground" i18n>
        Mencionado en
      </h4>

      @if (cargando()) {
        <p class="text-sm text-muted-foreground" i18n>Buscando…</p>
      } @else if (documentos().length === 0) {
        <p class="text-sm text-muted-foreground" i18n>
          Ningún documento habla de esto todavía.
        </p>
      } @else {
        <ul class="space-y-1.5">
          @for (d of documentos(); track d.pageId) {
            <li>
              <a [routerLink]="['/docs']" [queryParams]="{ doc: d.documentId, page: d.pageId }"
                 class="block rounded-md px-2 py-1.5 hover:bg-secondary transition-colors">
                <span class="text-sm font-medium">{{ d.pageTitle }}</span>

                @if (d.documentTitle !== d.pageTitle) {
                  <span class="text-xs text-muted-foreground"> · {{ d.documentTitle }}</span>
                }

                <!--
                  Cómo estaba escrita la mención. Da contexto —«el bloqueo de la migración» dice
                  más que el título del documento— y sigue diciéndolo aunque el documento se
                  renombre después.
                -->
                <span class="block text-xs text-muted-foreground truncate">«{{ d.visibleText }}»</span>
              </a>
            </li>
          }
        </ul>
      }
    </div>
  `
})
export class MencionadoEnComponent {
  private readonly menciones = inject(MencionesService);

  /** Uno de los tipos mencionables: «Tarea», «Ticket», «Proyecto». */
  readonly tipo = input.required<string>();
  readonly entidadId = input.required<string>();

  readonly documentos = signal<MentioningDocument[]>([]);
  readonly cargando = signal(true);

  constructor() {
    // Se carga una vez al abrir. No se refresca solo: quien está mirando una tarea no espera que
    // esta lista cambie sola, y un sondeo aquí sería una consulta más cada pocos segundos por
    // cada panel abierto.
    queueMicrotask(() => void this.cargar());
  }

  private async cargar(): Promise<void> {
    try {
      this.documentos.set(await firstValueFrom(
        this.menciones.quienMenciona(this.tipo(), this.entidadId())));
    } catch {
      // Que esto falle no puede estropear el panel de la tarea: es información añadida, no el
      // contenido. Se queda vacío y lo demás sigue funcionando.
      this.documentos.set([]);
    } finally {
      this.cargando.set(false);
    }
  }
}
