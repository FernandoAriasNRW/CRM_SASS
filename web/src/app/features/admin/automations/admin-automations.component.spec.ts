import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminAutomationsComponent } from './admin-automations.component';
import {
  AutomationsService, type ReglaDeAutomatizacion, type VocabularioDeAutomatizacion,
} from '../../../core/automations.service';

/**
 * La pantalla de automatizaciones.
 *
 * Lo que fijan estas pruebas es que **el formulario se construye con el vocabulario del
 * servidor**, no con listas escritas en el cliente, y que apagar una regla no miente: si el
 * servidor rechaza el cambio, el interruptor vuelve a donde estaba. Una automatización que se ve
 * apagada y sigue ejecutándose es la peor mentira posible en esta pantalla.
 */
describe('AdminAutomationsComponent', () => {
  const VOCABULARIO: VocabularioDeAutomatizacion = {
    disparadores: ['TareaCreada', 'TareaCambiaDeEstado'],
    campos: ['Estado', 'ResponsableId'],
    operadores: ['Igual', 'EstaVacio'],
    acciones: ['CambiarEstado', 'CambiarPrioridad'],
  };

  const REGLA: ReglaDeAutomatizacion = {
    id: 'r1', nombre: 'Bajar al cerrar', disparador: 'TareaCambiaDeEstado', activa: true,
    condiciones: [{ campo: 'Estado', operador: 'Igual', valor: 'Done' }],
    acciones: [{ tipo: 'CambiarPrioridad', valor: 'Low' }],
    vecesEjecutada: 3, ultimaEjecucionUtc: '2026-08-14T10:00:00Z',
  };

  let servicio: jasmine.SpyObj<AutomationsService>;
  let fixture: ComponentFixture<AdminAutomationsComponent>;
  let componente: AdminAutomationsComponent;

  async function montar(reglas: ReglaDeAutomatizacion[] = []): Promise<void> {
    servicio.reglas.and.returnValue(of(reglas));

    fixture = TestBed.createComponent(AdminAutomationsComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    servicio = jasmine.createSpyObj<AutomationsService>(
      'AutomationsService', ['vocabulario', 'reglas', 'crear', 'actualizar', 'activar', 'borrar']);

    servicio.vocabulario.and.returnValue(of(VOCABULARIO));
    servicio.crear.and.returnValue(of(REGLA));
    servicio.actualizar.and.returnValue(of(void 0));
    servicio.activar.and.returnValue(of(void 0));
    servicio.borrar.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AdminAutomationsComponent],
      providers: [{ provide: AutomationsService, useValue: servicio }],
    }).compileComponents();
  });

  it('pide el vocabulario y las reglas al abrir', async () => {
    await montar([REGLA]);

    expect(servicio.vocabulario).toHaveBeenCalled();
    expect(componente.reglas()).toEqual([REGLA]);
  });

  it('el formulario se arma con lo que dice el servidor', async () => {
    await montar();

    componente.nueva();

    expect(componente.disparador).toBe('TareaCreada');
    expect(componente.acciones).toEqual([{ tipo: 'CambiarEstado', valor: '' }]);
  });

  /** Una regla sin acciones se ejecutaría entera para no hacer nada. */
  it('una automatización nueva empieza con una acción', async () => {
    await montar();

    componente.nueva();

    expect(componente.acciones.length).toBe(1);
  });

  describe('lo que impide guardar', () => {
    beforeEach(async () => {
      await montar();
      componente.nueva();
      componente.acciones = [{ tipo: 'CambiarPrioridad', valor: 'Low' }];
    });

    it('una automatización sin nombre', () => {
      componente.nombre = '  ';

      expect(componente.impedimento).toBeTruthy();
    });

    it('una acción sin valor', () => {
      componente.nombre = 'Algo';
      componente.acciones = [{ tipo: 'CambiarPrioridad', valor: '' }];

      expect(componente.impedimento).toBeTruthy();
    });

    it('una condición que compara y no dice contra qué', () => {
      componente.nombre = 'Algo';
      componente.condiciones = [{ campo: 'Estado', operador: 'Igual', valor: '' }];

      expect(componente.impedimento).toBeTruthy();
    });

    /** «Está vacío» es el único operador que no compara contra nada. */
    it('nada, si la condición usa un operador que no necesita valor', () => {
      componente.nombre = 'Algo';
      componente.condiciones = [{ campo: 'ResponsableId', operador: 'EstaVacio', valor: '' }];

      expect(componente.impedimento).toBe('');
    });

    it('e impedida, no se manda nada al servidor', () => {
      componente.nombre = '';

      componente.guardar();

      expect(servicio.crear).not.toHaveBeenCalled();
    });
  });

  it('crea la regla con las condiciones y acciones limpias', async () => {
    await montar();
    componente.nueva();
    componente.nombre = '  Bajar al cerrar  ';
    componente.disparador = 'TareaCambiaDeEstado';
    componente.condiciones = [{ campo: 'Estado', operador: 'Igual', valor: ' Done ' }];
    componente.acciones = [{ tipo: 'CambiarPrioridad', valor: ' Low ' }];

    componente.guardar();

    expect(servicio.crear).toHaveBeenCalledWith({
      nombre: 'Bajar al cerrar',
      disparador: 'TareaCambiaDeEstado',
      condiciones: [{ campo: 'Estado', operador: 'Igual', valor: 'Done' }],
      acciones: [{ tipo: 'CambiarPrioridad', valor: 'Low' }],
    });
  });

  it('una condición sin valor se manda como nula, no como cadena vacía', async () => {
    await montar();
    componente.nueva();
    componente.nombre = 'Sin responsable';
    componente.condiciones = [{ campo: 'ResponsableId', operador: 'EstaVacio', valor: 'ruido' }];
    componente.acciones = [{ tipo: 'CambiarPrioridad', valor: 'High' }];

    componente.guardar();

    expect(servicio.crear).toHaveBeenCalledWith(jasmine.objectContaining({
      condiciones: [{ campo: 'ResponsableId', operador: 'EstaVacio', valor: null }],
    }));
  });

  it('editar carga la regla y actualiza en lugar de crear', async () => {
    await montar([REGLA]);

    componente.editar(REGLA);
    componente.guardar();

    expect(componente.esNueva()).toBeFalse();
    expect(servicio.crear).not.toHaveBeenCalled();
    expect(servicio.actualizar).toHaveBeenCalledWith('r1', jasmine.objectContaining({ nombre: 'Bajar al cerrar' }));
  });

  it('editar no toca la regla de la lista hasta que el servidor acepte', async () => {
    await montar([REGLA]);

    componente.editar(REGLA);
    componente.condiciones[0].valor = 'In Review';

    expect(componente.reglas()[0].condiciones[0].valor).toBe('Done');
  });

  it('si el servidor rechaza, el formulario sigue abierto con su explicación', async () => {
    await montar();
    servicio.crear.and.returnValue(throwError(() => ({ error: 'Ya hay una automatización con ese nombre' })));
    componente.nueva();
    componente.nombre = 'Repetida';
    componente.acciones = [{ tipo: 'CambiarPrioridad', valor: 'Low' }];

    componente.guardar();

    expect(componente.editando()).not.toBeNull();
    expect(componente.error()).toBe('Ya hay una automatización con ese nombre');
  });

  describe('apagar y encender', () => {
    it('cambia el interruptor y avisa al servidor', async () => {
      await montar([REGLA]);

      componente.alternarActiva(REGLA);

      expect(servicio.activar).toHaveBeenCalledWith('r1', false);
      expect(componente.reglas()[0].activa).toBeFalse();
    });

    /**
     * Una automatización que se ve apagada y sigue ejecutándose es la peor mentira posible en
     * esta pantalla: nadie vuelve a mirarla.
     */
    it('si el servidor rechaza, el interruptor vuelve a donde estaba', async () => {
      await montar([REGLA]);
      servicio.activar.and.returnValue(throwError(() => ({ error: 'No se pudo' })));

      componente.alternarActiva(REGLA);

      expect(componente.reglas()[0].activa).toBeTrue();
      expect(componente.error()).toBe('No se pudo');
    });
  });

  it('el borrado se pide dos veces: una para armarlo y otra para confirmarlo', async () => {
    await montar([REGLA]);

    componente.borrando.set(REGLA.id);
    expect(servicio.borrar).not.toHaveBeenCalled();

    componente.borrar(REGLA);
    expect(servicio.borrar).toHaveBeenCalledWith('r1');
  });

  it('el resumen dice qué hace la regla sin tener que abrirla', async () => {
    await montar([REGLA]);

    expect(componente.resumenDe(REGLA)).toContain('Estado Igual Done');
    expect(componente.resumenDe(REGLA)).toContain('CambiarPrioridad: Low');
  });

  it('si la carga falla lo dice en lugar de enseñar una lista vacía', async () => {
    servicio.reglas.and.returnValue(throwError(() => ({ error: 'Sin permiso' })));

    fixture = TestBed.createComponent(AdminAutomationsComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();

    expect(componente.error()).toBe('Sin permiso');
    expect(componente.cargando()).toBeFalse();
  });
});
