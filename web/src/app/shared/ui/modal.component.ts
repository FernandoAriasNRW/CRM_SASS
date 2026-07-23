import { Component, Input, Output, EventEmitter, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideX } from '@ng-icons/lucide';
import { animate, style, transition, trigger } from '@angular/animations';

@Component({
  selector: 'app-modal',
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
    trigger('modalAnimation', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scale(0.95)' }),
        animate('200ms cubic-bezier(0.16, 1, 0.3, 1)', style({ opacity: 1, transform: 'scale(1)' }))
      ]),
      transition(':leave', [
        animate('150ms cubic-bezier(0.16, 1, 0.3, 1)', style({ opacity: 0, transform: 'scale(0.95)' }))
      ])
    ])
  ],
  template: `
    <div *ngIf="isOpen" class="fixed inset-0 z-[60] flex items-center justify-center p-4 sm:p-6">
      <!-- Backdrop -->
      <div 
        @backdropAnimation
        class="absolute inset-0 bg-black/50 backdrop-blur-sm"
        (click)="close()"
        aria-hidden="true"
      ></div>

      <!-- Modal Panel -->
      <div 
        @modalAnimation
        class="relative w-full max-w-lg bg-background rounded-xl shadow-2xl border border-border/50 flex flex-col max-h-[90vh]"
        role="dialog" 
        aria-modal="true"
      >
        <!-- Header -->
        <div class="flex items-center justify-between px-6 py-4 border-b border-border/50">
          <h2 class="text-lg font-semibold text-foreground">{{ title }}</h2>
          <button 
            (click)="close()"
            class="p-2 text-muted-foreground hover:bg-muted hover:text-foreground rounded-md transition-colors"
          >
            <ng-icon name="lucideX" class="w-5 h-5"></ng-icon>
          </button>
        </div>

        <!-- Body -->
        <div class="overflow-y-auto p-6">
          <ng-content></ng-content>
        </div>
        
        <!-- Footer -->
        <div *ngIf="showFooter" class="px-6 py-4 border-t border-border/50 bg-muted/20 rounded-b-xl flex justify-end gap-2">
           <ng-content select="[modal-footer]"></ng-content>
        </div>
      </div>
    </div>
  `
})
export class ModalComponent {
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
  onKeydownHandler(event: KeyboardEvent) {
    if (this.isOpen) {
      this.close();
    }
  }
}
