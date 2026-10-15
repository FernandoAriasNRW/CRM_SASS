import { Component, Input, Output, EventEmitter, ElementRef, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { animate, style, transition, trigger } from '@angular/animations';

@Component({
  selector: 'app-popover',
  standalone: true,
  imports: [CommonModule],
  animations: [
    trigger('popoverAnimation', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scale(0.95) translateY(-10px)' }),
        animate('150ms cubic-bezier(0.16, 1, 0.3, 1)', style({ opacity: 1, transform: 'scale(1) translateY(0)' }))
      ]),
      transition(':leave', [
        animate('100ms ease-in', style({ opacity: 0, transform: 'scale(0.95) translateY(-10px)' }))
      ])
    ])
  ],
  template: `
    <!-- Recoge el clic que sube del disparador proyectado, que ya es un botón con su teclado. -->
    <!-- eslint-disable-next-line @angular-eslint/template/click-events-have-key-events, @angular-eslint/template/interactive-supports-focus -->
    <div class="relative inline-block" (click)="toggle($event)">
      <ng-content select="[popover-trigger]"></ng-content>
    
      @if (isOpen) {
        <!-- Sólo evita que un clic dentro del panel lo cierre; no es un control. -->
        <!-- eslint-disable-next-line @angular-eslint/template/click-events-have-key-events, @angular-eslint/template/interactive-supports-focus -->
        <div
          @popoverAnimation
          class="absolute z-50 mt-2 min-w-[200px] bg-background border border-border rounded-md shadow-md p-1"
          [ngClass]="positionClass"
          (click)="$event.stopPropagation()">
          <ng-content select="[popover-content]"></ng-content>
        </div>
      }
    </div>
    `
})
export class PopoverComponent {
  @Input() isOpen = false;
  @Input() position: 'bottom-left' | 'bottom-right' | 'bottom-center' = 'bottom-left';
  @Output() isOpenChange = new EventEmitter<boolean>();

  constructor(private eRef: ElementRef) {}

  get positionClass() {
    switch (this.position) {
      case 'bottom-right': return 'right-0 origin-top-right';
      case 'bottom-center': return 'left-1/2 -translate-x-1/2 origin-top';
      case 'bottom-left':
      default: return 'left-0 origin-top-left';
    }
  }

  toggle(event: Event) {
    event.stopPropagation();
    this.isOpen = !this.isOpen;
    this.isOpenChange.emit(this.isOpen);
  }

  @HostListener('document:click', ['$event'])
  clickout(event: Event) {
    if (this.isOpen && !this.eRef.nativeElement.contains(event.target)) {
      this.isOpen = false;
      this.isOpenChange.emit(false);
    }
  }
}
