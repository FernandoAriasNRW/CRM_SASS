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
          Filters
        </button>
        <button uiButton variant="ghost" size="sm" (click)="clearFilters()">Clear All</button>
      </div>
    
      @if (isOpen()) {
        <div class="absolute left-0 top-full mt-2 w-full sm:w-[600px] lg:w-[800px] bg-white dark:bg-muted rounded-xl border border-border shadow-xl z-50 p-4">
          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
            <!-- Dynamic Fields -->
            @for (field of fields; track field) {
              <div class="flex flex-col gap-1.5">
                <label class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">{{ field.label }}</label>
                @switch (field.type) {
                  <!-- Text -->
                  @case ('text') {
                    <input type="text" [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" [placeholder]="'Filter by ' + field.label" class="w-full px-3 py-2 bg-white dark:bg-muted border border-border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary transition-shadow hover:border-border">
                  }
                  <!-- Date -->
                  @case ('date') {
                    <input type="date" [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" class="w-full px-3 py-2 bg-white dark:bg-muted border border-border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary transition-shadow hover:border-border">
                  }
                  <!-- Select -->
                  @case ('select') {
                    <select [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" class="w-full px-3 py-2 bg-white dark:bg-muted border border-border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary transition-shadow hover:border-border">
                      <option [ngValue]="null">All</option>
                      @for (opt of field.options; track opt) {
                        <option [value]="opt.value">{{ opt.label }}</option>
                      }
                    </select>
                  }
                  <!-- Boolean -->
                  @case ('boolean') {
                    <select [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" class="w-full px-3 py-2 bg-white dark:bg-muted border border-border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary transition-shadow hover:border-border">
                      <option [ngValue]="null">All</option>
                      <option [value]="true">Yes</option>
                      <option [value]="false">No</option>
                    </select>
                  }
                }
              </div>
            }
          </div>
          <div class="mt-4 flex justify-end border-t border-border pt-4">
            <button uiButton variant="default" (click)="toggleOpen()">Close / Apply</button>
          </div>
        </div>
      }
    </div>
    `
})
export class AdvancedFiltersComponent {
  @Input() fields: FilterField[] = [];
  @Input() filters: Record<string, any> = {};
  @Output() filtersChange = new EventEmitter<Record<string, any>>();

  isOpen = signal(false);

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
