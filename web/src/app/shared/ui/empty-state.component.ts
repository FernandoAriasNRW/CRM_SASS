import { Component, Input } from '@angular/core';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideInbox,
  lucideSearch,
  lucideFileQuestion,
  lucideFolderOpen,
  lucideUsers,
  lucideCalendar
} from '@ng-icons/lucide';

/**
 * Tipos predefinidos de estado vacío
 */
export type EmptyStateType =
  | 'general'      // Sin ícono específico
  | 'inbox'        // Bandeja de entrada vacía
  | 'search'       // Sin resultados de búsqueda
  | 'no-data'      // Sin datos disponibles
  | 'no-projects'  // Sin proyectos
  | 'no-users'     // Sin usuarios
  | 'no-events';   // Sin eventos (calendario)

/**
 * Interfaz para configuración de EmptyState
 */
export interface EmptyStateConfig {
  icon: string;
  title: string;
  description?: string;
  actionLabel?: string;
}

/**
 * Componente para mostrar estado vacío cuando no hay datos.
 * Usado en listas, búsquedas, dashboards vacíos, etc.
 *
 * Uso:
 * ```html
 * <!-- Estado vacío genérico -->
 * <app-empty-state
 *   title="No hay elementos"
 *   description="Comienza agregando uno nuevo">
 * </app-empty-state>
 *
 * <!-- Estado vacío predefinido para proyectos -->
 * <app-empty-state type="no-projects"></app-empty-state>
 *
 * <!-- Con acción -->
 * <app-empty-state
 *   type="no-data"
 *   (actionClick)="createNew()">
 * </app-empty-state>
 * ```
 */
@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [NgIconComponent],
  viewProviders: [provideIcons({
    lucideInbox,
    lucideSearch,
    lucideFileQuestion,
    lucideFolderOpen,
    lucideUsers,
    lucideCalendar
  })],
  template: `
    <div class="empty-state" role="status" aria-live="polite">
      <!-- Ícono -->
      <div class="empty-state-icon">
        @if (config.icon) {
          <ng-icon [name]="config.icon" [size]="iconSize"></ng-icon>
        }
      </div>

      <!-- Contenido -->
      <div class="empty-state-content">
        <h3 class="empty-state-title">{{ config.title }}</h3>

        @if (config.description) {
          <p class="empty-state-description">{{ config.description }}</p>
        }

        <!-- Acción opcional -->
        @if (showAction && config.actionLabel) {
          <button
            class="empty-state-action"
            (click)="onActionClick()">
            {{ config.actionLabel }}
          </button>
        }
      </div>
    </div>
  `,
  styles: [`
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 3rem 1.5rem;
      text-align: center;
    }

    .empty-state-icon {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 4rem;
      height: 4rem;
      border-radius: 50%;
      background: hsl(var(--muted));
      color: hsl(var(--muted-foreground));
      margin-bottom: 1.5rem;
    }

    .empty-state-content {
      max-width: 320px;
    }

    .empty-state-title {
      font-size: 1.125rem;
      font-weight: 600;
      color: hsl(var(--foreground));
      margin: 0 0 0.5rem 0;
    }

    .empty-state-description {
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
      margin: 0 0 1.5rem 0;
      line-height: 1.5;
    }

    .empty-state-action {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 0.5rem 1rem;
      font-size: 0.875rem;
      font-weight: 500;
      color: hsl(var(--primary-foreground));
      background: hsl(var(--primary));
      border: none;
      border-radius: 0.375rem;
      cursor: pointer;
      transition: background-color 0.2s;
    }

    .empty-state-action:hover {
      background: hsl(var(--primary) / 0.9;
    }

    .empty-state-action:focus {
      outline: 2px solid hsl(var(--ring));
      outline-offset: 2px;
    }
  `]
})
export class EmptyStateComponent {
  /** Tipo predefinido de estado vacío */
  @Input() type: EmptyStateType = 'general';

  /** Título personalizado (sobrescribe el del tipo) */
  @Input() title?: string;

  /** Descripción personalizada */
  @Input() description?: string;

  /** Label del botón de acción */
  @Input() actionLabel?: string;

  /** Tamaño del ícono (en px) */
  @Input() iconSize = 48;

  /** Si mostrar el botón de acción */
  @Input() showAction = true;

  /** Evento cuando se hace click en la acción */
  @Input() actionClick: (() => void) | null = null;

  private readonly presets: Record<EmptyStateType, EmptyStateConfig> = {
    'general': {
      icon: 'lucideInbox',
      title: 'No hay elementos',
      description: 'No hay nada que mostrar aquí todavía.'
    },
    'inbox': {
      icon: 'lucideInbox',
      title: 'Tu bandeja está vacía',
      description: 'Cuando recibas notificaciones, aparecerán aquí.'
    },
    'search': {
      icon: 'lucideSearch',
      title: 'Sin resultados',
      description: 'No encontramos nada con esos criterios de búsqueda. Intenta con otros términos.'
    },
    'no-data': {
      icon: 'lucideFileQuestion',
      title: 'Sin datos disponibles',
      description: 'Aún no hay datos para mostrar. Los datos aparecerán cuando estén disponibles.'
    },
    'no-projects': {
      icon: 'lucideFolderOpen',
      title: 'Sin proyectos',
      description: 'Aún no tienes proyectos. Crea tu primer proyecto para comenzar a organizar tu trabajo.',
      actionLabel: 'Crear proyecto'
    },
    'no-users': {
      icon: 'lucideUsers',
      title: 'Sin usuarios',
      description: 'Aún no hay usuarios registrados en este tenant.',
      actionLabel: 'Invitar usuario'
    },
    'no-events': {
      icon: 'lucideCalendar',
      title: 'Sin eventos',
      description: 'No hay eventos programados para este período.',
      actionLabel: 'Crear evento'
    }
  };

  get config(): EmptyStateConfig {
    const preset = this.presets[this.type];

    return {
      icon: preset.icon,
      title: this.title ?? preset.title,
      description: this.description ?? preset.description,
      actionLabel: this.actionLabel ?? preset.actionLabel
    };
  }

  onActionClick(): void {
    if (this.actionClick) {
      this.actionClick();
    }
  }
}

/**
 * Componente inline para estado vacío en línea (inline text).
 * Útil para mostrar dentro de contenedores pequeños.
 */
@Component({
  selector: 'app-empty-inline',
  standalone: true,
  imports: [NgIconComponent],
  viewProviders: [provideIcons({ lucideInbox })],
  template: `
    <div class="empty-inline">
      <ng-icon name="lucideInbox" size="16"></ng-icon>
      <span>{{ message }}</span>
    </div>
  `,
  styles: [`
    .empty-inline {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.5rem 1rem;
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
      background: hsl(var(--muted));
      border-radius: 0.375rem;
    }
  `]
})
export class EmptyInlineComponent {
  @Input() message = 'No hay datos';
}
