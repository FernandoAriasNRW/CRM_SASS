import { Component, Input, Output, EventEmitter, signal, computed, OnInit, TemplateRef } from '@angular/core';
import { CommonModule, NgTemplateOutlet } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideChevronUp, lucideChevronDown, lucideGripVertical, lucideEye, lucideEyeOff, lucideSettings2, lucideSearch, lucideFilter, lucideSave, lucideDownload, lucidePencil } from '@ng-icons/lucide';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { FormsModule } from '@angular/forms';
import { PaginationComponent } from '../pagination.component';
import { ButtonComponent } from '../button.component';
import { UserAvatarComponent } from '../user-avatar.component';

export interface ColumnDef {
  key: string;
  label: string;
  sortable?: boolean;
  type?: 'text' | 'date' | 'number' | 'badge' | 'custom' | 'user';
  visible?: boolean;
  template?: TemplateRef<unknown>;
  /** Si la celda se puede editar en la propia tabla. Sin esto, la columna es de sólo lectura. */
  editable?: boolean;
  /** Con qué control se edita. Por defecto, un campo de texto. */
  editor?: 'text' | 'number' | 'date' | 'select';
  /** Las opciones del `select`. Sin ellas la columna no se puede editar aunque lo pida. */
  options?: { label: string; value: string }[];
}

/** Un cambio hecho en una celda. Quien reciba esto es el que decide si se guarda. */
export interface CellEdit<T> {
  item: T;
  key: string;
  valor: string;
}

export interface TableState {
  page: number;
  pageSize: number;
  sortColumn?: string;
  sortDirection?: 'asc' | 'desc';
  searchTerm?: string;
  filters?: Record<string, any>;
  columns?: string[]; // keys of visible columns in order
  customColumns?: { key: string; label: string; type?: 'text' | 'date' | 'number' | 'custom' | 'badge' | 'user' }[];
  viewType?: string;
}

