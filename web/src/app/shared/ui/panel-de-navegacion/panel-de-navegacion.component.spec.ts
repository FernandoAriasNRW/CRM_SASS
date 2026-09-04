import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';

import { PanelDeNavegacionComponent } from './panel-de-navegacion.component';
import { ENTRADAS_TRANSVERSALES, FILTROS, VOCABULARIO } from './vocabulario-del-menu';

describe('PanelDeNavegacionComponent', () => {
  let fixture: ComponentFixture<PanelDeNavegacionComponent>;
  let componente: PanelDeNavegacionComponent;
  let parametros: BehaviorSubject<Record<string, string>>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    parametros = new BehaviorSubject<Record<string, string>>({});
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [PanelDeNavegacionComponent],
      providers: [
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { queryParams: parametros.asObservable() } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PanelDeNavegacionComponent);
    componente = fixture.componentInstance;
    fixture.componentRef.setInput('modulo', 'tickets');
    fixture.detectChanges();
  });

  it('pinta el vocabulario del módulo que se le pide', () => {
    expect(componente.vocabulario().titulo).toBe(VOCABULARIO['tickets'].titulo);
    expect(componente.vocabulario().entradas.length).toBe(ENTRADAS_TRANSVERSALES.length);
  });

  /**
   * «Ver todo» tiene que quitar el parámetro, no mandarlo vacío.
   *
   * Con `filter=` en la URL, el servidor recibiría una cadena vacía y la lista completa se vería
   * igual, pero la URL parecería filtrada: al copiarla y compartirla, quien la abriera no sabría
   * si está viendo todo o el resultado de un filtro que no reconoce.
   */
  it('«ver todo» quita el parámetro de la URL', () => {
    const verTodo = componente.vocabulario().entradas.find(e => e.filtro === null)!;

    componente.ir(verTodo);

    const [, opciones] = router.navigate.calls.mostRecent().args as [unknown[], { queryParams: Record<string, unknown> }];
    expect(opciones.queryParams['filter']).toBeNull();
  });

  it('cada entrada navega con su filtro', () => {
    const favoritos = componente.vocabulario().entradas.find(e => e.filtro === FILTROS.favoritos)!;

    componente.ir(favoritos);

    const [, opciones] = router.navigate.calls.mostRecent().args as [unknown[], { queryParams: Record<string, unknown> }];
    expect(opciones.queryParams['filter']).toBe('favorites');
  });

  /**
   * La entrada activa sale de la URL y no de un click guardado. Así, entrar por un enlace ya
   * filtrado marca la entrada correcta, y el botón de atrás también.
   */
  it('marca como activa la entrada que dice la URL', () => {
    parametros.next({ filter: 'archived' });
    fixture.detectChanges();

    const archivado = componente.vocabulario().entradas.find(e => e.filtro === 'archived')!;
    const mios = componente.vocabulario().entradas.find(e => e.filtro === 'mine')!;

    expect(componente.esLaActiva(archivado)).toBeTrue();
    expect(componente.esLaActiva(mios)).toBeFalse();
  });

  it('sin filtro en la URL, la activa es «ver todo»', () => {
    const verTodo = componente.vocabulario().entradas.find(e => e.filtro === null)!;

    expect(componente.esLaActiva(verTodo)).toBeTrue();
  });

  it('el anclaje se puede alternar', () => {
    expect(componente.anclado()).toBeTrue();

    componente.alternarAnclado(new MouseEvent('click'));

    expect(componente.anclado()).toBeFalse();
  });

  /**
   * Un módulo sin vocabulario no revienta la pantalla: enseña un panel vacío.
   *
   * Preferible a inventarse entradas por defecto, que sería volver a ofrecer filtros que ese
   * módulo no sabe aplicar.
   */
  it('un módulo desconocido no rompe, sólo no ofrece nada', () => {
    fixture.componentRef.setInput('modulo', 'facturas');
    fixture.detectChanges();

    expect(componente.vocabulario().entradas.length).toBe(0);
  });
});

describe('vocabulario del menú', () => {
  /**
   * La regla que sostiene toda la fase 5A: aquí sólo hay entradas que el servidor sabe filtrar.
   *
   * Esta prueba no puede comprobar el backend —eso lo hacen las de integración—, pero sí que
   * nadie añada una entrada con un filtro inventado sobre la marcha.
   */
  it('todas las entradas usan un filtro conocido, o ninguno', () => {
    const conocidos = Object.values(FILTROS) as string[];

    for (const entrada of ENTRADAS_TRANSVERSALES) {
      if (entrada.filtro === null) continue;
      expect(conocidos).toContain(entrada.filtro);
    }
  });

  it('los tres módulos con lista comparten el vocabulario transversal', () => {
    for (const modulo of ['tasks', 'tickets', 'projects']) {
      expect(VOCABULARIO[modulo].entradas).toBe(ENTRADAS_TRANSVERSALES);
    }
  });
});
