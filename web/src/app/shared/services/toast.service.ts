import { Injectable, signal, computed } from '@angular/core';

/**
 * Tipos de toast disponibles
 */
export type ToastType = 'success' | 'error' | 'warning' | 'info';

/**
 * Interfaz para un toast
 */
export interface Toast {
  id: string;
  type: ToastType;
  title: string;
  message?: string;
  duration: number;
  dismissible: boolean;
}

/**
 * Servicio centralizado para mostrar notificaciones toast.
 * Usa signals para reactivity y permite mostrar mensajes de error, éxito, etc.
 *
 * Uso:
 * ```typescript
 * constructor(private toast: ToastService) {}
 *
 * // Mostrar error
 * this.toast.error('Error', 'No se pudo guardar el proyecto');
 *
 * // Mostrar éxito
 * this.toast.success('Éxito', 'Proyecto guardado correctamente');
 * ```
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  // Lista de toasts activos
  private readonly _toasts = signal<Toast[]>([]);

  // Toasts computados (solo lectura)
  readonly toasts = computed(() => this._toasts());

  // Configuración por defecto
  private readonly defaultDuration = 5000; // 5 segundos
  private readonly maxToasts = 5; // Máximo de toasts simultáneos

  /**
   * Muestra un toast de éxito
   */
  success(title: string, message?: string, duration?: number): void {
    this.show('success', title, message, duration);
  }

  /**
   * Muestra un toast de error
   */
  error(title: string, message?: string, duration?: number): void {
    // Errores duran más para que el usuario pueda leerlos
    this.show('error', title, message, duration ?? 8000);
  }

  /**
   * Muestra un toast de advertencia
   */
  warning(title: string, message?: string, duration?: number): void {
    this.show('warning', title, message, duration);
  }

  /**
   * Muestra un toast informativo
   */
  info(title: string, message?: string, duration?: number): void {
    this.show('info', title, message, duration);
  }

  /**
   * Método genérico para mostrar un toast
   */
  private show(type: ToastType, title: string, message?: string, duration?: number): void {
    const id = this.generateId();
    const toast: Toast = {
      id,
      type,
      title,
      message,
      duration: duration ?? this.defaultDuration,
      dismissible: true
    };

    // Agregar toast
    this._toasts.update(toasts => {
      // Limitar número de toasts
      const newToasts = [...toasts, toast];
      if (newToasts.length > this.maxToasts) {
        return newToasts.slice(-this.maxToasts);
      }
      return newToasts;
    });

    // Auto-dismiss después de duration
    if (toast.duration > 0) {
      setTimeout(() => this.dismiss(id), toast.duration);
    }
  }

  /**
   * Dismiss un toast específico por ID
   */
  dismiss(id: string): void {
    this._toasts.update(toasts => toasts.filter(t => t.id !== id));
  }

  /**
   * Dismiss todos los toasts
   */
  dismissAll(): void {
    this._toasts.set([]);
  }

  /**
   * Genera un ID único para el toast
   */
  private generateId(): string {
    return `toast-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;
  }

  /**
   * Helper para errores HTTP comunes
   */
  handleHttpError(error: any, fallbackTitle = 'Error'): void {
    let title = fallbackTitle;
    let message: string | undefined;

    if (error?.status === 0) {
      title = 'Sin conexión';
      message = 'No se pudo conectar con el servidor. Verifica tu conexión a internet.';
    } else if (error?.status === 401) {
      title = 'Sesión expirada';
      message = 'Por favor, inicia sesión nuevamente.';
    } else if (error?.status === 403) {
      title = 'Acceso denegado';
      message = 'No tienes permisos para realizar esta acción.';
    } else if (error?.status === 404) {
      title = 'No encontrado';
      message = 'El recurso solicitado no existe.';
    } else if (error?.status === 429) {
      title = 'Demasiadas solicitudes';
      message = 'Has superado el límite de solicitudes. Intenta más tarde.';
    } else if (error?.status >= 500) {
      title = 'Error del servidor';
      message = 'Ocurrió un error en el servidor. Intenta más tarde.';
    } else if (error?.error?.message) {
      // Error con mensaje del servidor
      message = error.error.message;
    } else if (error?.message) {
      message = error.message;
    } else {
      message = 'Ocurrió un error inesperado.';
    }

    this.error(title, message);
  }
}