@Component({
  selector: 'ui-data-table',
  standalone: true,
  imports: [CommonModule, NgIconComponent, DragDropModule, FormsModule, PaginationComponent, ButtonComponent, NgTemplateOutlet, UserAvatarComponent],
  providers: [provideIcons({ lucideChevronUp, lucideChevronDown, lucideGripVertical, lucideEye, lucideEyeOff, lucideSettings2, lucideSearch, lucideFilter, lucideSave, lucideDownload, lucidePencil })],
  host: { class: 'block h-full' },
  template: `
    <div class="flex flex-col h-full bg-white dark:bg-muted rounded-xl border border-border shadow-sm overflow-hidden">
    
      <!-- Toolbar -->
      <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center p-4 border-b border-border gap-4">
    
        <!-- Search and Filters -->
        <div class="flex items-center gap-3 w-full sm:w-auto">
          <div class="relative w-full sm:w-64">
            <ng-icon name="lucideSearch" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground"></ng-icon>
            <input
              type="text"
              [ngModel]="state().searchTerm"
              (ngModelChange)="onSearch($event)"
              placeholder="Search..."
              class="w-full pl-9 pr-4 py-2 bg-muted border border-border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary transition-shadow"
              />
            </div>
          </div>
    
          <!-- Column Settings & Save View -->
          <div class="flex items-center gap-2">
            <button uiButton variant="ghost" (click)="onSaveView.emit(state())">
              <ng-icon name="lucideSave" class="w-4 h-4 mr-2"></ng-icon>
              Save View
            </button>
    
            <div class="relative" #settingsDropdown>
              <button uiButton variant="outline" (click)="toggleColumnSettings()">
                <ng-icon name="lucideSettings2" class="w-4 h-4 mr-2"></ng-icon>
                Columns
              </button>
    
              <!-- Column Settings Dropdown -->
              @if (showColumnSettings()) {
                <div class="absolute right-0 mt-2 w-72 bg-white dark:bg-muted rounded-xl border border-border shadow-lg z-50 overflow-hidden flex flex-col max-h-[450px]">
                  <div class="p-3 border-b border-border bg-muted dark:bg-muted/50 shrink-0">
                    <h4 class="font-medium text-sm text-foreground dark:text-white">Visible Columns</h4>
                  </div>
                  <div class="p-2 overflow-y-auto shrink min-h-[100px]" cdkDropList (cdkDropListDropped)="onColumnDrop($event)">
                    @for (col of mutableColumns(); track col) {
                      <div cdkDrag class="flex items-center justify-between p-2 hover:bg-muted dark:hover:bg-secondary/50 rounded-lg group">
                        <div class="flex items-center gap-3">
                          <div cdkDragHandle class="cursor-grab active:cursor-grabbing text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity">
                            <ng-icon name="lucideGripVertical"></ng-icon>
                          </div>
                          <span class="text-sm font-medium text-muted-foreground dark:text-foreground">{{ col.label }}</span>
                        </div>
                        <button (click)="toggleColumnVisibility(col)" class="text-muted-foreground hover:text-primary transition-colors">
                          <ng-icon [name]="col.visible !== false ? 'lucideEye' : 'lucideEyeOff'"></ng-icon>
                        </button>
                      </div>
                    }
                  </div>
                  <!-- Add Custom Column -->
                  <div class="p-3 border-t border-border bg-muted dark:bg-muted/50 shrink-0">
                    <h4 class="font-medium text-xs text-muted-foreground mb-2 uppercase tracking-wider">Añadir Columna</h4>
                    <div class="flex flex-col gap-2">
                      <select [ngModel]="customColumnKey()" (ngModelChange)="customColumnKey.set($event)" class="w-full px-2 py-1.5 text-sm bg-white dark:bg-muted border border-border rounded-md focus:outline-none focus:ring-1 focus:ring-primary">
                        <option value="">Selecciona Propiedad...</option>
                        @for (key of availableColumns(); track key) {
                          <option [value]="key">{{ key }}</option>
                        }
                      </select>
                      <input type="text" [ngModel]="customColumnLabel()" (ngModelChange)="customColumnLabel.set($event)" placeholder="Etiqueta visible (ej. Cliente)" class="w-full px-2 py-1.5 text-sm bg-white dark:bg-muted border border-border rounded-md focus:outline-none focus:ring-1 focus:ring-primary" />
                      <button (click)="addCustomColumn()" [disabled]="!customColumnKey() || !customColumnLabel()" class="w-full py-1.5 bg-primary text-white text-sm rounded-md hover:bg-primary transition-colors disabled:opacity-50 disabled:cursor-not-allowed mt-1">
                        Añadir Columna
                      </button>
                    </div>
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
    
    
        <!-- Table Container -->
        <div class="flex-1 overflow-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="bg-muted dark:bg-muted/50 border-b border-border">
                @for (col of visibleColumns(); track col) {
                  <th
                    class="px-4 py-3 text-[11px] font-semibold text-muted-foreground uppercase tracking-wider whitespace-nowrap sticky top-0 bg-muted z-10 shadow-[0_1px_0_0_#e2e8f0] dark:shadow-[0_1px_0_0_#334155]"
                    [class.cursor-pointer]="col.sortable"
                    [class.hover:bg-muted]="col.sortable"
                    [class.dark:hover:bg-secondary]="col.sortable"
                    (click)="col.sortable && handleSort(col.key)">
                    <div class="flex items-center gap-1.5">
                      {{ col.label }}
                      @if (col.sortable && state().sortColumn === col.key) {
                        <div class="flex flex-col text-primary">
                          @if (state().sortDirection === 'asc') {
                            <ng-icon name="lucideChevronUp" class="w-3 h-3"></ng-icon>
                          }
                          @if (state().sortDirection === 'desc') {
                            <ng-icon name="lucideChevronDown" class="w-3 h-3"></ng-icon>
                          }
                        </div>
                      }
                    </div>
                  </th>
                }
                <!-- Optional Actions Column -->
                @if (hasActions) {
                  <th class="px-4 py-3 w-1 sticky top-0 bg-muted z-10 shadow-[0_1px_0_0_#e2e8f0] dark:shadow-[0_1px_0_0_#334155]"></th>
                }
              </tr>
            </thead>
            <tbody class="divide-y divide-border">
              @if (!loading && data.length > 0) {
                @for (item of data; track item) {
                  <tr (click)="rowClick.emit(item)" class="hover:bg-muted/80 dark:hover:bg-muted/50 transition-colors group cursor-pointer">
                    @for (col of visibleColumns(); track col) {
                      <td class="px-4 py-2 whitespace-nowrap border-b border-border dark:border-border/50">
                        @if (editandoEsta(item, col)) {
                          <!-- El clic se para en el propio control, y no en un envoltorio: si
                               subiera a la fila abriría el panel de detalle encima de lo que se
                               está escribiendo. Un div con (click) sería un elemento interactivo
                               que no se puede enfocar ni alcanzar con el teclado.
                               Ojo: esta plantilla es una cadena con acentos graves, así que no se
                               pueden usar aquí ni para citar código. -->
                          @if (col.editor === 'select') {
                            <select [ngModel]="valorTexto(item, col)"
                                    (ngModelChange)="confirmarEdicion(item, col, $event)"
                                    (click)="$event.stopPropagation()"
                                    (keydown.escape)="cancelarEdicion()"
                                    [attr.aria-label]="col.label"
                                    class="w-full bg-transparent border border-border rounded-md px-2 py-1 text-[13px] outline-none focus:ring-1 focus:ring-ring">
                              @for (opcion of col.options ?? []; track opcion.value) {
                                <option [value]="opcion.value">{{ opcion.label }}</option>
                              }
                            </select>
                          } @else {
                            <input [type]="col.editor === 'date' ? 'date' : 'text'"
                                   [inputMode]="col.editor === 'number' ? 'decimal' : 'text'"
                                   [ngModel]="valorTexto(item, col)"
                                   (blur)="confirmarEdicion(item, col, $any($event.target).value)"
                                   (click)="$event.stopPropagation()"
                                   (keydown.enter)="$any($event.target).blur()"
                                   (keydown.escape)="cancelarEdicion()"
                                   [attr.aria-label]="col.label"
                                   class="w-full bg-transparent border border-border rounded-md px-2 py-1 text-[13px] outline-none focus:ring-1 focus:ring-ring" />
                          }
                        } @else if (col.template) {
                          <ng-container *ngTemplateOutlet="col.template; context: { $implicit: item, column: col }"></ng-container>
                        } @else {
                          @if (col.type === 'user') {
                            <app-user-avatar [userId]="valorTexto(item, col)"></app-user-avatar>
                          } @else {
                            <span class="text-[13px] text-muted-foreground">
                              {{ formatValue(item, col) }}
                            </span>
                          }
                        }

                        @if (sePuedeEditar(col) && !editandoEsta(item, col)) {
                          <!-- El disparador de la edición va aparte del contenido, y no envolviéndolo,
                               porque las columnas con plantilla propia ya traen sus propios controles
                               dentro y anidar botones no es válido. -->
                          <button type="button"
                                  (click)="$event.stopPropagation(); empezarEdicion(item, col)"
                                  [attr.aria-label]="etiquetaDeEdicion(col)"
                                  class="ml-1 align-middle opacity-0 group-hover:opacity-100 focus:opacity-100 text-muted-foreground hover:text-primary transition-opacity">
                            <ng-icon name="lucidePencil" class="w-3 h-3"></ng-icon>
                          </button>
                        }
                      </td>
                    }
                    @if (hasActions) {
                      <td class="px-4 py-2 whitespace-nowrap text-right border-b border-border dark:border-border/50">
                        <ng-container *ngTemplateOutlet="actionsTemplate; context: { $implicit: item }"></ng-container>
                      </td>
                    }
                  </tr>
                }
              }
              @if (loading) {
                <tr>
                  <td [attr.colspan]="visibleColumns().length + (hasActions ? 1 : 0)" class="px-4 py-8 text-center border-b border-border dark:border-border/50">
                    <div class="flex flex-col items-center justify-center gap-3">
                      <div class="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
                      <span class="text-sm text-muted-foreground">Loading data...</span>
                    </div>
                  </td>
                </tr>
              }
              @if (!loading && data.length === 0) {
                <tr>
                  <td [attr.colspan]="visibleColumns().length + (hasActions ? 1 : 0)" class="px-6 py-16 text-center">
                    <div class="flex flex-col items-center justify-center gap-2 text-muted-foreground">
                      <ng-icon name="lucideSearch" class="w-12 h-12 mb-2 opacity-50"></ng-icon>
                      <p class="text-lg font-medium text-foreground dark:text-white">No results found</p>
                      <p class="text-sm">Try adjusting your filters or search term.</p>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
    
        <!-- Pagination -->
        <div class="p-4 border-t border-border bg-muted/50">
          <app-pagination
            [state]="paginationState()"
            (pageChange)="onPageChange($event)"
            (pageSizeChange)="onPageSizeChange($event)">
          </app-pagination>
        </div>
    
      </div>
    `
})
/**
 * Tabla genérica en el tipo de sus filas.
 *
 * El parámetro `T` no es adorno: hace que `rowClick` emita el tipo real de la fila, de
 * modo que quien lo recibe no tiene que convertirlo a mano ni acertar de memoria. Antes
 * era `any` y cualquier error de nombre de propiedad pasaba desapercibido hasta que
 * algo aparecía vacío en pantalla.
 */
