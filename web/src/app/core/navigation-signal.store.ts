import { Injectable, signal, computed, inject } from '@angular/core';
import { ApiService } from './api.service';

export interface NavItem {
  id: string; // unique identifier
  label: string;
  icon: string;
  route: string;
  hasSubmenu?: boolean;
  isHome?: boolean;
  hasCustomizer?: boolean;
}

export interface SubmenuItem {
  id: string;
  label: string;
  route?: string;
  queryParams?: Record<string, string>;
  isDivider?: boolean;
  isAction?: boolean;
  isCustom?: boolean;
}

export interface SidebarPreferences {
  pinnedIds: string[];
  unpinnedIds: string[];
  submenuItems?: Record<string, SubmenuItem[]>;
  recentViews?: any[]; // We use any[] here to avoid circular dependency, typed in the service
}

const DEFAULT_NAV_ITEMS: NavItem[] = [
  { id: 'home', label: 'Home', icon: 'lucideHome', route: '/', hasSubmenu: true, isHome: true, hasCustomizer: true },
  { id: 'dashboard', label: 'Dashboard', icon: 'lucideLayoutDashboard', route: '/dashboard', hasSubmenu: true, hasCustomizer: true },
  { id: 'docs', label: 'Docs', icon: 'lucideFileText', route: '/docs', hasSubmenu: true, hasCustomizer: true },
  { id: 'projects', label: 'Proyectos', icon: 'lucideFolderKanban', route: '/projects', hasSubmenu: true, hasCustomizer: true },
  { id: 'tasks', label: 'Tareas', icon: 'lucideCheckSquare', route: '/tasks', hasSubmenu: true, hasCustomizer: true },
  { id: 'tickets', label: 'Tickets', icon: 'lucideTicket', route: '/tickets', hasSubmenu: true, hasCustomizer: true },
  { id: 'teams', label: 'Teams', icon: 'lucideUsers', route: '/teams', hasSubmenu: true, hasCustomizer: true },
  { id: 'chat', label: 'Chat', icon: 'lucideMessageSquare', route: '/chat' },
  { id: 'calendar', label: 'Calendario', icon: 'lucideCalendar', route: '/calendar' },
  { id: 'reports', label: 'Reportes', icon: 'lucideChartBar', route: '/reports' },
  { id: 'webhooks', label: 'Webhooks', icon: 'lucideWebhook', route: '/webhooks' }
];

const DEFAULT_SUBMENUS: Record<string, SubmenuItem[]> = {
  home: [
    { id: 'h_mine_proj', label: 'Mis Proyectos', route: '/projects', queryParams: { filter: 'mine' } },
    { id: 'h_mine_task', label: 'Mis Tareas', route: '/tasks', queryParams: { filter: 'mine' } },
    { id: 'h_mine_tick', label: 'Mis Tickets', route: '/tickets', queryParams: { filter: 'mine' } },
    { id: 'h_div_1', label: 'Spaces', isDivider: true },
    { id: 'h_spaces_all', label: 'Listar todos' },
    { id: 'h_spaces_add', label: 'Añadir o Crear Space', isAction: true }
  ],
  dashboard: [
    { id: 'd_all', label: 'Listar todos', route: '/dashboard', queryParams: { type: 'all' } },
    { id: 'd_mine', label: 'Mis Dashboards', route: '/dashboard', queryParams: { type: 'private' } },
    { id: 'd_team', label: 'Dashboards del team', route: '/dashboard', queryParams: { type: 'public' } },
    { id: 'd_div_1', label: '', isDivider: true },
    { id: 'd_add', label: 'Crear Dashboard', route: '/dashboard/new', isAction: true }
  ]
};

function getDefaultSubmenu(item: NavItem): SubmenuItem[] {
  if (DEFAULT_SUBMENUS[item.id]) {
    return DEFAULT_SUBMENUS[item.id];
  }
  return [
    { id: `${item.id}_all`, label: 'Listar todos', route: item.route },
    { id: `${item.id}_mine`, label: `Mis ${item.label.toLowerCase()}`, route: item.route, queryParams: { filter: 'mine' } },
    { id: `${item.id}_team`, label: `${item.label} del Team`, route: item.route, queryParams: { filter: 'team' } },
    { id: `${item.id}_div`, label: '', isDivider: true },
    { id: `${item.id}_add`, label: 'Crear / Agregar', route: `${item.route}/new`, isAction: true }
  ];
}

@Injectable({ providedIn: 'root' })
export class NavigationSignalStore {
  private readonly api = inject(ApiService);
  
  private readonly _allItems = signal<NavItem[]>(DEFAULT_NAV_ITEMS);
  private readonly _preferences = signal<SidebarPreferences | null>(null);

  readonly allItems = this._allItems.asReadonly();
  
  // Computed values based on preferences or defaults
  readonly pinnedItems = computed(() => {
    const prefs = this._preferences();
    const items = this._allItems();
    if (!prefs || prefs.pinnedIds.length === 0) {
      // Default: top 6 items
      return items.slice(0, 6);
    }
    return prefs.pinnedIds.map(id => items.find(i => i.id === id)!).filter(Boolean);
  });

  readonly unpinnedItems = computed(() => {
    const prefs = this._preferences();
    const items = this._allItems();
    if (!prefs || prefs.pinnedIds.length === 0) {
      // Default: everything after 6
      return items.slice(6);
    }
    return prefs.unpinnedIds.map(id => items.find(i => i.id === id)!).filter(Boolean);
  });

  getSubmenuItems(menuId: string): SubmenuItem[] {
    const prefs = this._preferences();
    const item = this._allItems().find(i => i.id === menuId);
    if (!item) return [];

    // Si el usuario tiene guardado un arreglo personalizado para este submenú, lo usamos.
    if (prefs?.submenuItems && prefs.submenuItems[menuId]) {
      return prefs.submenuItems[menuId];
    }

    return getDefaultSubmenu(item);
  }

  get rawPreferences(): SidebarPreferences | null {
    return this._preferences();
  }

  async loadPreferences(): Promise<void> {
    try {
      const response = await this.api.get<{ sidebarPreferences?: string }>('/users/me/preferences').toPromise();
      if (response && response.sidebarPreferences) {
        const prefs = JSON.parse(response.sidebarPreferences) as SidebarPreferences;
        this._preferences.set(prefs);
      }
    } catch (e) {
      console.warn('Could not load sidebar preferences', e);
    }
  }

  async updatePreferences(newPrefs: SidebarPreferences): Promise<void> {
    this._preferences.set(newPrefs);
    try {
      await this.api.put('/users/me/preferences', { preferencesJson: JSON.stringify(newPrefs) }).toPromise();
    } catch (e) {
      console.error('Failed to save sidebar preferences', e);
    }
  }
}
