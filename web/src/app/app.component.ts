import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter, map } from 'rxjs/operators';
import { AuthSignalStore } from './core/auth-signal.store';
import { RealtimeService } from './core/realtime.service';
import { NotificationsComponent } from './features/notifications/notifications.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { ApiService } from './core/api.service';
import { SessionManagerService } from './core/session-manager.service';
import { ToastContainerComponent } from './shared/ui/toast-container.component';
import { ToastService } from './shared/services/toast.service';
import { UserAvatarComponent } from './shared/ui/user-avatar.component';
import { SidebarCustomizerComponent } from './shared/ui/sidebar-customizer.component';
import { SubmenuCustomizerComponent } from './shared/ui/submenu-customizer.component';
import { PanelDeNavegacionComponent } from './shared/ui/panel-de-navegacion/panel-de-navegacion.component';
import { VOCABULARIO } from './shared/ui/panel-de-navegacion/vocabulario-del-menu';
import {
  lucideLayoutDashboard, lucideFolderKanban, lucideCheckSquare,
  lucideTicket, lucideLogOut, lucideMenu, lucideX,
  lucideMessageSquare, lucideCalendar, lucideBarChart2, lucideUser, lucideSettings,
  lucideWebhook, lucideChevronDown, lucideChevronRight, lucideFileText,
  lucideUsers, lucideHome, lucideMoreHorizontal, lucideChartBar, lucidePlus
} from '@ng-icons/lucide';
import { HierarchySignalStore } from './core/hierarchy-signal.store';
import { NavigationSignalStore } from './core/navigation-signal.store';
import { UpperCasePipe } from '@angular/common';
import { ClickableDirective } from './shared/directives/clickable.directive';
import { CommandPaletteComponent } from './shared/ui/command-palette/command-palette.component';
import { CommandPaletteService } from './shared/ui/command-palette/command-palette.service';

/**
 * Los módulos que tienen panel de navegación propio.
 *
 * Sale de `VOCABULARIO`, que es donde están las entradas: si estuviera escrita a mano aquí, añadir
 * un módulo al vocabulario no le daría panel y nadie sabría por qué.
 */
