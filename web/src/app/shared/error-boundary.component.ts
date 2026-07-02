import { Component, ErrorHandler, inject, Injectable } from '@angular/core';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideAlertTriangle, lucideRefreshCw, lucideHome, lucideBug } from '@ng-icons/lucide';
import { RouterLink } from '@angular/router';

/**
 * Error Boundary Component
 * Catches and displays errors in the Angular application.
 */
@Component({
  selector: 'app-error-boundary',
  standalone: true,
  imports: [NgIconComponent, RouterLink],
  viewProviders: [
    provideIcons({ lucideAlertTriangle, lucideRefreshCw, lucideHome, lucideBug })
  ],
  template: `
    <div class="min-h-screen bg-background flex items-center justify-center p-4">
      <div class="max-w-md w-full text-center space-y-6">
        <!-- Error Icon -->
        <div class="mx-auto w-20 h-20 rounded-full bg-destructive/10 flex items-center justify-center">
          <ng-icon name="lucideAlertTriangle" size="40" class="text-destructive" />
        </div>

        <!-- Error Title -->
        <div class="space-y-2">
          <h1 class="text-2xl font-bold text-foreground">Algo salió mal</h1>
          <p class="text-muted-foreground">
            Lo sentimos, occurredió un error inesperado. Por favor intenta de nuevo.
          </p>
        </div>

        <!-- Error Details (only in development) -->
        @if (isDevelopment) {
          <div class="bg-card border border-border rounded-lg p-4 text-left">
            <div class="flex items-center gap-2 mb-2">
              <ng-icon name="lucideBug" size="16" class="text-muted-foreground" />
              <span class="text-xs font-medium text-muted-foreground uppercase">Detalles del error</span>
            </div>
            <code class="text-xs text-destructive break-all">{{ errorMessage }}</code>
          </div>
        }

        <!-- Stack Trace (only in development) -->
        @if (isDevelopment && stackTrace) {
          <div class="bg-card border border-border rounded-lg p-4 text-left max-h-48 overflow-auto">
            <span class="text-xs font-medium text-muted-foreground mb-1 block">Stack trace:</span>
            <pre class="text-xs text-muted-foreground whitespace-pre-wrap break-all">{{ stackTrace }}</pre>
          </div>
        }

        <!-- Actions -->
        <div class="flex flex-col sm:flex-row gap-3 justify-center">
          <button
            (click)="reloadPage()"
            class="inline-flex items-center justify-center gap-2 px-6 py-3 bg-primary text-primary-foreground rounded-md font-medium hover:bg-primary/90 transition-colors"
          >
            <ng-icon name="lucideRefreshCw" size="16" />
            Recargar página
          </button>
          <a
            routerLink="/"
            class="inline-flex items-center justify-center gap-2 px-6 py-3 bg-secondary text-secondary-foreground rounded-md font-medium hover:bg-secondary/80 transition-colors"
          >
            <ng-icon name="lucideHome" size="16" />
            Ir al inicio
          </a>
        </div>

        <!-- Report Error Link -->
        <p class="text-xs text-muted-foreground">
          Si el problema persiste,
          <a href="mailto:soporte@acme.com" class="text-primary hover:underline">
            contacta al soporte
          </a>
        </p>
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: block;
    }
  `]
})
export class ErrorBoundaryComponent {
  errorMessage = '';
  stackTrace = '';
  isDevelopment = true; // Default to true, can be controlled via environment

  /**
   * Reloads the current page
   */
  reloadPage(): void {
    window.location.reload();
  }

  /**
   * Static method to handle errors from Angular's error handler.
   */
  static handleError(error: Error | unknown): void {
    console.error('Application Error:', error);
  }
}

/**
 * Global Error Handler Service
 * Provides centralized error handling for the Angular application.
 */
@Injectable({ providedIn: 'root' })
export class GlobalErrorHandler implements ErrorHandler {
  private errorLogService = inject(ErrorLogService);

  handleError(error: Error | unknown): void {
    const errorMessage = error instanceof Error ? error.message : String(error);
    const stackTrace = error instanceof Error ? error.stack ?? '' : '';

    // Log to error service
    this.errorLogService.logError({
      message: errorMessage,
      stackTrace,
      timestamp: new Date(),
      userAgent: navigator.userAgent,
      url: window.location.href,
    });

    // Log to console
    console.error('Global Error Handler:', error);
  }
}

/**
 * Error Log Service
 * Collects and manages application errors.
 */
@Injectable({ providedIn: 'root' })
export class ErrorLogService {
  private readonly maxLogs = 50;
  private logs: ErrorLog[] = [];

  logError(error: ErrorLogEntry): void {
    const log: ErrorLog = {
      id: crypto.randomUUID(),
      message: error.message,
      stackTrace: error.stackTrace,
      timestamp: error.timestamp,
      userAgent: error.userAgent,
      url: error.url,
    };

    this.logs.unshift(log);

    // Keep only the last maxLogs entries
    if (this.logs.length > this.maxLogs) {
      this.logs.pop();
    }
  }

  getLogs(): ErrorLog[] {
    return [...this.logs];
  }

  clearLogs(): void {
    this.logs = [];
  }
}

interface ErrorLog {
  id: string;
  message: string;
  stackTrace: string;
  timestamp: Date;
  userAgent: string;
  url: string;
}

interface ErrorLogEntry {
  message: string;
  stackTrace: string;
  timestamp: Date;
  userAgent: string;
  url: string;
}