export class DataTableComponent<T extends object = Record<string, unknown>> implements OnInit {
  @Input({ required: true }) columns: ColumnDef[] = [];
  @Input({ required: true }) data: T[] = [];
  @Input() totalItems = 0;
  @Input() loading = false;
  @Input() hasActions = false;
  @Input() actionsTemplate!: TemplateRef<unknown>;
  
  @Input() set initialState(val: Partial<TableState>) {
    this.state.update(s => ({ ...s, ...val }));
  }

  @Output() stateChange = new EventEmitter<TableState>();
  @Output() onSaveView = new EventEmitter<TableState>();
  @Output() rowClick = new EventEmitter<T>();

  /**
   * Un cambio confirmado en una celda.
   *
   * La tabla **no guarda nada**: emite el cambio y vuelve a pintar lo que le llegue en `data`.
   * Quien la usa es quien llama a la API y quien revierte si el servidor rechaza, que es donde
   * ya vive esa lógica —el tablero lo hace igual al arrastrar una tarjeta—. Si la tabla también
   * guardara, habría dos sitios decidiendo qué se ve, y acabarían discrepando.
   */
  @Output() cellEdit = new EventEmitter<CellEdit<T>>();

  /** La celda que se está editando, o `null`. Sólo puede haber una. */
  readonly editando = signal<{ fila: T; key: string } | null>(null);

