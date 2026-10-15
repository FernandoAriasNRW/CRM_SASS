import { Component, EventEmitter, Input, Output, signal } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { ButtonComponent } from '../button.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideX, lucideFilter } from '@ng-icons/lucide';

export interface FilterField {
  key: string;
  label: string;
  type: 'text' | 'date' | 'select' | 'boolean';
  options?: { label: string; value: any }[]; // For select type
}

let nextInstance = 0;

@Component({
  selector: 'ui-advanced-filters',
  standalone: true,
  imports: [FormsModule, ButtonComponent, NgIconComponent],
  providers: [provideIcons({ lucideX, lucideFilter })],
  template: `
    <div class="relative w-full">
      <div class="flex items-center gap-2 mb-4">
        <button uiButton variant="outline" (click)="toggleOpen()">
          <ng-icon name="lucideFilter" class="w-4 h-4 mr-2"></ng-icon>
          <span i18n>Filtros</span>
        </button>
        <button uiButton variant="ghost" size="sm" (click)="clearFilters()" i18n>Limpiar</button>
      </div>
    
      @if (isOpen()) {
        <div class="absolute left-0 top-full mt-2 w-full sm:w-[600px] lg:w-[800px] bg-white dark:bg-muted rounded-xl border border-border shadow-xl z-50 p-4">
          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
            <!-- Dynamic Fields -->
            @for (field of fields; track field) {
              <div class="flex flex-col gap-1.5">
                <label class="text-xs font-semibold text-muted-foreground uppercase tracking-wider" [for]="controlId(field)">{{ field.label }}</label>
                @switch (field.type) {
                  <!-- Text -->
                  @case ('text') {
                    <input type="text" [id]="controlId(field)" [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" [placeholder]="placeholderFor(field)" class="w-full px-3 py-2 bg-white dark:bg-muted border border-border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary transition-shadow hover:border-border">
                  }
                  <!-- Date -->
                  @case ('date') {
                    <input type="date" [id]="controlId(field)" [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" class="w-full px-3 py-2 bg-white dark:bg-muted border border-border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary transition-shadow hover:border-border">
                  }
                  <!-- Select -->
                  @case ('select') {
                    <select [id]="controlId(field)" [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" class="w-full px-3 py-2 bg-white dark:bg-muted border border-border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary transition-shadow hover:border-border">
                      <option [ngValue]="null" i18n>Todos</option>
                      @for (opt of field.options; track opt) {
                        <option [value]="opt.value">{{ opt.label }}</option>
                      }
                    </select>
                  }
                  <!-- Boolean -->
                  @case ('boolean') {
                    <select [id]="controlId(field)" [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" class="w-full px-3 py-2 bg-white dark:bg-muted border border-border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary transition-shadow hover:border-border">
                      <option [ngValue]="null" i18n>Todos</option>
                      <option [value]="true" i18n>Sí</option>
                      <option [value]="false" i18n>No</option>
                    </select>
                  }
                }
              </div>
            }
          </div>
          <div class="mt-4 flex justify-end border-t border-border pt-4">
            <button uiButton variant="default" (click)="toggleOpen()" i18n>Aplicar</button>
          </div>
        </div>
      }
    </div>
    `
})
export class AdvancedFiltersComponent {
  /** Distingue los identificadores de los campos si hay más de un panel en la página. */
  private readonly instance = nextInstance++;

  @Input() fields: FilterField[] = [];
  @Input() filters: Record<string, any> = {};
  @Output() filtersChange = new EventEmitter<Record<string, any>>();

  isOpen = signal(false);

  /** El identificador del campo, para que su etiqueta lo nombre. */
  controlId(field: FilterField): string {
    return `advanced-filter-${this.instance}-${field.key}`;
  }

  placeholderFor(field: FilterField): string {
    return $localize`Filtrar por ${field.label}:label:`;
  }

  toggleOpen() {
    this.isOpen.update(v => !v);
  }

  onFilterChange() {
    this.filtersChange.emit(this.filters);
  }

  clearFilters() {
    this.filters = {};
    this.filtersChange.emit(this.filters);
  }
}
