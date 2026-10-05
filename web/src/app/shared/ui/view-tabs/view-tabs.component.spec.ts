import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ViewTabsComponent, type BuiltInView } from './view-tabs.component';
import type { SavedView } from '../../services/views.service';

/**
 * Estas pruebas fijan lo que estaba roto: <b>las pestañas de fábrica desaparecían</b> en cuanto
 * había una vista guardada, porque vivían dentro de un <code>&#64;if (savedViews().length === 0)</code>.
 * En tickets eso dejaba sin manera de ver la lista; en tareas se llevaba además el Gantt y la
 * carga. Y como nadie llamaba a borrar vistas, no había vuelta atrás.
 */
describe('ViewTabsComponent', () => {
  let fixture: ComponentFixture<ViewTabsComponent>;
  let component: ViewTabsComponent;

  const BUILT_IN: BuiltInView[] = [
    { key: 'board', label: 'Tablero', icon: 'lucideLayoutDashboard' },
    { key: 'list', label: 'Lista', icon: 'lucideList' },
    { key: 'gantt', label: 'Gantt', icon: 'lucideChartGantt' }
  ];

  const view = (id: string, name: string, type = 'list'): SavedView => ({
    id,
    userId: 'u1',
    tenantId: 't1',
    moduleName: 'Tickets',
    viewName: name,
    stateJson: JSON.stringify({ viewType: type }),
    isDefault: false
  });

  /** Los textos de las pestañas, que es lo que ve quien usa la pantalla. */
  const pestanas = () =>
    Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLElement>)
      .map(b => b.textContent?.trim() ?? '')
      .filter(t => t.length > 0);

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ViewTabsComponent] }).compileComponents();

    fixture = TestBed.createComponent(ViewTabsComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('builtIn', BUILT_IN);
    fixture.componentRef.setInput('mode', 'board');
    fixture.componentRef.setInput('saved', []);
    fixture.detectChanges();
  });

  it('shows the built-in views when none is saved', () => {
    expect(pestanas()).toEqual(['Tablero', 'Lista', 'Gantt', 'Vista']);
  });

  /**
   * El fallo, con su nombre. Sin esta comprobación volvería a colarse: la versión rota también
   * «enseñaba las pestañas» —mientras no hubiera vistas guardadas—.
   */
  it('keeps showing them when there are saved views', () => {
    fixture.componentRef.setInput('saved', [view('v1', 'Urgentes')]);
    fixture.detectChanges();

    expect(pestanas()).toContain('Tablero');
    expect(pestanas()).toContain('Lista');
    expect(pestanas()).toContain('Gantt');
    expect(pestanas()).toContain('Urgentes');
  });

  it('marks the built-in one active only if no saved view is applied', () => {
    fixture.componentRef.setInput('saved', [view('v1', 'Urgentes')]);
    fixture.componentRef.setInput('activeViewId', 'v1');
    fixture.detectChanges();

    const board = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLElement>
    ).find(b => b.textContent?.trim() === 'Tablero')!;

    expect(board.className).withContext(
      'con una vista guardada puesta, la pestaña de fábrica no puede seguir marcada: serían dos ' +
      'pestañas activas a la vez diciendo cosas distintas'
    ).toContain('border-transparent');
  });

  it('a saved view icon comes from its state, not its name', () => {
    expect(component.iconOf(view('v1', 'Lo que sea', 'gantt'))).toBe('lucideChartGantt');
    expect(component.iconOf(view('v2', 'Tablero de Ana', 'list'))).toBe('lucideList');
  });

  /** Un estado ilegible no puede tumbar la barra: si no, no se podría ni borrar la vista mala. */
  it('survives a view with a corrupt state', () => {
    const broken = { ...view('v3', 'Rota'), stateJson: 'esto no es json' };
    expect(() => component.iconOf(broken)).not.toThrow();
    expect(component.iconOf(broken)).toBe('lucideList');
  });

  it('suggests saving the current view type', () => {
    fixture.componentRef.setInput('mode', 'gantt');
    fixture.detectChanges();

    component.startCreate();
    expect(component.newType).withContext(
      'quien pulsa «Vista» estando en el Gantt casi siempre quiere guardar ese Gantt'
    ).toBe('gantt');
  });

  it('does not create a view without a name', () => {
    const created: unknown[] = [];
    component.create.subscribe(v => created.push(v));

    component.startCreate();
    component.newName = '   ';
    component.confirmCreate();

    expect(created).toEqual([]);
    expect(component.creating()).withContext('el campo sigue abierto para poder escribir').toBeTrue();
  });

  it('creates the view with the trimmed name', () => {
    const created: { name: string; type: string }[] = [];
    component.create.subscribe(v => created.push(v));

    component.startCreate();
    component.newName = '  Urgentes de hoy  ';
    component.newType = 'list';
    component.confirmCreate();

    expect(created).toEqual([{ name: 'Urgentes de hoy', type: 'list' }]);
    expect(component.creating()).toBeFalse();
  });

  it('asks for confirmation before deleting, and does not delete on no', () => {
    const deleted: SavedView[] = [];
    component.remove.subscribe(v => deleted.push(v));

    spyOn(window, 'confirm').and.returnValue(false);
    component.requestDelete(view('v1', 'Urgentes'));
    expect(deleted).toEqual([]);

    (window.confirm as jasmine.Spy).and.returnValue(true);
    component.requestDelete(view('v1', 'Urgentes'));
    expect(deleted.length).toBe(1);
  });
});
