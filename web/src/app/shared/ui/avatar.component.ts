import { Component, input, HostBinding } from '@angular/core';
import { cn } from '../utils/cn';

@Component({
  selector: 'ui-avatar',
  standalone: true,
  template: `
    @if (src()) {
      <img [src]="src()" [alt]="alt()" class="h-full w-full object-cover" />
    } @else {
      <span class="flex h-full w-full items-center justify-center text-sm font-medium uppercase">
        {{ initials() }}
      </span>
    }
  `,
})
export class AvatarComponent {
  src = input<string>('');
  alt = input<string>('');
  name = input<string>('');

  @HostBinding('class') classes = cn(
    'relative flex h-9 w-9 shrink-0 overflow-hidden rounded-full bg-muted inline-flex'
  );

  initials() {
    return this.name()
      .split(' ')
      .map(n => n[0])
      .slice(0, 2)
      .join('');
  }
}
