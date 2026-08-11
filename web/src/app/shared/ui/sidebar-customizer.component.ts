import { Component, EventEmitter, inject, Output } from '@angular/core';
import { CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { NavigationSignalStore, NavItem } from '../../core/navigation-signal.store';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideX, lucideGripVertical, lucidePlus, lucideMinus } from '@ng-icons/lucide';

@Component({
  selector: 'app-sidebar-customizer',
  standalone: true,
  imports: [DragDropModule, NgIconComponent],
  viewProviders: [provideIcons({ lucideX, lucideGripVertical, lucidePlus, lucideMinus })],
  templateUrl: './sidebar-customizer.component.html'
})
export class SidebarCustomizerComponent {
  readonly navStore = inject(NavigationSignalStore);
  
  @Output() close = new EventEmitter<void>();

  pinnedItems: NavItem[] = [];
  unpinnedItems: NavItem[] = [];

  constructor() {
    this.pinnedItems = [...this.navStore.pinnedItems()];
    this.unpinnedItems = [...this.navStore.unpinnedItems()];
  }

  drop(event: CdkDragDrop<NavItem[]>) {
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(
        event.previousContainer.data,
        event.container.data,
        event.previousIndex,
        event.currentIndex,
      );
    }
  }

  moveToPinned(item: NavItem, index: number) {
    this.unpinnedItems.splice(index, 1);
    this.pinnedItems.push(item);
  }

  moveToUnpinned(item: NavItem, index: number) {
    this.pinnedItems.splice(index, 1);
    this.unpinnedItems.push(item);
  }

  save() {
    const prefs = {
      pinnedIds: this.pinnedItems.map(i => i.id),
      unpinnedIds: this.unpinnedItems.map(i => i.id)
    };
    this.navStore.updatePreferences(prefs).then(() => {
      this.close.emit();
    });
  }
}
