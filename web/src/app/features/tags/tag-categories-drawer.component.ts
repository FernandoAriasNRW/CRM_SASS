import { Component, OnInit, computed, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { DrawerComponent } from '../../shared/ui/drawer.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { ToastService } from '../../shared/services/toast.service';
import { TagsService, type TagCategoryItem } from '../../shared/services/tags.service';
import { mensajeDeError } from '../../shared/utils/mensaje-de-error';

/**
 * Las categorías de etiquetas: las predefinidas, que sólo se ven, y las propias de la
 * organización, que se crean, renombran y borran aquí.
 *
 * Crear la puede cualquiera que cree etiquetas; renombrar y borrar, quien gestiona todas
 * (`canManage`), porque afecta a etiquetas de otras personas. Borrar sólo con la categoría vacía:
 * el servidor lo exige y aquí se explica antes de intentarlo.
 */
@Component({
  selector: 'app-tag-categories-drawer',
  standalone: true,
  imports: [FormsModule, DrawerComponent, ButtonComponent],
  templateUrl: './tag-categories-drawer.component.html',
})
export class TagCategoriesDrawerComponent implements OnInit {
  private readonly tagsService = inject(TagsService);
  private readonly toast = inject(ToastService);

  readonly closed = output<void>();

  readonly notEmptyHint = $localize`Tiene etiquetas: muévelas a otra categoría o bórralas antes`;

  readonly builtIn = computed(() => this.tagsService.categories().filter(c => !c.isCustom));
  readonly custom = computed(() => this.tagsService.categories().filter(c => c.isCustom));

  newName = '';
  readonly creating = signal(false);
  readonly createError = signal('');

  /** La categoría que se está renombrando, y el nombre que se escribe. Sólo una a la vez. */
  readonly renamingId = signal<string | null>(null);
  renameValue = '';
  readonly confirmingDeleteId = signal<string | null>(null);
  readonly busy = signal(false);
  readonly rowError = signal<{ id: string; message: string } | null>(null);

  ngOnInit(): void {
    this.tagsService.loadCategories();
  }

  create(): void {
    const name = this.newName.trim();
    if (!name || this.creating()) return;

    this.creating.set(true);
    this.createError.set('');
    this.tagsService.createCategory(name).subscribe({
      next: () => {
        this.creating.set(false);
        this.newName = '';
        this.toast.success($localize`Categoría creada`, name);
      },
      error: response => {
        this.creating.set(false);
        this.createError.set(mensajeDeError(response, $localize`No se pudo crear la categoría`));
      },
    });
  }

  startRename(category: TagCategoryItem): void {
    this.renamingId.set(category.id ?? null);
    this.renameValue = category.name;
    this.confirmingDeleteId.set(null);
    this.rowError.set(null);
  }

  cancelRename(): void {
    this.renamingId.set(null);
  }

  saveRename(category: TagCategoryItem): void {
    const name = this.renameValue.trim();
    if (!category.id || !name || this.busy()) return;
    if (name === category.name) {
      this.renamingId.set(null);
      return;
    }

    this.busy.set(true);
    this.tagsService.renameCategory(category.id, name).subscribe({
      next: () => {
        this.busy.set(false);
        this.renamingId.set(null);
        this.toast.success($localize`Categoría renombrada`, name);
      },
      error: response => {
        this.busy.set(false);
        this.rowError.set({ id: category.id!, message: mensajeDeError(response, $localize`No se pudo renombrar la categoría`) });
      },
    });
  }

  askDelete(category: TagCategoryItem): void {
    this.confirmingDeleteId.set(category.id ?? null);
    this.renamingId.set(null);
    this.rowError.set(null);
  }

  delete(category: TagCategoryItem): void {
    if (!category.id || this.busy()) return;

    this.busy.set(true);
    this.tagsService.deleteCategory(category.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.confirmingDeleteId.set(null);
        this.toast.success($localize`Categoría borrada`, category.name);
      },
      error: response => {
        this.busy.set(false);
        this.confirmingDeleteId.set(null);
        this.rowError.set({ id: category.id!, message: mensajeDeError(response, $localize`No se pudo borrar la categoría`) });
      },
    });
  }

  errorFor(category: TagCategoryItem): string {
    const error = this.rowError();
    return error && error.id === category.id ? error.message : '';
  }

  countLabel(category: TagCategoryItem): string {
    const count = category.tagCount ?? 0;
    return count === 1 ? $localize`1 etiqueta` : $localize`${count}:count: etiquetas`;
  }

  close(): void {
    this.closed.emit();
  }
}
