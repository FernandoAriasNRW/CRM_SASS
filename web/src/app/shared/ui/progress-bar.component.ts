import { Component, input } from '@angular/core';

@Component({
  selector: 'app-progress-bar',
  standalone: true,
  template: `
    <div class="space-y-1">
      <div class="flex justify-between text-xs">
        <span class="font-medium">{{ label() }}</span>
        <span class="text-muted-foreground">{{ value() }}%</span>
      </div>
      <div class="h-2 bg-secondary rounded-full overflow-hidden">
        <div
          class="h-full rounded-full transition-all duration-500"
          [style.width.%]="value()"
          [class]="colorClass()">
        </div>
      </div>
      <div class="flex justify-between text-xs text-muted-foreground">
        <span>{{ doneCount() }} completadas</span>
        <span>{{ totalCount() }} total</span>
      </div>
    </div>
  `
})
export class ProgressBarComponent {
  readonly label = input.required<string>();
  readonly value = input.required<number>();
  readonly doneCount = input<number>(0);
  readonly totalCount = input<number>(0);

  colorClass(): string {
    const pct = this.value();
    if (pct >= 80) return 'bg-success';
    if (pct >= 50) return 'bg-primary';
    if (pct >= 25) return 'bg-warning';
    return 'bg-muted-foreground';
  }
}
