import { Component, Input } from '@angular/core';
import { NgClass } from '@angular/common';

/**
 * Variantes de skeleton
 */
export type SkeletonVariant = 'text' | 'circular' | 'rectangular' | 'card' | 'table-row';

/**
 * Componente Skeleton para mostrar estados de carga.
 * Proporciona feedback visual mientras se cargan datos.
 *
 * Uso:
 * ```html
 * <!-- Skeleton básico -->
 * <app-skeleton></app-skeleton>
 *
 * <!-- Skeleton de texto -->
 * <app-skeleton variant="text" [lines]="3"></app-skeleton>
 *
 * <!-- Skeleton circular (avatar) -->
 * <app-skeleton variant="circular" width="40px" height="40px"></app-skeleton>
 *
 * <!-- Skeleton de tabla -->
 * <app-skeleton variant="table-row" [columns]="4"></app-skeleton>
 *
 * <!-- Skeleton de tarjeta -->
 * <app-skeleton variant="card"></app-skeleton>
 * ```
 */
@Component({
  selector: 'app-skeleton',
  standalone: true,
  imports: [NgClass],
  template: `
    @switch (variant) {
      @case ('text') {
        <div class="skeleton-text">
          @for (line of linesArray; track $index) {
            <div
              class="skeleton-line"
              [ngClass]="{ 'skeleton-line-short': $index === lines - 1 }">
            </div>
          }
        </div>
      }
      @case ('circular') {
        <div
          class="skeleton skeleton-circular"
          [style.width]="width"
          [style.height]="height || width">
        </div>
      }
      @case ('rectangular') {
        <div
          class="skeleton skeleton-rectangular"
          [style.width]="width"
          [style.height]="height">
        </div>
      }
      @case ('card') {
        <div class="skeleton-card">
          <div class="skeleton-card-header">
            <div class="skeleton skeleton-circular" style="width: 40px; height: 40px;"></div>
            <div class="skeleton-card-header-text">
              <div class="skeleton" style="width: 60%; height: 16px;"></div>
              <div class="skeleton" style="width: 40%; height: 12px;"></div>
            </div>
          </div>
          <div class="skeleton-card-body">
            <div class="skeleton" style="width: 100%; height: 12px;"></div>
            <div class="skeleton" style="width: 90%; height: 12px;"></div>
            <div class="skeleton" style="width: 75%; height: 12px;"></div>
          </div>
        </div>
      }
      @case ('table-row') {
        <div class="skeleton-table-row">
          @for (col of columnsArray; track $index) {
            <div class="skeleton skeleton-cell"></div>
          }
        </div>
      }
      @default {
        <div class="skeleton" [style.width]="width" [style.height]="height"></div>
      }
    }
  `,
  styles: [`
    .skeleton {
      background: linear-gradient(
        90deg,
        hsl(var(--muted)) 0%,
        hsl(var(--muted-foreground) / 0.1) 50%,
        hsl(var(--muted)) 100%
      );
      background-size: 200% 100%;
      animation: shimmer 1.5s infinite;
      border-radius: 0.25rem;
    }

    @keyframes shimmer {
      0% { background-position: 200% 0; }
      100% { background-position: -200% 0; }
    }

    .skeleton-circular {
      border-radius: 50%;
    }

    .skeleton-rectangular {
      border-radius: 0.5rem;
    }

    .skeleton-text {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .skeleton-line {
      height: 14px;
      width: 100%;
    }

    .skeleton-line-short {
      width: 60% !important;
    }

    .skeleton-card {
      padding: 1rem;
      border: 1px solid hsl(var(--border));
      border-radius: 0.5rem;
    }

    .skeleton-card-header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 1rem;
    }

    .skeleton-card-header-text {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .skeleton-card-body {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .skeleton-table-row {
      display: grid;
      gap: 1rem;
      padding: 0.75rem 1rem;
    }

    .skeleton-cell {
      height: 16px;
      border-radius: 0.25rem;
    }
  `]
})
export class SkeletonComponent {
  @Input() variant: SkeletonVariant = 'rectangular';
  @Input() width = '100%';
  @Input() height = '100px';
  @Input() lines = 3;
  @Input() columns = 4;

  get linesArray(): number[] {
    return Array(this.lines).fill(0).map((_, i) => i);
  }

  get columnsArray(): number[] {
    return Array(this.columns).fill(0).map((_, i) => i);
  }
}

/**
 * Componente para mostrar estado de carga de lista.
 */
@Component({
  selector: 'app-skeleton-list',
  standalone: true,
  imports: [SkeletonComponent],
  template: `
    <div class="skeleton-list">
      @for (item of itemsArray; track $index) {
        <app-skeleton variant="card"></app-skeleton>
      }
    </div>
  `,
  styles: [`
    .skeleton-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
  `]
})
export class SkeletonListComponent {
  @Input() count = 3;

  get itemsArray(): number[] {
    return Array(this.count).fill(0).map((_, i) => i);
  }
}

/**
 * Componente para mostrar estado de carga de dashboard.
 */
@Component({
  selector: 'app-skeleton-dashboard',
  standalone: true,
  imports: [SkeletonComponent],
  template: `
    <div class="skeleton-dashboard">
      <div class="skeleton-kpi-grid">
        @for (i of [1,2,3,4]; track i) {
          <app-skeleton variant="card"></app-skeleton>
        }
      </div>
      <div class="skeleton-main">
        <app-skeleton variant="card" style="flex: 2;"></app-skeleton>
        <app-skeleton variant="card" style="flex: 1;"></app-skeleton>
      </div>
    </div>
  `,
  styles: [`
    .skeleton-dashboard {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }

    .skeleton-kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 1rem;
    }

    .skeleton-main {
      display: flex;
      gap: 1rem;
    }

    @media (max-width: 768px) {
      .skeleton-main {
        flex-direction: column;
      }
    }
  `]
})
export class SkeletonDashboardComponent {}
