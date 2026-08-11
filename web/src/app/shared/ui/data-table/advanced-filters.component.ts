import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonComponent } from '../button.component';
import { InputComponent } from '../input.component';
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
  imports: [CommonModule, FormsModule, ButtonComponent, NgIconComponent],
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
      
      <div *ngIf="isOpen()" class="absolute left-0 top-full mt-2 w-full sm:w-[600px] lg:w-[800px] bg-white dark:bg-slate-800 rounded-xl border border-slate-200 dark:border-slate-700 shadow-xl z-50 p-4">
        <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
          <!-- Dynamic Fields -->
          <div *ngFor="let field of fields" class="flex flex-col gap-1.5">
            <label class="text-xs font-semibold text-slate-600 dark:text-slate-400 uppercase tracking-wider">{{ field.label }}</label>
            
            <ng-container [ngSwitch]="field.type">
              <!-- Text -->
              <input *ngSwitchCase="'text'" type="text" [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" [placeholder]="'Filter by ' + field.label" class="w-full px-3 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 transition-shadow hover:border-slate-300">
              
              <!-- Date -->
              <input *ngSwitchCase="'date'" type="date" [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" class="w-full px-3 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 transition-shadow hover:border-slate-300">
              
              <!-- Select -->
              <select *ngSwitchCase="'select'" [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" class="w-full px-3 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 transition-shadow hover:border-slate-300">
                <option [ngValue]="null">All</option>
                <option *ngFor="let opt of field.options" [value]="opt.value">{{ opt.label }}</option>
              </select>

              <!-- Boolean -->
              <select *ngSwitchCase="'boolean'" [(ngModel)]="filters[field.key]" (ngModelChange)="onFilterChange()" class="w-full px-3 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 transition-shadow hover:border-slate-300">
                <option [ngValue]="null">All</option>
                <option [value]="true">Yes</option>
                <option [value]="false">No</option>
              </select>
            </ng-container>
          </div>
        </div>
        <div class="mt-4 flex justify-end border-t border-slate-100 dark:border-slate-700 pt-4">
          <button uiButton variant="default" (click)="toggleOpen()">Close / Apply</button>
        </div>
      </div>
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