  state = signal<TableState>({
    page: 1,
    pageSize: 25,
    sortDirection: 'asc'
  });

  mutableColumns = signal<ColumnDef[]>([]);
  
  visibleColumns = computed(() => {
    return this.mutableColumns().filter(c => c.visible !== false);
  });

  paginationState = computed(() => {
    const s = this.state();
    const totalPages = Math.ceil(this.totalItems / s.pageSize);
    return {
      page: s.page,
      pageSize: s.pageSize,
      totalCount: this.totalItems,
      totalPages: totalPages,
      hasPreviousPage: s.page > 1,
      hasNextPage: s.page < totalPages
    };
  });

  showColumnSettings = signal(false);
  showFilters = signal(false);

  customColumnKey = signal('');
  customColumnLabel = signal('');

  availableColumns = computed(() => {
    if (!this.data || this.data.length === 0) return [];
    const allKeys = Object.keys(this.data[0]);
    const existingKeys = new Set(this.mutableColumns().map(c => c.key));
    return allKeys.filter(key => {
      if (existingKeys.has(key)) return false;
      const lower = key.toLowerCase();
      if (lower === 'id' || lower === 'password' || lower.endsWith('id')) return false;
      return true;
    });
  });

  addCustomColumn() {
    const key = this.customColumnKey();
    const label = this.customColumnLabel();
    if (!key || !label) return;

    let colType: 'text' | 'number' | 'date' = 'text';
    if (this.data && this.data.length > 0) {
      const sample = (this.data[0] as Record<string, unknown>)[key];
      if (typeof sample === 'number') {
        colType = 'number';
      } else if (typeof sample === 'string' && !isNaN(Date.parse(sample)) && sample.includes('-')) {
        colType = 'date';
      }
    }

    const newCol: ColumnDef = { key, label, type: colType, sortable: true, visible: true };
    this.mutableColumns.update(cols => [...cols, newCol]);
    this.customColumnKey.set('');
    this.customColumnLabel.set('');
    this.emitStateChange();
  }

