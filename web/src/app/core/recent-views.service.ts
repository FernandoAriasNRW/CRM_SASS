import { Injectable, inject, signal, effect } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { filter, debounceTime } from 'rxjs/operators';
import { Subject } from 'rxjs';
import { NavigationSignalStore } from './navigation-signal.store';

export interface RecentView {
  label: string;
  route: string;
  icon: string;
  url: string; // The exact URL they visited
  timestamp?: number; // UNIX timestamp for expiration
}

const TWO_DAYS_MS = 2 * 24 * 60 * 60 * 1000;

@Injectable({ providedIn: 'root' })
export class RecentViewsService {
  private readonly router = inject(Router);
  private readonly navStore = inject(NavigationSignalStore);
  
  readonly views = signal<RecentView[]>([]);
  private readonly saveSubject = new Subject<RecentView[]>();
  private initialized = false;

  constructor() {
    effect(() => {
      const prefs = this.navStore.rawPreferences;
      if (prefs?.recentViews && !this.initialized) {
        const now = Date.now();
        // Filtrar aquellos que tengan más de 2 días
        const validViews = prefs.recentViews.filter(v => {
          if (!v.timestamp) return true; // Retrocompatibilidad corta
          return (now - v.timestamp) <= TWO_DAYS_MS;
        });
        this.views.set(validViews);
        this.initialized = true;
      }
    });

    this.saveSubject.pipe(
      debounceTime(3000) // Debounce saving to backend by 3 seconds
    ).subscribe(views => {
      const prefs = this.navStore.rawPreferences || { pinnedIds: [], unpinnedIds: [] };
      const newPrefs = { ...prefs, recentViews: views };
      this.navStore.updatePreferences(newPrefs);
    });

    this.startTracking();
  }

  private saveToStorage(v: RecentView[]) {
    this.saveSubject.next(v);
  }

  private startTracking() {
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe((e: any) => {
      const url = e.urlAfterRedirects;
      
      // Ignore home and auth routes
      if (url === '/' || url.startsWith('/auth')) return;

      const path = url.split('?')[0].split('/')[1]; // extract base path handling query params
      const item = this.navStore.allItems().find(i => i.route === `/${path}`);
      
      if (item) {
        const newView: RecentView = {
          label: item.label,
          route: item.route,
          icon: item.icon,
          url: url,
          timestamp: Date.now()
        };
        
        this.addRecentView(newView);
      }
    });
  }

  private addRecentView(view: RecentView) {
    this.views.update(current => {
      const now = Date.now();
      // Filtrar duplicados (misma ruta/módulo) y expirados
      const filtered = current.filter(v => 
        v.route !== view.route && 
        (!v.timestamp || (now - v.timestamp) <= TWO_DAYS_MS)
      );
      const updated = [view, ...filtered].slice(0, 10); // Keep last 10 modules
      this.saveToStorage(updated);
      return updated;
    });
  }
}
