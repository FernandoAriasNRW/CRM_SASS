import { Component, input, HostBinding } from '@angular/core';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '../utils/cn';

const badgeVariants = cva(
  'inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold transition-colors',
  {
    variants: {
      variant: {
        default: 'border-transparent bg-primary text-primary-foreground',
        secondary: 'border-transparent bg-secondary text-secondary-foreground',
        destructive: 'border-transparent bg-destructive text-destructive-foreground',
        outline: 'text-foreground',
        success: 'border-transparent bg-green-100 text-green-800',
        warning: 'border-transparent bg-yellow-100 text-yellow-800',
      },
    },
    defaultVariants: { variant: 'default' },
  }
);

export type BadgeVariant = VariantProps<typeof badgeVariants>['variant'];

@Component({
  selector: 'ui-badge',
  standalone: true,
  template: '<ng-content />',
})
export class BadgeComponent {
  variant = input<BadgeVariant>('default');

  @HostBinding('class') get classes() {
    return cn(badgeVariants({ variant: this.variant() }));
  }
}
