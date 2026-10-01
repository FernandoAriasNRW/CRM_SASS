import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { Observable } from 'rxjs';

import { DrawerComponent } from './drawer.component';
import { ButtonComponent } from './button.component';
import { TagFieldComponent } from './tag-field.component';
import { ToastService } from '../services/toast.service';
import { mensajeDeError } from '../utils/mensaje-de-error';

/**
 * Un cajón para elegir las etiquetas de algo que no tiene ficha propia donde ponerlas: un informe
 * o un panel. Se marcan con el mismo selector que tareas y tickets y se guardan al pulsar
 * «Guardar», todas de una vez.
 *
 * Cómo se guardan lo decide quien lo abre (`save`): cada módulo tiene su endpoint. Si el servidor
 * rechaza, el cajón sigue abierto con lo marcado y el motivo.
 */
@Component({
  selector: 'app-tag-assign-drawer',
  standalone: true,
  imports: [DrawerComponent, ButtonComponent, TagFieldComponent],
  template: `
    <app-drawer [isOpen]="true" (closed)="closed.emit()" [title]="title()" i18n-subtitle subtitle="Etiquetas" size="md">
      <div class="p-6 space-y-4">
        <app-tag-field [tagIds]="selected()" (tagIdsChange)="selected.set($event)" />
        @if (error()) {
          <p role="alert" class="text-sm text-destructive" data-testid="tag-assign-error">{{ error() }}</p>
        }
      </div>
      <div drawer-footer class="flex justify-end gap-2 w-full">
        <button uiButton variant="outline" size="sm" (click)="closed.emit()" i18n>Cancelar</button>
        <button uiButton size="sm" (click)="submit()" [disabled]="saving()" data-testid="tag-assign-save" i18n>Guardar</button>
      </div>
    </app-drawer>
  `,
})
export class TagAssignDrawerComponent implements OnInit {
  private readonly toast = inject(ToastService);

  readonly title = input.required<string>();
  readonly tagIds = input<readonly string[] | null | undefined>([]);
  /** Cómo se guardan. Recibe la lista entera. */
  readonly save = input.required<(tagIds: string[]) => Observable<unknown>>();

  readonly closed = output<void>();
  readonly saved = output<string[]>();

  readonly selected = signal<string[]>([]);
  readonly saving = signal(false);
  readonly error = signal('');

  ngOnInit(): void {
    this.selected.set([...(this.tagIds() ?? [])]);
  }

  submit(): void {
    const ids = this.selected();
    this.saving.set(true);
    this.error.set('');
    this.save()(ids).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success($localize`Etiquetas guardadas`, this.title());
        this.saved.emit(ids);
        this.closed.emit();
      },
      error: response => {
        this.saving.set(false);
        this.error.set(mensajeDeError(response, $localize`No se pudieron guardar las etiquetas`));
      },
    });
  }
}
