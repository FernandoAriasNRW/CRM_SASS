import { Component, EventEmitter, inject, Input, OnInit, Output } from '@angular/core';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { NavigationSignalStore, SubmenuItem } from '../../core/navigation-signal.store';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideX, lucideGripVertical, lucidePlus, lucideTrash2 } from '@ng-icons/lucide';
import { FormsModule } from '@angular/forms';
import { ButtonComponent } from './button.component';

@Component({
  selector: 'app-submenu-customizer',
  standalone: true,
  imports: [DragDropModule, NgIconComponent, FormsModule, ButtonComponent],
  viewProviders: [provideIcons({ lucideX, lucideGripVertical, lucidePlus, lucideTrash2 })],
  templateUrl: './submenu-customizer.component.html'
})
export class SubmenuCustomizerComponent implements OnInit {
  readonly navStore = inject(NavigationSignalStore);
  
  @Input({ required: true }) menuId!: string;
  @Output() close = new EventEmitter<void>();

  items: SubmenuItem[] = [];
  
  newLabel = '';
  newRoute = '';
  newQueryParams?: Record<string, string>;

  selectedTemplate: any = null;

  commonLinks = [
    { label: 'Calendario', route: '/calendar' },
    { label: 'Chat', route: '/chat' },
    { label: 'Reportes', route: '/reports' },
    { label: 'Tareas Completadas', route: '/tasks', queryParams: { filter: 'done' } },
    { label: 'Tickets Cerrados', route: '/tickets', queryParams: { filter: 'closed' } },
    { label: 'Mi Perfil', route: '/profile' }
  ];

  applyTemplate() {
    if (this.selectedTemplate) {
      this.newLabel = this.selectedTemplate.label;
      this.newRoute = this.selectedTemplate.route;
      this.newQueryParams = this.selectedTemplate.queryParams;
    } else {
      this.newLabel = '';
      this.newRoute = '';
      this.newQueryParams = undefined;
    }
  }

  ngOnInit() {
    this.items = [...this.navStore.getSubmenuItems(this.menuId)];
  }

  drop(event: CdkDragDrop<SubmenuItem[]>) {
    moveItemInArray(this.items, event.previousIndex, event.currentIndex);
  }

  removeItem(index: number) {
    this.items.splice(index, 1);
  }

  addCustomLink() {
    if (!this.newLabel || !this.newRoute) return;
    
    this.items.push({
      id: `custom_${Date.now()}`,
      label: this.newLabel,
      route: this.newRoute,
      queryParams: this.newQueryParams,
      isCustom: true
    });
    
    this.newLabel = '';
    this.newRoute = '';
    this.newQueryParams = undefined;
    this.selectedTemplate = null;
  }

  save() {
    const rawPrefs = this.navStore.rawPreferences;
    const prefs = rawPrefs ? { ...rawPrefs } : { pinnedIds: [], unpinnedIds: [] };
    
    if (!prefs.submenuItems) {
      prefs.submenuItems = {};
    }
    
    prefs.submenuItems[this.menuId] = this.items;
    
    this.navStore.updatePreferences(prefs).then(() => {
      this.close.emit();
    });
  }
}
