import { Component, HostBinding } from '@angular/core';
import { cn } from '../utils/cn';

@Component({
  selector: 'label[uiLabel]',
  standalone: true,
  template: '<ng-content />',
})
export class LabelComponent {
  @HostBinding('class') classes = cn(
    'text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70'
  );
}