  ngOnInit() {
    // Inject any custom columns from state BEFORE resolving order and visibility
    let baseColumns = [...this.columns];
    if (this.state().customColumns) {
      const customDefs = this.state().customColumns!.map(cc => ({
        key: cc.key,
        label: cc.label,
        type: cc.type || 'text',
        sortable: true,
        visible: true // visibility is overridden by state().columns anyway
      } as ColumnDef));
      baseColumns = [...baseColumns, ...customDefs];
    }

    if (this.state().columns && this.state().columns!.length > 0) {
      const orderMap = new Map(this.state().columns!.map((k, i) => [k, i]));
      const ordered = [...baseColumns].sort((a, b) => {
        const indexA = orderMap.has(a.key) ? orderMap.get(a.key)! : 999;
        const indexB = orderMap.has(b.key) ? orderMap.get(b.key)! : 999;
        return indexA - indexB;
      });
      ordered.forEach(col => {
        col.visible = this.state().columns!.includes(col.key);
      });
      this.mutableColumns.set(ordered);
    } else {
      this.mutableColumns.set([...baseColumns.map(c => ({...c, visible: c.visible !== false}))]);
    }
  }

  toggleColumnSettings() {
    this.showColumnSettings.update(v => !v);
  }

  toggleFilters() {
    this.showFilters.update(v => !v);
  }

  toggleColumnVisibility(col: ColumnDef) {
    const cols = this.mutableColumns();
    const index = cols.findIndex(c => c.key === col.key);
    if (index > -1) {
      cols[index].visible = !cols[index].visible;
      this.mutableColumns.set([...cols]);
      this.emitStateChange();
    }
  }

  onColumnDrop(event: CdkDragDrop<string[]>) {
    const cols = [...this.mutableColumns()];
    moveItemInArray(cols, event.previousIndex, event.currentIndex);
    this.mutableColumns.set(cols);
    this.emitStateChange();
  }

  handleSort(columnKey: string) {
    this.state.update(s => {
      if (s.sortColumn === columnKey) {
        return {
          ...s,
          sortDirection: s.sortDirection === 'asc' ? 'desc' : 'asc',
          page: 1 // Reset to first page on sort
        };
      }
      return {
        ...s,
        sortColumn: columnKey,
        sortDirection: 'asc',
        page: 1
      };
    });
    this.emitStateChange();
  }

  onSearch(term: string) {
    this.state.update(s => ({ ...s, searchTerm: term, page: 1 }));
    // Consider adding debounce here or let parent handle it via stateChange
    this.emitStateChange();
  }