const MODULOS_CON_PANEL = Object.keys(VOCABULARIO);

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [ClickableDirective, 
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    NgIconComponent,
    NotificationsComponent,
    ToastContainerComponent,
    UserAvatarComponent,
    SidebarCustomizerComponent,
    SubmenuCustomizerComponent,
    UpperCasePipe,
    CommandPaletteComponent, PanelDeNavegacionComponent
  ],
  viewProviders: [provideIcons({
    lucideLayoutDashboard, lucideFolderKanban, lucideCheckSquare,
    lucideTicket, lucideLogOut, lucideMenu, lucideX,
    lucideMessageSquare, lucideCalendar, lucideBarChart2, lucideUser, lucideSettings, lucideWebhook,
    lucideChevronDown, lucideChevronRight, lucideFileText, lucideUsers, lucideHome, lucideMoreHorizontal, lucideChartBar,
    // `lucidePlus` lo usa el «+» de añadir al submenú. Faltaba, y `ng-icon` no falla cuando no
    // encuentra un icono: sólo escribe un aviso en la consola y deja el hueco. Era el ruido que
    // salía siete veces en cada carga.
    lucidePlus
  })],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit {
  protected readonly paleta = inject(CommandPaletteService);

  /**
   * Atajo global del paletón. Se escucha en el documento y no en un elemento concreto
   * para que funcione desde cualquier vista y con el foco donde sea.
   *
   * Se atiende Ctrl+K además de Cmd+K: en Windows y Linux no hay tecla Cmd, y limitarlo
   * a macOS dejaría el atajo inservible para la mayoría. El preventDefault evita que el
   * navegador se quede con la pulsación, que en Chrome enfoca la barra de direcciones.
   */
  @HostListener('document:keydown', ['$event'])
  protected alPulsarGlobal(evento: KeyboardEvent): void {
    if ((evento.metaKey || evento.ctrlKey) && evento.key.toLowerCase() === 'k') {
      evento.preventDefault();
      this.paleta.alternar();
    }
  }

  readonly authStore = inject(AuthSignalStore);
  private readonly router = inject(Router);
  private readonly realtime = inject(RealtimeService);
  private readonly api = inject(ApiService);
  private readonly sessionManager = inject(SessionManagerService);
  private readonly toast = inject(ToastService);
  readonly hierarchyStore = inject(HierarchySignalStore);
  readonly navStore = inject(NavigationSignalStore);

  /**
   * La URL actual, cruda.
   *
   * Hace falta aparte de `currentRouteName`, que devuelve la etiqueta traducida —«Tareas»— y no
   * sirve para saber de qué módulo se trata.
   */
  readonly currentRouteUrl = toSignal(
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map((e: any) => (e.urlAfterRedirects || e.url || '') as string)
    ),
    { initialValue: this.router.url }
  );

  readonly currentRouteName = toSignal(
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map((e: any) => {
        const fullUrl = e.urlAfterRedirects || e.url || '';
        const cleanPath = fullUrl.split('?')[0];
        const seg = cleanPath.split('/')[1] || '';
        if (seg.toLowerCase() === 'admin') return 'Admin';
        const item = this.navStore.allItems().find(i => i.route === `/${seg}`);
        if (item) return item.label;
        if (!seg || seg.toLowerCase() === 'home') return 'Home';
        return seg.charAt(0).toUpperCase() + seg.slice(1);
      })
    ),
    { initialValue: 'Dashboard' }
  );

  // ── El panel de navegación del módulo ──────────────────────────────────────

  /**
   * Qué módulo se está señalando en la barra lateral, o nulo si ninguno.
   *
   * Se separa de la ruta para que asomar el panel de Tickets estando en Tareas enseñe el de
   * Tickets. Al salir del ratón se vuelve a la ruta actual, que es lo que se espera ver.
   */
  private readonly moduloSenalado = signal<string | null>(null);

  /** Si el ratón está encima del panel. Sin esto, al ir hacia él se cerraría por el camino. */
  protected readonly raton = signal(false);

  /** El panel se queda abierto y empuja el contenido. */
  protected readonly panelAnclado = signal(true);

  /** El módulo de la ruta actual, si tiene panel. */
  protected readonly moduloDeLaRuta = computed(() => {
    const segmento = (this.currentRouteUrl() ?? '').split('?')[0].split('/')[1] ?? '';
    return MODULOS_CON_PANEL.includes(segmento) ? segmento : null;
  });

  /**
   * El módulo cuyo panel se pinta: el señalado con el ratón, y si no, el de la pantalla.
   *
   * Se prefiere el señalado porque es una intención explícita —alguien acaba de llevar el ratón
   * ahí— mientras que la ruta es sólo dónde se está.
   */
  protected readonly moduloDelPanel = computed(
    () => this.moduloSenalado() ?? this.moduloDeLaRuta());

  /**
   * Si el panel se ve ahora mismo.
   *
   * Anclado, siempre. Sin anclar, sólo mientras el ratón esté en la barra o en el propio panel:
   * si sólo se mirara la barra, el panel se cerraría en cuanto se moviera el ratón hacia él.
   */
  protected readonly panelVisible = computed(
    () => this.panelAnclado() || this.moduloSenalado() !== null || this.raton());

  protected tienePanel(ruta: string): boolean {
    return MODULOS_CON_PANEL.includes(ruta.replace(/^\//, ''));
  }

  protected alEntrarEnModulo(ruta: string): void {
    const modulo = ruta.replace(/^\//, '');
    // Los módulos sin panel no lo abren, pero **sí lo cierran**: pasar el ratón por «Docs» tiene
    // que recoger el de Tareas, o se quedaría abierto enseñando otra cosa.
    this.moduloSenalado.set(MODULOS_CON_PANEL.includes(modulo) ? modulo : null);
  }

  // Drawer state for sidebar customizer
  isCustomizerOpen = false;
  activeSubmenuCustomizer: string | null = null;

  openCustomizer(): void {
    this.isCustomizerOpen = true;
  }

  closeCustomizer(): void {
    this.isCustomizerOpen = false;
  }

  openSubmenuCustomizer(menuId: string): void {
    this.activeSubmenuCustomizer = menuId;
  }

  closeSubmenuCustomizer(): void {
    this.activeSubmenuCustomizer = null;
  }

  ngOnInit(): void {
    if (this.authStore.isAuthenticated()) {
      this.realtime.connect();
      this.realtime.connectChat();
      this.hierarchyStore.loadHierarchy();
      this.navStore.loadPreferences();

      // Fetch user info if not loaded
      if (!this.authStore.userInfo()) {
        this.api.get<{ id: string; name: string; email: string; tenantId: string; role: string }>('/auth/users/me')
          .subscribe({
            next: (user) => this.authStore.setUserInfo(user),
            error: () => {}
          });
      }
    }
  }

  logout(): void {
    // Call backend logout - it will clear the refresh token cookie
    // No need to pass refresh token in body - it's sent via cookie automatically
    this.api.post('/auth/logout', {}).subscribe({
      next: () => {
        this.realtime.disconnect();
        this.authStore.logout();
      },
      error: () => {
        this.realtime.disconnect();
        this.authStore.logout();
      }
    });
  }

  navigateToProfile(): void {
    this.router.navigate(['/profile']);
  }

  navigateToAdminUsers(): void {
    this.router.navigate(['/admin/users']);
  }
}
