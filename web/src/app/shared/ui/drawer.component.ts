import { Component, Input, Output, EventEmitter, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideX, lucideMaximize2, lucideMinimize2 } from '@ng-icons/lucide';

@Component({
  selector: 'app-drawer',
  standalone: true,
  imports: [CommonModule, NgIconComponent],
  viewProviders: [provideIcons({ lucideX, lucideMaximize2, lucideMinimize2 })],
  template: `
    @if (isOpen) {
      <div class="fixed inset-0 z-50 overflow-hidden flex justify-end">
        <!-- Backdrop Overlay -->
        <div 
          class="fixed inset-0 bg-black/60 backdrop-blur-xs transition-opacity duration-300 animate-in fade-in"
          (click)="close()"
        ></div>

        <!-- Right Side Panel -->
        <div 
          class="relative flex flex-col h-full bg-card text-card-foreground border-l border-border shadow-2xl z-10 transition-all duration-300 transform animate-in slide-in-from-right"
          [ngClass]="sizeClasses"
        >
          <!-- Drawer Header -->
          <div class="flex items-center justify-between px-6 py-4 border-b border-border bg-muted/20 shrink-0">
            <div class="flex items-center gap-3 min-w-0 pr-4">
              <ng-content select="[drawer-icon]"></ng-content>
              <div class="min-w-0">
                <div class="flex items-center gap-2">
                  <h2 class="text-base font-bold tracking-tight text-foreground truncate">{{ title }}</h2>
                  <ng-content select="[drawer-badge]"></ng-content>
                </div>
                @if (subtitle) {
                  <p class="text-xs text-muted-foreground truncate mt-0.5">{{ subtitle }}</p>
                }
              </div>
            </div>

            <!-- Action Controls -->
            <div class="flex items-center gap-2 shrink-0">
              <ng-content select="[drawer-actions]"></ng-content>
              <button 
                (click)="toggleExpand()" 
                class="p-1.5 rounded-lg text-muted-foreground hover:text-foreground hover:bg-accent transition-colors"
                [title]="isExpanded ? 'Restaurar tamaño' : 'Maximizar'"
              >
                <ng-icon [name]="isExpanded ? 'lucideMinimize2' : 'lucideMaximize2'" size="16" />
              </button>
              <button 
                (click)="close()" 
                class="p-1.5 rounded-lg text-muted-foreground hover:text-foreground hover:bg-accent transition-colors"
                title="Cerrar (Esc)"
              >
                <ng-icon name="lucideX" size="18" />
              </button>
            </div>
          </div>

          <!-- Drawer Content Body -->
          <div class="flex-1 overflow-y-auto p-6 space-y-6">
            <ng-content></ng-content>
          </div>

          <!-- Drawer Footer -->
          @if (showFooter) {
            <div class="px-6 py-4 border-t border-border bg-muted/20 flex items-center justify-end gap-3 shrink-0">
              <ng-content select="[drawer-footer]"></ng-content>
            </div>
          }
        </div>
      </div>
    }
  `,
  styles: [`
    :host {
      display: contents;
    }
  `]
})
export class DrawerComponent {
  @Input() isOpen = false;
  @Input() title = '';
  @Input() subtitle = '';
  @Input() size: 'sm' | 'md' | 'lg' | 'xl' | '2xl' = 'lg';
  @Input() showFooter = true;
  @Output() closed = new EventEmitter<void>();
  @Output() isOpenChange = new EventEmitter<boolean>();

  isExpanded = false;

  get sizeClasses(): string {
    if (this.isExpanded) return 'w-full max-w-full';
    switch (this.size) {
      case 'sm': return 'w-full max-w-md';
      case 'md': return 'w-full max-w-xl';
      case 'lg': return 'w-full max-w-2xl';
      case 'xl': return 'w-full max-w-4xl';
      case '2xl': return 'w-full max-w-6xl';
      default: return 'w-full max-w-2xl';
    }
  }

  toggleExpand(): void {
    this.isExpanded = !this.isExpanded;
  }

  close(): void {
    this.closed.emit();
    this.isOpenChange.emit(false);
  }

  @HostListener('document:keydown.escape', ['$event'])
  handleEscape(event: Event): void {
    if (this.isOpen) {
      this.close();
    }
  }
}
