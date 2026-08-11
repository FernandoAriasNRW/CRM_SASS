import { Component, Input, Output, EventEmitter, signal, computed, ViewChild, ElementRef, OnInit, TemplateRef } from '@angular/core';
import { CommonModule, NgTemplateOutlet } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideChevronUp, lucideChevronDown, lucideGripVertical, lucideEye, lucideEyeOff, lucideSettings2, lucideSearch, lucideFilter, lucideSave, lucideDownload } from '@ng-icons/lucide';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { FormsModule } from '@angular/forms';
import { PaginationComponent } from '../pagination.component';
import { ButtonComponent } from '../button.component';
import { InputComponent } from '../input.component';
import { UserAvatarComponent } from '../user-avatar.component';

export interface ColumnDef {
  key: string;
  label: string;
  sortable?: boolean;
  type?: 'text' | 'date' | 'number' | 'badge' | 'custom' | 'user';
  visible?: boolean;
  template?: TemplateRef<any>;
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
  providers: [provideIcons({ lucideChevronUp, lucideChevronDown, lucideGripVertical, lucideEye, lucideEyeOff, lucideSettings2, lucideSearch, lucideFilter, lucideSave, lucideDownload })],
  host: { class: 'block h-full' },
  template: `
    <div class="flex flex-col h-full bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-700 shadow-sm overflow-hidden">
      
      <!-- Toolbar -->
      <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center p-4 border-b border-slate-200 dark:border-slate-700 gap-4">
        
        <!-- Search and Filters -->
        <div class="flex items-center gap-3 w-full sm:w-auto">
          <div class="relative w-full sm:w-64">
            <ng-icon name="lucideSearch" class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"></ng-icon>
            <input 
              type="text" 
              [ngModel]="state().searchTerm" 
              (ngModelChange)="onSearch($event)"
              placeholder="Search..." 
              class="w-full pl-9 pr-4 py-2 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 transition-shadow"
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
            <div *ngIf="showColumnSettings()" class="absolute right-0 mt-2 w-72 bg-white dark:bg-slate-800 rounded-xl border border-slate-200 dark:border-slate-700 shadow-lg z-50 overflow-hidden flex flex-col max-h-[450px]">
              <div class="p-3 border-b border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/50 shrink-0">
                <h4 class="font-medium text-sm text-slate-900 dark:text-white">Visible Columns</h4>
              </div>
              <div class="p-2 overflow-y-auto shrink min-h-[100px]" cdkDropList (cdkDropListDropped)="onColumnDrop($event)">
                <div *ngFor="let col of mutableColumns()" cdkDrag class="flex items-center justify-between p-2 hover:bg-slate-50 dark:hover:bg-slate-700/50 rounded-lg group">
                  <div class="flex items-center gap-3">
                    <div cdkDragHandle class="cursor-grab active:cursor-grabbing text-slate-400 opacity-0 group-hover:opacity-100 transition-opacity">
                      <ng-icon name="lucideGripVertical"></ng-icon>
                    </div>
                    <span class="text-sm font-medium text-slate-700 dark:text-slate-200">{{ col.label }}</span>
                  </div>
                  <button (click)="toggleColumnVisibility(col)" class="text-slate-400 hover:text-blue-500 transition-colors">
                    <ng-icon [name]="col.visible !== false ? 'lucideEye' : 'lucideEyeOff'"></ng-icon>
                  </button>
                </div>
              </div>

              <!-- Add Custom Column -->
              <div class="p-3 border-t border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/50 shrink-0">
                <h4 class="font-medium text-xs text-slate-500 dark:text-slate-400 mb-2 uppercase tracking-wider">Añadir Columna</h4>
                <div class="flex flex-col gap-2">
                  <select [ngModel]="customColumnKey()" (ngModelChange)="customColumnKey.set($event)" class="w-full px-2 py-1.5 text-sm bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500">
                    <option value="">Selecciona Propiedad...</option>
                    <option *ngFor="let key of availableColumns()" [value]="key">{{ key }}</option>
                  </select>
                  <input type="text" [ngModel]="customColumnLabel()" (ngModelChange)="customColumnLabel.set($event)" placeholder="Etiqueta visible (ej. Cliente)" class="w-full px-2 py-1.5 text-sm bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500" />
                  <button (click)="addCustomColumn()" [disabled]="!customColumnKey() || !customColumnLabel()" class="w-full py-1.5 bg-blue-600 text-white text-sm rounded-md hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed mt-1">
                    Añadir Columna
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>


      <!-- Table Container -->
      <div class="flex-1 overflow-auto">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr class="bg-slate-50 dark:bg-slate-800/50 border-b border-slate-200 dark:border-slate-700">
              <th *ngFor="let col of visibleColumns()" 
                  class="px-4 py-3 text-[11px] font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider whitespace-nowrap sticky top-0 bg-slate-50 dark:bg-slate-800 z-10 shadow-[0_1px_0_0_#e2e8f0] dark:shadow-[0_1px_0_0_#334155]"
                  [class.cursor-pointer]="col.sortable"
                  [class.hover:bg-slate-100]="col.sortable"
                  [class.dark:hover:bg-slate-700]="col.sortable"
                  (click)="col.sortable && handleSort(col.key)">
                <div class="flex items-center gap-1.5">
                  {{ col.label }}
                  <div *ngIf="col.sortable && state().sortColumn === col.key" class="flex flex-col text-blue-500">
                    <ng-icon name="lucideChevronUp" *ngIf="state().sortDirection === 'asc'" class="w-3 h-3"></ng-icon>
                    <ng-icon name="lucideChevronDown" *ngIf="state().sortDirection === 'desc'" class="w-3 h-3"></ng-icon>
                  </div>
                </div>
              </th>
              <!-- Optional Actions Column -->
              <th *ngIf="hasActions" class="px-4 py-3 w-1 sticky top-0 bg-slate-50 dark:bg-slate-800 z-10 shadow-[0_1px_0_0_#e2e8f0] dark:shadow-[0_1px_0_0_#334155]"></th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-200 dark:divide-slate-700">
            <ng-container *ngIf="!loading && data.length > 0">
              <tr *ngFor="let item of data" (click)="rowClick.emit(item)" class="hover:bg-slate-50/80 dark:hover:bg-slate-800/50 transition-colors group cursor-pointer">
                <td *ngFor="let col of visibleColumns()" class="px-4 py-2 whitespace-nowrap border-b border-slate-100 dark:border-slate-800/50">
                  <ng-container *ngIf="col.template; else defaultCell">
                    <ng-container *ngTemplateOutlet="col.template; context: { $implicit: item, column: col }"></ng-container>
                  </ng-container>
                  <ng-template #defaultCell>
                    <ng-container *ngIf="col.type === 'user'; else textCell">
                      <app-user-avatar [userId]="item[col.key]"></app-user-avatar>
                    </ng-container>
                    <ng-template #textCell>
                      <span class="text-[13px] text-slate-700 dark:text-slate-300">
                        {{ formatValue(item, col) }}
                      </span>
                    </ng-template>
                  </ng-template>
                </td>
                <td *ngIf="hasActions" class="px-4 py-2 whitespace-nowrap text-right border-b border-slate-100 dark:border-slate-800/50">
                  <ng-container *ngTemplateOutlet="actionsTemplate; context: { $implicit: item }"></ng-container>
                </td>
              </tr>
            </ng-container>
            <tr *ngIf="loading">
              <td [attr.colspan]="visibleColumns().length + (hasActions ? 1 : 0)" class="px-4 py-8 text-center border-b border-slate-100 dark:border-slate-800/50">
                <div class="flex flex-col items-center justify-center gap-3">
                  <div class="w-8 h-8 border-4 border-blue-500 border-t-transparent rounded-full animate-spin"></div>
                  <span class="text-sm text-slate-500">Loading data...</span>
                </div>
              </td>
            </tr>
            <tr *ngIf="!loading && data.length === 0">
              <td [attr.colspan]="visibleColumns().length + (hasActions ? 1 : 0)" class="px-6 py-16 text-center">
                <div class="flex flex-col items-center justify-center gap-2 text-slate-500 dark:text-slate-400">
                  <ng-icon name="lucideSearch" class="w-12 h-12 mb-2 opacity-50"></ng-icon>
                  <p class="text-lg font-medium text-slate-900 dark:text-white">No results found</p>
                  <p class="text-sm">Try adjusting your filters or search term.</p>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div class="p-4 border-t border-slate-200 dark:border-slate-700 bg-slate-50/50 dark:bg-slate-900/50">
        <app-pagination
          [state]="paginationState()"
          (pageChange)="onPageChange($event)"
          (pageSizeChange)="onPageSizeChange($event)">
        </app-pagination>
      </div>

    </div>
  `
})
export class DataTableComponent implements OnInit {
  @Input({ required: true }) columns: ColumnDef[] = [];
  @Input({ required: true }) data: any[] = [];
  @Input() totalItems = 0;
  @Input() loading = false;
  @Input() hasActions = false;
  @Input() actionsTemplate!: TemplateRef<any>;
  
  @Input() set initialState(val: Partial<TableState>) {
    this.state.update(s => ({ ...s, ...val }));
  }

  @Output() stateChange = new EventEmitter<TableState>();
  @Output() onSaveView = new EventEmitter<TableState>();
  @Output() rowClick = new EventEmitter<any>();

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
      const sample = this.data[0][key];
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

  formatValue(item: any, col: ColumnDef): string {
    const val = item[col.key];
    if (val === null || val === undefined) return '-';
    
    if (col.type === 'date') {
      return new Date(val).toLocaleDateString();
    }
    
    return String(val);
  }
}
