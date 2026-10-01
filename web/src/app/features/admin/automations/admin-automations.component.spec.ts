import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminAutomationsComponent } from './admin-automations.component';
import {
  AutomationsService, type AutomationRule, type AutomationVocabulary,
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
  const VOCABULARY: AutomationVocabulary = {
    triggers: ['TaskCreated', 'TaskStatusChanged'],
    fields: ['Status', 'AssigneeId'],
    operators: ['EqualTo', 'IsEmpty'],
    actions: ['ChangeStatus', 'ChangePriority'],
  };

  const RULE: AutomationRule = {
    id: 'r1', name: 'Bajar al cerrar', trigger: 'TaskStatusChanged', isActive: true,
    conditions: [{ field: 'Status', operator: 'EqualTo', value: 'Done' }],
    actions: [{ type: 'ChangePriority', value: 'Low' }],
    executionCount: 3, lastExecutedAtUtc: '2026-08-14T10:00:00Z',
  };

  let service: jasmine.SpyObj<AutomationsService>;
  let fixture: ComponentFixture<AdminAutomationsComponent>;
  let component: AdminAutomationsComponent;

  async function mount(rules: AutomationRule[] = []): Promise<void> {
    service.rules.and.returnValue(of(rules));

    fixture = TestBed.createComponent(AdminAutomationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    service = jasmine.createSpyObj<AutomationsService>(
      'AutomationsService', ['vocabulary', 'rules', 'create', 'update', 'setActive', 'remove']);

    service.vocabulary.and.returnValue(of(VOCABULARY));
    service.create.and.returnValue(of(RULE));
    service.update.and.returnValue(of(void 0));
    service.setActive.and.returnValue(of(void 0));
    service.remove.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AdminAutomationsComponent],
      providers: [{ provide: AutomationsService, useValue: service }],
    }).compileComponents();
  });

  it('pide el vocabulario y las reglas al abrir', async () => {
    await mount([RULE]);

    expect(service.vocabulary).toHaveBeenCalled();
    expect(component.rules()).toEqual([RULE]);
  });

  it('el formulario se arma con lo que dice el servidor', async () => {
    await mount();

    component.startNew();

    expect(component.trigger).toBe('TaskCreated');
    expect(component.actions).toEqual([{ type: 'ChangeStatus', value: '' }]);
  });

  /** Una regla sin acciones se ejecutaría entera para no hacer nada. */
  it('una automatización nueva empieza con una acción', async () => {
    await mount();

    component.startNew();

    expect(component.actions.length).toBe(1);
  });

  describe('lo que impide guardar', () => {
    beforeEach(async () => {
      await mount();
      component.startNew();
      component.actions = [{ type: 'ChangePriority', value: 'Low' }];
    });

    it('una automatización sin nombre', () => {
      component.name = '  ';

      expect(component.blocker).toBeTruthy();
    });

    it('una acción sin valor', () => {
      component.name = 'Algo';
      component.actions = [{ type: 'ChangePriority', value: '' }];

      expect(component.blocker).toBeTruthy();
    });

    it('una condición que compara y no dice contra qué', () => {
      component.name = 'Algo';
      component.conditions = [{ field: 'Status', operator: 'EqualTo', value: '' }];

      expect(component.blocker).toBeTruthy();
    });

    /** «Está vacío» es el único operador que no compara contra nada. */
    it('nada, si la condición usa un operador que no necesita valor', () => {
      component.name = 'Algo';
      component.conditions = [{ field: 'AssigneeId', operator: 'IsEmpty', value: '' }];

      expect(component.blocker).toBe('');
    });

    it('e impedida, no se manda nada al servidor', () => {
      component.name = '';

      component.save();

      expect(service.create).not.toHaveBeenCalled();
    });
  });

  it('crea la regla con las condiciones y acciones limpias', async () => {
    await mount();
    component.startNew();
    component.name = '  Bajar al cerrar  ';
    component.trigger = 'TaskStatusChanged';
    component.conditions = [{ field: 'Status', operator: 'EqualTo', value: ' Done ' }];
    component.actions = [{ type: 'ChangePriority', value: ' Low ' }];

    component.save();

    expect(service.create).toHaveBeenCalledWith({
      name: 'Bajar al cerrar',
      trigger: 'TaskStatusChanged',
      conditions: [{ field: 'Status', operator: 'EqualTo', value: 'Done' }],
      actions: [{ type: 'ChangePriority', value: 'Low' }],
    });
  });

  it('una condición sin valor se manda como nula, no como cadena vacía', async () => {
    await mount();
    component.startNew();
    component.name = 'Sin responsable';
    component.conditions = [{ field: 'AssigneeId', operator: 'IsEmpty', value: 'ruido' }];
    component.actions = [{ type: 'ChangePriority', value: 'High' }];

    component.save();

    expect(service.create).toHaveBeenCalledWith(jasmine.objectContaining({
      conditions: [{ field: 'AssigneeId', operator: 'IsEmpty', value: null }],
    }));
  });

  it('editar carga la regla y actualiza en lugar de crear', async () => {
    await mount([RULE]);

    component.edit(RULE);
    component.save();

    expect(component.isNew()).toBeFalse();
    expect(service.create).not.toHaveBeenCalled();
    expect(service.update).toHaveBeenCalledWith('r1', jasmine.objectContaining({ name: 'Bajar al cerrar' }));
  });

  it('editar no toca la regla de la lista hasta que el servidor acepte', async () => {
    await mount([RULE]);

    component.edit(RULE);
    component.conditions[0].value = 'In Review';

    expect(component.rules()[0].conditions[0].value).toBe('Done');
  });

  it('si el servidor rechaza, el formulario sigue abierto con su explicación', async () => {
    await mount();
    service.create.and.returnValue(throwError(() => ({ error: 'Ya hay una automatización con ese nombre' })));
    component.startNew();
    component.name = 'Repetida';
    component.actions = [{ type: 'ChangePriority', value: 'Low' }];

    component.save();

    expect(component.editing()).not.toBeNull();
    expect(component.error()).toBe('Ya hay una automatización con ese nombre');
  });

  describe('apagar y encender', () => {
    it('cambia el interruptor y avisa al servidor', async () => {
      await mount([RULE]);

      component.toggleActive(RULE);

      expect(service.setActive).toHaveBeenCalledWith('r1', false);
      expect(component.rules()[0].isActive).toBeFalse();
    });

    /**
     * Una automatización que se ve apagada y sigue ejecutándose es la peor mentira posible en
     * esta pantalla: nadie vuelve a mirarla.
     */
    it('si el servidor rechaza, el interruptor vuelve a donde estaba', async () => {
      await mount([RULE]);
      service.setActive.and.returnValue(throwError(() => ({ error: 'No se pudo' })));

      component.toggleActive(RULE);

      expect(component.rules()[0].isActive).toBeTrue();
      expect(component.error()).toBe('No se pudo');
    });
  });

  it('el borrado se pide dos veces: una para armarlo y otra para confirmarlo', async () => {
    await mount([RULE]);

    component.deleting.set(RULE.id);
    expect(service.remove).not.toHaveBeenCalled();

    component.remove(RULE);
    expect(service.remove).toHaveBeenCalledWith('r1');
  });

  it('el resumen dice qué hace la regla sin tener que abrirla', async () => {
    await mount([RULE]);

    expect(component.summaryOf(RULE)).toContain('Estado es igual a Done');
    expect(component.summaryOf(RULE)).toContain('Cambiar la prioridad: Low');
  });

  it('si la carga falla lo dice en lugar de enseñar una lista vacía', async () => {
    service.rules.and.returnValue(throwError(() => ({ error: 'Sin permiso' })));

    fixture = TestBed.createComponent(AdminAutomationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.error()).toBe('Sin permiso');
    expect(component.loading()).toBeFalse();
  });
});
