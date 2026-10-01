import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { DrawerComponent } from '../../shared/ui/drawer.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { BadgeComponent } from '../../shared/ui/badge.component';
import { UserAvatarComponent } from '../../shared/ui/user-avatar.component';
import { ToastService } from '../../shared/services/toast.service';
import { TagsService, type TagCategoryItem, type TagInput } from '../../shared/services/tags.service';
import { errorMessage } from '../../shared/utils/error-message';
import { type TagRow } from './tag-list';

/** Los colores que se ofrecen de un clic. Cualquier otro se puede escribir o elegir en el selector. */
export const TAG_SWATCHES: readonly string[] = [
  '#EF4444', '#F97316', '#F59E0B', '#84CC16', '#10B981', '#06B6D4',
  '#3B82F6', '#6366F1', '#8B5CF6', '#EC4899', '#6B7280', '#0F172A',
];

const DEFAULT_COLOR = '#6B7280';
const HEX_COLOR = /^#[0-9A-Fa-f]{6}$/;

/**
 * El detalle de una etiqueta, en un cajón: qué es, de dónde sale y, si se puede, editarla o
 * borrarla. Sin etiqueta, es el alta.
 *
 * Qué se puede tocar lo decide el servidor (`canManage`): quien la creó, un administrador o quien
 * tenga el permiso. Aquí sólo se refleja, y se explica por qué no cuando no se puede, en vez de
 * enseñar un formulario que va a dar 403 al guardar.
 */
@Component({
  selector: 'app-tag-drawer',
  standalone: true,
  imports: [FormsModule, DrawerComponent, ButtonComponent, BadgeComponent, UserAvatarComponent],
  templateUrl: './tag-drawer.component.html',
})
export class TagDrawerComponent implements OnInit {
  private readonly tagsService = inject(TagsService);
  private readonly toast = inject(ToastService);

  /** La etiqueta que se mira, o `null` para crear una. */
  readonly tag = input<TagRow | null>(null);
  readonly closed = output<void>();

  readonly swatches = TAG_SWATCHES;

  name = '';
  colorHex = DEFAULT_COLOR;
  category = '';

  readonly saving = signal(false);
  readonly error = signal('');
  readonly confirmingDelete = signal(false);

  readonly isNew = computed(() => this.tag() === null);
  readonly editable = computed(() => this.isNew() || (this.tag()?.canManage ?? false));

  /** Las que admiten etiquetas a mano: todas menos equipos y proyectos. */
  readonly selectableCategories = computed<TagCategoryItem[]>(() => {
    const categories = this.tagsService.categories().filter(c => !c.isAutomatic);
    const current = this.tag();
    // Si la suya no está en la lista (una propia que ya no existe), se enseña igualmente: si no,
    // el desplegable saldría en blanco y parecería que no tiene categoría.
    if (current && !categories.some(c => c.name === current.category) && !this.isAutomatic()) {
      return [...categories, { name: current.category, label: current.categoryLabel, isCustom: true, isAutomatic: false }];
    }
    return categories;
  });

  readonly title = computed(() => this.tag()?.name ?? $localize`Nueva etiqueta`);
  readonly subtitle = computed(() => this.tag()?.categoryLabel ?? $localize`Nombre, categoría y color`);

  ngOnInit(): void {
    this.tagsService.loadCategories();

    const tag = this.tag();
    if (tag) {
      this.name = tag.name;
      this.colorHex = tag.colorHex || DEFAULT_COLOR;
      this.category = tag.category;
    }
  }

  isAutomatic(): boolean {
    return this.tag()?.kind === 'automatic';
  }

  /** Por qué no se puede editar, dicho a quien lo intenta. */
  readOnlyReason(): string {
    return this.isAutomatic()
      ? $localize`Esta etiqueta sigue a su equipo o proyecto: se crea con él y no se edita a mano.`
      : $localize`Sólo quien la creó, un administrador o alguien con permiso para gestionar etiquetas puede cambiarla o borrarla.`;
  }

  canSave(): boolean {
    return this.editable() && !this.saving() && this.name.trim().length > 0
      && this.category.length > 0 && HEX_COLOR.test(this.colorHex);
  }

  pickColor(color: string): void {
    this.colorHex = color;
  }

  save(): void {
    if (!this.canSave()) return;

    const input: TagInput = { name: this.name.trim(), colorHex: this.colorHex.toUpperCase(), category: this.category };
    const tag = this.tag();
    const request = tag ? this.tagsService.update(tag.id, input) : this.tagsService.create(input);

    this.saving.set(true);
    this.error.set('');
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(tag ? $localize`Etiqueta guardada` : $localize`Etiqueta creada`, input.name);
        this.closed.emit();
      },
      error: response => {
        this.saving.set(false);
        this.error.set(errorMessage(response, $localize`No se pudo guardar la etiqueta`));
      },
    });
  }

  remove(): void {
    const tag = this.tag();
    if (!tag) return;

    this.saving.set(true);
    this.error.set('');
    this.tagsService.remove(tag.id).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success($localize`Etiqueta borrada`, tag.name);
        this.closed.emit();
      },
      error: response => {
        this.saving.set(false);
        this.confirmingDelete.set(false);
        this.error.set(errorMessage(response, $localize`No se pudo borrar la etiqueta`));
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
