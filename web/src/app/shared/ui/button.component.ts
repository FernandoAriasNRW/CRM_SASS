import { Component, input, output, HostBinding, HostListener, computed, Input } from '@angular/core';
import { NgIf } from '@angular/common';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '../utils/cn';

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50',
  {
    variants: {
      variant: {
        default: 'bg-primary text-primary-foreground hover:bg-primary/90',
        destructive: 'bg-destructive text-destructive-foreground hover:bg-destructive/90',
        outline: 'border border-input bg-background hover:bg-accent hover:text-accent-foreground',
        secondary: 'bg-secondary text-secondary-foreground hover:bg-secondary/80',
        ghost: 'hover:bg-accent hover:text-accent-foreground',
        link: 'text-primary underline-offset-4 hover:underline',
      },
      size: {
        default: 'h-9 px-4 py-2',
        sm: 'h-8 rounded-md px-3 text-xs',
        lg: 'h-10 rounded-md px-8',
        icon: 'h-9 w-9',
      },
    },
    defaultVariants: { variant: 'default', size: 'default' },
  }
);

export type ButtonVariant = VariantProps<typeof buttonVariants>['variant'];
export type ButtonSize = VariantProps<typeof buttonVariants>['size'];

@Component({
  selector: 'button[uiButton], a[uiButton]',
  standalone: true,
  imports: [NgIf],
  template: `
    @if (loading()) {
      <span class="button-spinner"></span>
    }
    <ng-content></ng-content>
  `,
  styles: [`
    :host {
      display: inline-flex;
      position: relative;
    }

    .button-spinner {
      width: 16px;
      height: 16px;
      border: 2px solid currentColor;
      border-right-color: transparent;
      border-radius: 50%;
      animation: spin 0.6s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    :host(.loading) .button-spinner + * {
      opacity: 0.7;
    }
  `]
})
export class ButtonComponent {
  variant = input<ButtonVariant>('default');
  size = input<ButtonSize>('default');
  disabled = input<boolean>(false);

  /**
   * Estado de carga. Cuando está en true, muestra un spinner
   * y deshabilita el botón.
   */
  loading = input<boolean>(false);

  @HostBinding('class') get classes() {
    return cn(
      buttonVariants({ variant: this.variant(), size: this.size() }),
      { 'loading': this.loading() }
    );
  }

  @HostBinding('disabled') get isDisabled() {
    return this.disabled() || this.loading() || null;
  }
}
