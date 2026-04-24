import { Component, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideCheckCircle,
  lucideXCircle,
  lucideAlertTriangle,
  lucideInfo,
  lucideX
} from '@ng-icons/lucide';
import { ToastService, type Toast, type ToastType } from '../services/toast.service';

/**
 * Componente contenedor de toasts.
 * Se debe agregar una sola vez en app.component o layout principal.
 *
 * Muestra toasts en la esquina superior derecha de la pantalla.
 */
@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [NgClass, NgIconComponent],
  viewProviders: [provideIcons({
    lucideCheckCircle,
    lucideXCircle,
    lucideAlertTriangle,
    lucideInfo,
    lucideX
  })],
  template: `
    <div class="toast-container">
      @for (toast of toastService.toasts(); track toast.id) {
        <div
          class="toast"
          [ngClass]="'toast-' + toast.type"
          role="alert"
          [attr.aria-live]="'polite'">

          <!-- Icono -->
          <div class="toast-icon">
            @switch (toast.type) {
              @case ('success') {
                <ng-icon name="lucideCheckCircle" size="20"></ng-icon>
              }
              @case ('error') {
                <ng-icon name="lucideXCircle" size="20"></ng-icon>
              }
              @case ('warning') {
                <ng-icon name="lucideAlertTriangle" size="20"></ng-icon>
              }
              @case ('info') {
                <ng-icon name="lucideInfo" size="20"></ng-icon>
              }
            }
          </div>

          <!-- Contenido -->
          <div class="toast-content">
            <div class="toast-title">{{ toast.title }}</div>
            @if (toast.message) {
              <div class="toast-message">{{ toast.message }}</div>
            }
          </div>

          <!-- Dismiss button -->
          @if (toast.dismissible) {
            <button
              class="toast-dismiss"
              (click)="dismiss(toast.id)"
              aria-label="Cerrar notificación">
              <ng-icon name="lucideX" size="16"></ng-icon>
            </button>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .toast-container {
      position: fixed;
      top: 1rem;
      right: 1rem;
      z-index: 9999;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      max-width: 400px;
      width: 100%;
      pointer-events: none;
    }

    .toast {
      display: flex;
      align-items: flex-start;
      gap: 0.75rem;
      padding: 1rem;
      border-radius: 0.5rem;
      box-shadow: 0 10px 15px -3px rgb(0 0 0 / 0.1), 0 4px 6px -4px rgb(0 0 0 / 0.1);
      pointer-events: auto;
      animation: slideIn 0.3s ease-out;
      background: white;
      border-left: 4px solid;
    }

    @keyframes slideIn {
      from {
        transform: translateX(100%);
        opacity: 0;
      }
      to {
        transform: translateX(0);
        opacity: 1;
      }
    }

    .toast-success {
      border-color: #10b981;
      background: #ecfdf5;
    }

    .toast-success .toast-icon {
      color: #10b981;
    }

    .toast-error {
      border-color: #ef4444;
      background: #fef2f2;
    }

    .toast-error .toast-icon {
      color: #ef4444;
    }

    .toast-warning {
      border-color: #f59e0b;
      background: #fffbeb;
    }

    .toast-warning .toast-icon {
      color: #f59e0b;
    }

    .toast-info {
      border-color: #3b82f6;
      background: #eff6ff;
    }

    .toast-info .toast-icon {
      color: #3b82f6;
    }

    .toast-icon {
      flex-shrink: 0;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .toast-content {
      flex: 1;
      min-width: 0;
    }

    .toast-title {
      font-weight: 600;
      font-size: 0.875rem;
      color: #1f2937;
    }

    .toast-message {
      font-size: 0.8125rem;
      color: #4b5563;
      margin-top: 0.25rem;
      line-height: 1.4;
    }

    .toast-dismiss {
      flex-shrink: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      width: 1.5rem;
      height: 1.5rem;
      border: none;
      background: transparent;
      color: #9ca3af;
      cursor: pointer;
      border-radius: 0.25rem;
      transition: all 0.2s;
    }

    .toast-dismiss:hover {
      background: rgba(0, 0, 0, 0.05);
      color: #6b7280;
    }

    /* Responsive */
    @media (max-width: 480px) {
      .toast-container {
        top: 0.5rem;
        right: 0.5rem;
        left: 0.5rem;
        max-width: none;
      }
    }
  `]
})
export class ToastContainerComponent {
  protected readonly toastService = inject(ToastService);

  dismiss(id: string): void {
    this.toastService.dismiss(id);
  }
}
