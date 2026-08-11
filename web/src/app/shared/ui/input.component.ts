import { Component, HostBinding } from '@angular/core';
import { cn } from '../utils/cn';

@Component({
  selector: 'input[uiInput]',
  standalone: true,
  template: '',
})
export class InputComponent {
  @HostBinding('class') classes = cn(
    'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors',
    'file:border-0 file:bg-transparent file:text-sm file:font-medium',
    'placeholder:text-muted-foreground',
    'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring',
    'disabled:cursor-not-allowed disabled:opacity-50'
  );
}
