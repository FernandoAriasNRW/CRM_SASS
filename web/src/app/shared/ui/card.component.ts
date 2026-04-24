import { Component, HostBinding } from '@angular/core';
import { cn } from '../utils/cn';

@Component({
  selector: 'ui-card',
  standalone: true,
  template: '<ng-content />',
})
export class CardComponent {
  @HostBinding('class') classes = cn('rounded-lg border bg-card text-card-foreground shadow-sm block');
}

@Component({
  selector: 'ui-card-header',
  standalone: true,
  template: '<ng-content />',
})
export class CardHeaderComponent {
  @HostBinding('class') classes = cn('flex flex-col space-y-1.5 p-6 block');
}

@Component({
  selector: 'ui-card-title',
  standalone: true,
  template: '<ng-content />',
})
export class CardTitleComponent {
  @HostBinding('class') classes = cn('text-2xl font-semibold leading-none tracking-tight block');
}

@Component({
  selector: 'ui-card-description',
  standalone: true,
  template: '<ng-content />',
})
export class CardDescriptionComponent {
  @HostBinding('class') classes = cn('text-sm text-muted-foreground block');
}

@Component({
  selector: 'ui-card-content',
  standalone: true,
  template: '<ng-content />',
})
export class CardContentComponent {
  @HostBinding('class') classes = cn('p-6 pt-0 block');
}

@Component({
  selector: 'ui-card-footer',
  standalone: true,
  template: '<ng-content />',
})
export class CardFooterComponent {
  @HostBinding('class') classes = cn('flex items-center p-6 pt-0 block');
}
