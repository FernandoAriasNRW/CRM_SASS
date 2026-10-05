import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';

import { NavigationPanelComponent } from './navigation-panel.component';
import { SHARED_ENTRIES, FILTERS, MENU_VOCABULARY } from './menu-vocabulary';

describe('NavigationPanelComponent', () => {
  let fixture: ComponentFixture<NavigationPanelComponent>;
  let component: NavigationPanelComponent;
  let params: BehaviorSubject<Record<string, string>>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    params = new BehaviorSubject<Record<string, string>>({});
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [NavigationPanelComponent],
      providers: [
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { queryParams: params.asObservable() } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NavigationPanelComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('moduleKey', 'tickets');
    // El módulo de la pantalla se le dice desde fuera. Antes el componente lo sacaba de
    // `router.url`, que no reacciona a los cambios de ruta y además obligaba a que el doble del
    // router fingiera una URL para algo que no es asunto suyo.
    fixture.componentRef.setInput('currentModule', 'tickets');
    fixture.detectChanges();
  });

  it('renders the vocabulary of the requested module', () => {
    expect(component.vocabulary().title).toBe(MENU_VOCABULARY['tickets'].title);
    expect(component.vocabulary().entries.length).toBe(SHARED_ENTRIES.length);
  });

  /**
   * «Ver todo» tiene que quitar el parámetro, no mandarlo vacío.
   *
   * Con `filter=` en la URL, el servidor recibiría una cadena vacía y la lista completa se vería
   * igual, pero la URL parecería filtrada: al copiarla y compartirla, quien la abriera no sabría
   * si está viendo todo o el resultado de un filtro que no reconoce.
   */
  it('«see all» removes the URL parameter', () => {
    const seeAll = component.vocabulary().entries.find(e => e.filter === null)!;

    component.go(seeAll);

    const [, options] = router.navigate.calls.mostRecent().args as [unknown[], { queryParams: Record<string, unknown> }];
    expect(options.queryParams['filter']).toBeNull();
  });

  it('each entry navigates with its filter', () => {
    const favorites = component.vocabulary().entries.find(e => e.filter === FILTERS.favorites)!;

    component.go(favorites);

    const [, options] = router.navigate.calls.mostRecent().args as [unknown[], { queryParams: Record<string, unknown> }];
    expect(options.queryParams['filter']).toBe('favorites');
  });

  /**
   * Navega a la ruta del módulo del panel, no a la actual.
   *
   * Importa desde que el panel se asoma desde la barra lateral: estando en Tareas y asomando el de
   * Tickets, «Favoritos» tiene que llevar a los tickets favoritos. Navegando relativo llevaba a
   * las tareas favoritas, que es la pantalla equivocada con el filtro correcto.
   */
  it('navigates to the panel module even from another one', () => {
    fixture.componentRef.setInput('currentModule', 'tasks');
    fixture.detectChanges();

    component.go(component.vocabulary().entries.find(e => e.filter === FILTERS.favorites)!);

    const [route] = router.navigate.calls.mostRecent().args as [unknown[]];
    expect(route).toEqual(['/tickets']);
  });

  /**
   * Y por lo mismo, asomado sobre otro módulo no marca nada: el filtro de la URL es del módulo en
   * el que se está, y marcarlo aquí haría creer que los tickets ya están filtrados así.
   */
  it('peeking over another module marks no entry', () => {
    fixture.componentRef.setInput('currentModule', 'tasks');
    params.next({ filter: 'archived' });
    fixture.detectChanges();

    const archivedEntry = component.vocabulary().entries.find(e => e.filter === 'archived')!;
    expect(component.isActive(archivedEntry)).toBeFalse();
  });

  /**
   * La entrada activa sale de la URL y no de un click guardado. Así, entrar por un enlace ya
   * filtrado marca la entrada correcta, y el botón de atrás también.
   */
  it('marks as active the entry the URL says', () => {
    params.next({ filter: 'archived' });
    fixture.detectChanges();

    const archivedEntry = component.vocabulary().entries.find(e => e.filter === 'archived')!;
    const mine = component.vocabulary().entries.find(e => e.filter === 'mine')!;

    expect(component.isActive(archivedEntry)).toBeTrue();
    expect(component.isActive(mine)).toBeFalse();
  });

  it('without a filter in the URL, «see all» is active', () => {
    const seeAll = component.vocabulary().entries.find(e => e.filter === null)!;

    expect(component.isActive(seeAll)).toBeTrue();
  });

  it('pinning can be toggled', () => {
    expect(component.pinned()).toBeTrue();

    component.togglePinned(new MouseEvent('click'));

    expect(component.pinned()).toBeFalse();
  });

  /**
   * Un módulo nuevo del menú recibe panel sin que nadie lo dé de alta aquí.
   *
   * Es lo que hace que añadir un módulo a la barra lateral no lo deje sin submenú mientras el
   * resto sí lo tiene. Se le ofrece **sólo «Ver todo»**: es lo único que se puede afirmar sin
   * conocerlo. Inventarle «Mis facturas» o «Facturas del equipo» sería repetir el fallo que se
   * quitó —ofrecer filtros que el servidor no aplica—.
   */
  it('a new module gets a panel with the only entry that is true', () => {
    fixture.componentRef.setInput('moduleKey', 'facturas');
    fixture.componentRef.setInput('currentModule', 'facturas');
    fixture.componentRef.setInput('moduleName', 'Facturas');
    fixture.detectChanges();

    expect(component.vocabulary().title).toBe('Facturas');
    expect(component.vocabulary().entries.map(e => e.filter)).toEqual([null]);
  });

  /**
   * Y el que sí tiene vocabulario propio lo usa. Es la otra mitad: si el respaldo se aplicara
   * siempre, todos los módulos tendrían una sola entrada y nadie lo notaría hasta usarlos.
   */
  it('a known module uses its own vocabulary', () => {
    fixture.componentRef.setInput('moduleKey', 'docs');
    fixture.detectChanges();

    expect(component.vocabulary().entries.length).toBeGreaterThan(1);
  });
});

describe('menu vocabulary', () => {
  /**
   * La regla que sostiene toda la fase 5A: aquí sólo hay entradas que el servidor sabe filtrar.
   *
   * Esta prueba no puede comprobar el backend —eso lo hacen las de integración—, pero sí que
   * nadie añada una entrada con un filtro inventado sobre la marcha.
   */
  it('every entry uses a known filter, or none', () => {
    const known = Object.values(FILTERS) as string[];

    for (const entry of SHARED_ENTRIES) {
      if (entry.filter === null) continue;
      expect(known).toContain(entry.filter);
    }
  });

  it('the three list modules share the cross-cutting vocabulary', () => {
    for (const moduleKey of ['tasks', 'tickets', 'projects']) {
      expect(MENU_VOCABULARY[moduleKey].entries).toBe(SHARED_ENTRIES);
    }
  });
});