  onPageChange(page: number) {
    this.state.update(s => ({ ...s, page }));
    this.emitStateChange();
  }

  onPageSizeChange(pageSize: number) {
    this.state.update(s => ({ ...s, pageSize, page: 1 }));
    this.emitStateChange();
  }

  updateFilters(filters: Record<string, any>) {
    this.state.update(s => ({ ...s, filters, page: 1 }));
    this.emitStateChange();
  }

  private emitStateChange() {
    const s = this.state();
    const cols = this.mutableColumns().filter(c => c.visible).map(c => c.key);
    
    // Find custom columns
    const originalKeys = new Set(this.columns.map(c => c.key));
    const customCols = this.mutableColumns()
      .filter(c => !originalKeys.has(c.key))
      .map(c => ({ key: c.key, label: c.label, type: c.type as any }));
      
    const newState = { ...s, columns: cols, customColumns: customCols.length > 0 ? customCols : undefined };
    this.stateChange.emit(newState);
  }

  /** Valor de una columna como cadena, para plantillas que esperan texto. */
  valorTexto(item: T, col: ColumnDef): string {
    const val = (item as Record<string, unknown>)[col.key];
    if (val == null) return '';

    const texto = String(val);

    // `input type="date"` sólo entiende `aaaa-mm-dd`. Con la marca de tiempo entera se queda
    // vacío, sin decir por qué, y parece que la tarea no tiene fecha.
    if (col.editor === 'date') return texto.slice(0, 10);

    return texto;
  }

  /**
   * Una columna se puede editar si lo pide y, cuando es un desplegable, si trae opciones.
   * Un `select` vacío sería un control que no deja elegir nada.
   */
  sePuedeEditar(col: ColumnDef): boolean {
    if (!col.editable) return false;
    return col.editor !== 'select' || (col.options?.length ?? 0) > 0;
  }

  editandoEsta(item: T, col: ColumnDef): boolean {
    const actual = this.editando();
    return !!actual && actual.fila === item && actual.key === col.key;
  }

  etiquetaDeEdicion(col: ColumnDef): string {
    return $localize`Editar ${col.label}`;
  }

  empezarEdicion(item: T, col: ColumnDef): void {
    if (!this.sePuedeEditar(col)) return;
    this.editando.set({ fila: item, key: col.key });
  }

  cancelarEdicion(): void {
    this.editando.set(null);
  }

  /**
   * Cierra la edición y avisa del cambio, si es que lo hay.
   *
   * **Sin edición abierta no se confirma nada.** Al pulsar Escape se quita el editor del DOM, y
   * el navegador dispara un `blur` sobre el elemento que acaba de desaparecer: sin esta guarda,
   * ese `blur` guardaba el valor que se acababa de descartar y Escape no cancelaba nada.
   *
   * Un valor idéntico tampoco se emite: guardar lo mismo gasta una petición y, si el servidor
   * responde tarde, hace parpadear una celda que nadie tocó.
   */
  confirmarEdicion(item: T, col: ColumnDef, valor: string): void {
    if (!this.editandoEsta(item, col)) return;

    this.editando.set(null);

    const anterior = this.valorTexto(item, col);
    if (valor === anterior) return;

    this.cellEdit.emit({ item, key: col.key, valor });
  }

  formatValue(item: T, col: ColumnDef): string {
    // La columna se identifica por nombre, así que hay que indexar por cadena. La
    // conversión se hace aquí, en un punto, y no se propaga a quien usa la tabla.
    const val = (item as Record<string, unknown>)[col.key];
    if (val === null || val === undefined) return '-';

    if (col.type === 'date') {
      // Se comprueba el tipo antes de construir la fecha: con `any` esto aceptaba
      // cualquier cosa y producía «Invalid Date» en pantalla sin avisar.
      if (typeof val === 'string' || typeof val === 'number' || val instanceof Date) {
        return new Date(val).toLocaleDateString();
      }
      return '-';
    }

    return String(val);
  }
}
