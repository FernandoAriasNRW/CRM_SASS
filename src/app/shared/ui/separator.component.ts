import { Component, input, HostBinding } from '@angular/core';
import { cn } from '../utils/cn';

@Component({
  selector: 'ui-separator',
  standalone: true,
  template: '',
})
export class SeparatorComponent {
  orientation = input<'horizontal' | 'vertical'>('horizontal');

  @HostBinding('class') get classes() {
    return cn(
      'shrink-0 bg-border block',
      this.orientation() === 'horizontal' ? 'h-[1px] w-full' : 'h-full w-[1px]'
    );
  }
}
