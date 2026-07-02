import { Component, input, output, computed } from '@angular/core';
import { NgIconComponent } from '@ng-icons/core';
import { lucideChevronLeft, lucideChevronRight, lucideChevronsLeft, lucideChevronsRight } from '@ng-icons/lucide';

export interface PaginationState {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [NgIconComponent],
  template: `
    @if (state().totalPages > 1) {
      <div class="flex items-center justify-between px-2 py-3 border-t border-border">
        <!-- Results info -->
        <div class="text-sm text-muted-foreground">
          Mostrando
          <span class="font-medium">{{ startItem() }}</span>
          -
          <span class="font-medium">{{ endItem() }}</span>
          de
          <span class="font-medium">{{ state().totalCount }}</span>
          resultados
        </div>

        <!-- Page controls -->
        <div class="flex items-center gap-1">
          <!-- Page size selector -->
          <select
            class="h-8 w-16 rounded-md border border-input bg-background text-sm px-2"
            [value]="state().pageSize"
            (change)="onPageSizeChange($event)">
            <option value="5">5</option>
            <option value="10">10</option>
            <option value="20">20</option>
            <option value="50">50</option>
          </select>

          <!-- Navigation buttons -->
          <div class="flex items-center ml-2">
            <button
              class="inline-flex items-center justify-center w-8 h-8 rounded-md border border-input bg-background hover:bg-accent hover:text-accent-foreground disabled:opacity-50 disabled:pointer-events-none"
              [disabled]="!state().hasPreviousPage"
              (click)="goToPage(1)">
              <ng-icon name="lucideChevronsLeft" class="w-4 h-4"></ng-icon>
            </button>

            <button
              class="inline-flex items-center justify-center w-8 h-8 rounded-md border border-input bg-background hover:bg-accent hover:text-accent-foreground disabled:opacity-50 disabled:pointer-events-none"
              [disabled]="!state().hasPreviousPage"
              (click)="previousPage()">
              <ng-icon name="lucideChevronLeft" class="w-4 h-4"></ng-icon>
            </button>

            <!-- Page numbers -->
            <div class="flex items-center gap-1 mx-2">
              @for (page of visiblePages(); track page) {
                @if (page === -1) {
                  <span class="w-8 h-8 flex items-center justify-center text-muted-foreground">...</span>
                } @else {
                  <button
                    class="inline-flex items-center justify-center w-8 h-8 rounded-md text-sm font-medium transition-colors"
                    [class.bg-primary]="page === state().page"
                    [class.text-primary-foreground]="page === state().page"
                    [class.text-foreground]="page !== state().page"
                    [class.hover:bg-accent]="page !== state().page"
                    (click)="goToPage(page)">
                    {{ page }}
                  </button>
                }
              }
            </div>

            <button
              class="inline-flex items-center justify-center w-8 h-8 rounded-md border border-input bg-background hover:bg-accent hover:text-accent-foreground disabled:opacity-50 disabled:pointer-events-none"
              [disabled]="!state().hasNextPage"
              (click)="nextPage()">
              <ng-icon name="lucideChevronRight" class="w-4 h-4"></ng-icon>
            </button>

            <button
              class="inline-flex items-center justify-center w-8 h-8 rounded-md border border-input bg-background hover:bg-accent hover:text-accent-foreground disabled:opacity-50 disabled:pointer-events-none"
              [disabled]="!state().hasNextPage"
              (click)="goToPage(state().totalPages)">
              <ng-icon name="lucideChevronsRight" class="w-4 h-4"></ng-icon>
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class PaginationComponent {
  readonly state = input.required<PaginationState>();
  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  readonly startItem = computed(() => {
    const s = this.state();
    return (s.page - 1) * s.pageSize + 1;
  });

  readonly endItem = computed(() => {
    const s = this.state();
    return Math.min(s.page * s.pageSize, s.totalCount);
  });

  readonly visiblePages = computed(() => {
    const s = this.state();
    const total = s.totalPages;
    const current = s.page;
    const pages: number[] = [];

    if (total <= 7) {
      for (let i = 1; i <= total; i++) pages.push(i);
    } else {
      pages.push(1);
      if (current > 3) pages.push(-1);
      for (let i = Math.max(2, current - 1); i <= Math.min(total - 1, current + 1); i++) {
        pages.push(i);
      }
      if (current < total - 2) pages.push(-1);
      pages.push(total);
    }

    return pages;
  });

  goToPage(page: number): void {
    if (page >= 1 && page <= this.state().totalPages && page !== this.state().page) {
      this.pageChange.emit(page);
    }
  }

  previousPage(): void {
    if (this.state().hasPreviousPage) {
      this.goToPage(this.state().page - 1);
    }
  }

  nextPage(): void {
    if (this.state().hasNextPage) {
      this.goToPage(this.state().page + 1);
    }
  }

  onPageSizeChange(event: Event): void {
    const size = parseInt((event.target as HTMLSelectElement).value, 10);
    this.pageSizeChange.emit(size);
  }
}
