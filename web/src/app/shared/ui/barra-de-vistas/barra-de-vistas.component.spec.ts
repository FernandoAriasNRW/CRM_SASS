import { ComponentFixture, TestBed } from '@angular/core/testing';

import { BarraDeVistasComponent, type VistaIntegrada } from './barra-de-vistas.component';
import type { SavedView } from '../../services/views.service';

/**
 * Estas pruebas fijan lo que estaba roto: <b>las pestañas de fábrica desaparecían</b> en cuanto
 * había una vista guardada, porque vivían dentro de un <code>&#64;if (savedViews().length === 0)</code>.
 * En tickets eso dejaba sin manera de ver la lista; en tareas se llevaba además el Gantt y la
 * carga. Y como nadie llamaba a borrar vistas, no había vuelta atrás.
 */
describe('BarraDeVistasComponent', () => {
  let fixture: ComponentFixture<BarraDeVistasComponent>;
  let componente: BarraDeVistasComponent;

  const INTEGRADAS: VistaIntegrada[] = [
    { clave: 'board', etiqueta: 'Tablero', icono: 'lucideLayoutDashboard' },
    { clave: 'list', etiqueta: 'Lista', icono: 'lucideList' },
    { clave: 'gantt', etiqueta: 'Gantt', icono: 'lucideChartGantt' }
  ];

  const vista = (id: string, nombre: string, tipo = 'list'): SavedView => ({
    id,
    userId: 'u1',
    tenantId: 't1',
    moduleName: 'Tickets',
    viewName: nombre,
    stateJson: JSON.stringify({ viewType: tipo }),
    isDefault: false
  });

  /** Los textos de las pestañas, que es lo que ve quien usa la pantalla. */
  const pestanas = () =>
    Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLElement>)
      .map(b => b.textContent?.trim() ?? '')
      .filter(t => t.length > 0);

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [BarraDeVistasComponent] }).compileComponents();

    fixture = TestBed.createComponent(BarraDeVistasComponent);
    componente = fixture.componentInstance;
    fixture.componentRef.setInput('integradas', INTEGRADAS);
    fixture.componentRef.setInput('modo', 'board');
    fixture.componentRef.setInput('guardadas', []);
    fixture.detectChanges();
  });

  it('enseña las vistas de fábrica cuando no hay ninguna guardada', () => {
    expect(pestanas()).toEqual(['Tablero', 'Lista', 'Gantt', 'Vista']);
  });

  /**
   * El fallo, con su nombre. Sin esta comprobación volvería a colarse: la versión rota también
   * «enseñaba las pestañas» —mientras no hubiera vistas guardadas—.
   */
  it('sigue enseñándolas cuando hay vistas guardadas', () => {
    fixture.componentRef.setInput('guardadas', [vista('v1', 'Urgentes')]);
    fixture.detectChanges();

    expect(pestanas()).toContain('Tablero');
    expect(pestanas()).toContain('Lista');
    expect(pestanas()).toContain('Gantt');
    expect(pestanas()).toContain('Urgentes');
  });

  it('marca la de fábrica activa sólo si no hay una guardada puesta', () => {
    fixture.componentRef.setInput('guardadas', [vista('v1', 'Urgentes')]);
    fixture.componentRef.setInput('vistaActivaId', 'v1');
    fixture.detectChanges();

    const tablero = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLElement>
    ).find(b => b.textContent?.trim() === 'Tablero')!;

    expect(tablero.className).withContext(
      'con una vista guardada puesta, la pestaña de fábrica no puede seguir marcada: serían dos ' +
      'pestañas activas a la vez diciendo cosas distintas'
    ).toContain('border-transparent');
  });

  it('el icono de una guardada sale de su estado, no de su nombre', () => {
    expect(componente.iconoDe(vista('v1', 'Lo que sea', 'gantt'))).toBe('lucideChartGantt');
    expect(componente.iconoDe(vista('v2', 'Tablero de Ana', 'list'))).toBe('lucideList');
  });

  /** Un estado ilegible no puede tumbar la barra: si no, no se podría ni borrar la vista mala. */
  it('aguanta una vista con el estado corrupto', () => {
    const rota = { ...vista('v3', 'Rota'), stateJson: 'esto no es json' };
    expect(() => componente.iconoDe(rota)).not.toThrow();
    expect(componente.iconoDe(rota)).toBe('lucideList');
  });

  it('propone guardar la forma que se está viendo', () => {
    fixture.componentRef.setInput('modo', 'gantt');
    fixture.detectChanges();

    componente.empezarCreacion();
    expect(componente.tipoNuevo).withContext(
      'quien pulsa «Vista» estando en el Gantt casi siempre quiere guardar ese Gantt'
    ).toBe('gantt');
  });

  it('no crea una vista sin nombre', () => {
    const creadas: unknown[] = [];
    componente.crear.subscribe(v => creadas.push(v));

    componente.empezarCreacion();
    componente.nombreNuevo = '   ';
    componente.confirmarCreacion();

    expect(creadas).toEqual([]);
    expect(componente.creando()).withContext('el campo sigue abierto para poder escribir').toBeTrue();
  });

  it('crea la vista con el nombre recortado', () => {
    const creadas: { nombre: string; tipo: string }[] = [];
    componente.crear.subscribe(v => creadas.push(v));

    componente.empezarCreacion();
    componente.nombreNuevo = '  Urgentes de hoy  ';
    componente.tipoNuevo = 'list';
    componente.confirmarCreacion();

    expect(creadas).toEqual([{ nombre: 'Urgentes de hoy', tipo: 'list' }]);
    expect(componente.creando()).toBeFalse();
  });

  it('pide confirmación antes de borrar, y no borra si se dice que no', () => {
    const borradas: SavedView[] = [];
    componente.borrar.subscribe(v => borradas.push(v));

    spyOn(window, 'confirm').and.returnValue(false);
    componente.pedirBorrado(vista('v1', 'Urgentes'));
    expect(borradas).toEqual([]);

    (window.confirm as jasmine.Spy).and.returnValue(true);
    componente.pedirBorrado(vista('v1', 'Urgentes'));
    expect(borradas.length).toBe(1);
  });
});
