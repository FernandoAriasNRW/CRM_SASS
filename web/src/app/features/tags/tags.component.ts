import { Component, OnInit, TemplateRef, ViewChild, computed, inject, signal } from '@angular/core';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucidePlus, lucideRefreshCw, lucideFolderPlus } from '@ng-icons/lucide';

import { ButtonComponent } from '../../shared/ui/button.component';
import { UserAvatarComponent } from '../../shared/ui/user-avatar.component';
import { DataTableComponent, type ColumnDef, type TableState } from '../../shared/ui/data-table/data-table.component';
import { TableColumnService } from '../../shared/services/table-column.service';
import { TagsService } from '../../shared/services/tags.service';
import { TagDrawerComponent } from './tag-drawer.component';
import { TagCategoriesDrawerComponent } from './tag-categories-drawer.component';
import { type TagRow, toRows, visibleRows } from './tag-list';

/**
 * Las etiquetas de la organización: una lista como las de tareas o tickets, y al pulsar una fila
 * un cajón con el detalle, donde se edita o se borra.
 *
 * Todos pueden verlas y crear las suyas; editar y borrar lo decide el servidor por etiqueta
 * (`canManage`). Las categorías se gestionan en su propio cajón, desde la cabecera.
 */
@Component({
  selector: 'app-tags',
  standalone: true,
  imports: [NgIconComponent, ButtonComponent, UserAvatarComponent, DataTableComponent, TagDrawerComponent, TagCategoriesDrawerComponent],
  viewProviders: [provideIcons({ lucidePlus, lucideRefreshCw, lucideFolderPlus })],
  templateUrl: './tags.component.html',
})
export class TagsComponent implements OnInit {
  private readonly tagsService = inject(TagsService);
  private readonly columnService = inject(TableColumnService);

  @ViewChild('nameTemplate', { static: true }) nameTemplate!: TemplateRef<unknown>;
  @ViewChild('createdByTemplate', { static: true }) createdByTemplate!: TemplateRef<unknown>;

  readonly columns: ColumnDef[] = this.columnService.buildColumns<TagRow>({
    name: { label: $localize`Nombre`, type: 'custom' },
    categoryLabel: { label: $localize`Categoría` },
    kindLabel: { label: $localize`Tipo` },
    createdBy: { label: $localize`Creada por`, type: 'custom', sortable: false },
  });

  readonly tableState = signal<TableState>({ page: 1, pageSize: 25, sortColumn: 'categoryLabel', sortDirection: 'asc' });

  readonly loaded = this.tagsService.loaded;
  private readonly rows = computed(() => toRows(this.tagsService.tags()));
  private readonly visible = computed(() => visibleRows(this.rows(), this.tableState()));
  readonly pageRows = computed(() => this.visible().page);
  readonly total = computed(() => this.visible().total);

  /** La fila abierta en el cajón; `undefined` si está cerrado, `null` si es el alta. */
  readonly selected = signal<TagRow | null | undefined>(undefined);

  readonly categoriesOpen = signal(false);

  ngOnInit(): void {
    this.columns.find(c => c.key === 'name')!.template = this.nameTemplate;
    this.columns.find(c => c.key === 'createdBy')!.template = this.createdByTemplate;
    this.tagsService.load(true);
    this.tagsService.loadCategories();
  }

  refresh(): void {
    this.tagsService.load(true);
    this.tagsService.loadCategories();
  }

  onStateChange(state: TableState): void {
    this.tableState.set(state);
  }

  open(row: TagRow): void {
    this.selected.set(row);
  }

  create(): void {
    this.selected.set(null);
  }

  closeDrawer(): void {
    this.selected.set(undefined);
  }
}
