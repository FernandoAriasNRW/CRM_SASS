import { Component, Input, Output, EventEmitter, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideX } from '@ng-icons/lucide';
import { animate, style, transition, trigger } from '@angular/animations';

@Component({
  selector: 'app-drawer',
  standalone: true,
  imports: [CommonModule, NgIconComponent],
  viewProviders: [provideIcons({ lucideX })],
  animations: [
    trigger('backdropAnimation', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('200ms ease-out', style({ opacity: 1 }))
      ]),
      transition(':leave', [
        animate('200ms ease-in', style({ opacity: 0 }))
      ])
    ]),
    trigger('drawerAnimation', [
      transition(':enter', [
        style({ transform: 'translateX(100%)', opacity: 0 }),
        animate('300ms cubic-bezier(0.16, 1, 0.3, 1)', style({ transform: 'translateX(0)', opacity: 1 }))
      ]),
      transition(':leave', [
        animate('200ms cubic-bezier(0.16, 1, 0.3, 1)', style({ transform: 'translateX(100%)', opacity: 0 }))
      ])
    ])
  ],
  template: `
    <div *ngIf="isOpen" class="fixed inset-0 z-50 flex justify-end overflow-hidden">
      <!-- Backdrop -->
      <div 
        @backdropAnimation
        class="absolute inset-0 bg-black/40 backdrop-blur-sm"
        (click)="close()"
        aria-hidden="true"
      ></div>

      <!-- Drawer Panel -->
      <div 
        @drawerAnimation
        class="relative w-full max-w-3xl h-full bg-background border-l border-border shadow-2xl flex flex-col"
        role="dialog" 
        aria-modal="true"
      >
        <!-- Header -->
        <div class="flex items-center justify-between px-6 py-4 border-b border-border/50 shrink-0">
          <ng-content select="[drawer-header]">
            <h2 class="text-xl font-semibold text-foreground tracking-tight">{{ title }}</h2>
          </ng-content>
          <button 
            (click)="close()"
            class="p-2 text-muted-foreground hover:bg-muted hover:text-foreground rounded-md transition-colors shrink-0 ml-4"
          >
            <ng-icon name="lucideX" class="w-5 h-5"></ng-icon>
          </button>
        </div>

        <!-- Body -->
        <div class="flex-1 overflow-y-auto p-0 bg-background flex flex-col">
          <ng-content></ng-content>
        </div>
        
        <!-- Footer (Optional) -->
        <div *ngIf="showFooter" class="px-6 py-4 border-t border-border bg-muted/20">
           <ng-content select="[drawer-footer]"></ng-content>
        </div>
      </div>
    </div>
  `
})
export class DrawerComponent {
  @Input() isOpen = false;
  @Input() title = '';
  @Input() showFooter = false;
  
  @Output() isOpenChange = new EventEmitter<boolean>();
  @Output() onClose = new EventEmitter<void>();

  close() {
    this.isOpen = false;
    this.isOpenChange.emit(this.isOpen);
    this.onClose.emit();
  }

  @HostListener('document:keydown.escape', ['$event'])
  onKeydownHandler(event: Event) {
    if (this.isOpen) {
      this.close();
    }
  }
}
