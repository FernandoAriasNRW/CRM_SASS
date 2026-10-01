import { Component, inject, input, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideLoader2, lucideCircleAlert } from '@ng-icons/lucide';
import {
  CustomFieldsService, MULTI_SEPARATOR, type CustomFieldValue,
} from '../../core/custom-fields.service';
import { errorMessage } from '../utils/error-message';
import { UsersService } from '../../core/users.service';
import { SkeletonComponent } from './skeleton.component';

/**
 * Pinta los campos personalizados de una entidad y guarda cada uno al cambiarlo.
 *
 * Se guarda campo a campo y no con un botón «guardar todo»: el resto del detalle de tarea
 * funciona así, y mezclar los dos modos en la misma pantalla haría dudar de si lo escrito
 * quedó guardado.
 *
 * **Los errores del servidor se muestran junto al campo**, no como un aviso genérico. El
 * backend valida y explica por qué —«no es un número», «no está entre las opciones»— y esa
 * frase sólo sirve si se lee al lado de la casilla que la provocó. Y el valor se revierte:
 * dejar en pantalla lo que el servidor rechazó es la clase de mentira que este proyecto ya
 * corrigió en tableros y prioridad.
 */
@Component({
  selector: 'app-custom-fields-form',
  standalone: true,
  imports: [FormsModule, NgIconComponent, SkeletonComponent],
  viewProviders: [provideIcons({ lucideLoader2, lucideCircleAlert })],
  templateUrl: './custom-fields-form.component.html',
})
export class CustomFieldsFormComponent implements OnInit {
  /** «Tarea» o «Proyecto». */
  readonly entity = input.required<string>();
  readonly entityId = input.required<string>();

  private readonly service = inject(CustomFieldsService);
  private readonly users = inject(UsersService);

  readonly fields = signal<CustomFieldValue[]>([]);
  readonly loading = signal(false);
  readonly saving = signal<string | null>(null);
  readonly errors = signal<Record<string, string>>({});

  /** Copia de lo último guardado, para poder revertir si el servidor rechaza. */
  private lastValid: Record<string, string | null> = {};

  ngOnInit(): void {
    this.load();
    if (!this.users.users().length) this.users.loadTenantUsers().subscribe();
  }

  get people() { return this.users.users(); }

  load(): void {
    this.loading.set(true);
    this.service.valuesOf(this.entity(), this.entityId()).subscribe({
      next: fields => {
        this.fields.set(fields ?? []);
        this.lastValid = Object.fromEntries((fields ?? []).map(c => [c.definitionId, c.value]));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  /** Lo marcado en una selección múltiple. */
  isChecked(field: CustomFieldValue, option: string): boolean {
    return (field.value ?? '').split(MULTI_SEPARATOR).includes(option);
  }

  toggleOption(field: CustomFieldValue, option: string): void {
    const current = (field.value ?? '').split(MULTI_SEPARATOR).filter(Boolean);
    const updated = current.includes(option)
      ? current.filter(o => o !== option)
      : [...current, option];

    this.save(field, updated.join(MULTI_SEPARATOR));
  }

  /**
   * Guarda un valor y deja el error del servidor junto al campo si lo rechaza.
   */
  save(field: CustomFieldValue, value: string | null): void {
    const previous = this.lastValid[field.definitionId] ?? null;
    const clean = value === '' ? null : value;

    this.applyOnScreen(field.definitionId, clean);
    this.saving.set(field.definitionId);
    this.clearError(field.definitionId);

    this.service.saveValue(field.definitionId, this.entityId(), clean).subscribe({
      next: () => {
        this.lastValid[field.definitionId] = clean;
        this.saving.set(null);
      },
      error: response => {
        this.applyOnScreen(field.definitionId, previous);
        this.saving.set(null);
        this.errors.update(current => ({
          ...current,
          [field.definitionId]: errorMessage(response, $localize`No se pudo guardar el valor`),
        }));
      },
    });
  }

  private applyOnScreen(definitionId: string, value: string | null): void {
    this.fields.update(fields => fields.map(c => c.definitionId === definitionId ? { ...c, value } : c));
  }

  private clearError(definitionId: string): void {
    this.errors.update(current => {
      const copy = { ...current };
      delete copy[definitionId];
      return copy;
    });
  }

  userName(id: string | null): string {
    if (!id) return '';
    return this.users.getUser(id)?.name ?? `${id.slice(0, 8)}…`;
  }
}
