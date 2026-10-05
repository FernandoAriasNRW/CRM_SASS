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
    fieldsByTrigger: {
      TaskCreated: ['AssigneeId'],
      TaskStatusChanged: ['Status', 'AssigneeId'],
    },
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

  it('asks for the vocabulary and rules on open', async () => {
    await mount([RULE]);

    expect(service.vocabulary).toHaveBeenCalled();
    expect(component.rules()).toEqual([RULE]);
  });

  it('the form is built from what the server says', async () => {
    await mount();

    component.startNew();

    expect(component.trigger).toBe('TaskCreated');
    expect(component.actions).toEqual([{ type: 'ChangeStatus', value: '' }]);
  });

  /** Una regla sin acciones se ejecutaría entera para no hacer nada. */
  it('a new automation starts with one action', async () => {
    await mount();

    component.startNew();

    expect(component.actions.length).toBe(1);
  });

  /**
   * Una condición sobre un campo que el disparador no trae no se cumple nunca, y se anotaba como
   * «condiciones no cumplidas» sin avisar a nadie. El formulario sólo ofrece los campos que trae.
   */
  describe('fields by trigger', () => {
    function fieldOptions(): string[] {
      const select = fixture.nativeElement.querySelector('select[aria-label="Campo"]') as HTMLSelectElement;
      return Array.from(select.options).map(o => o.value);
    }

    it('the field dropdown only offers the chosen trigger fields', async () => {
      await mount();
      component.startNew();
      component.addCondition();
      fixture.detectChanges();

      expect(component.trigger).toBe('TaskCreated');
      expect(fieldOptions()).toEqual(['AssigneeId']);

      component.changeTrigger('TaskStatusChanged');
      fixture.detectChanges();

      expect(fieldOptions()).toEqual(['Status', 'AssigneeId']);
    });

    it('a new condition starts with a trigger field', async () => {
      await mount();
      component.startNew();

      component.addCondition();

      expect(component.conditions[0].field).toBe('AssigneeId');
    });

    it('changing the trigger removes conditions the new one lacks, and says so', async () => {
      await mount();
      component.startNew();
      component.changeTrigger('TaskStatusChanged');
      component.conditions = [
        { field: 'Status', operator: 'EqualTo', value: 'Done' },
        { field: 'AssigneeId', operator: 'IsEmpty', value: null },
      ];

      component.changeTrigger('TaskCreated');
      fixture.detectChanges();

      expect(component.trigger).toBe('TaskCreated');
      expect(component.conditions).toEqual([{ field: 'AssigneeId', operator: 'IsEmpty', value: null }]);
      const notice = fixture.nativeElement.querySelector('[data-testid="dropped-conditions"]');
      expect(notice?.textContent).toContain('Estado');
    });

    it('if none is removed, it warns about nothing', async () => {
      await mount();
      component.startNew();
      component.conditions = [{ field: 'AssigneeId', operator: 'IsEmpty', value: null }];

      component.changeTrigger('TaskStatusChanged');

      expect(component.conditions.length).toBe(1);
      expect(component.droppedNotice()).toBe('');
    });

    /** Reglas guardadas antes de que el servidor lo comprobara. */
    describe('an old rule with a condition its trigger lacks', () => {
      const LEGACY: AutomationRule = {
        ...RULE, id: 'r2', name: 'Título con 8b', trigger: 'TaskCreated',
        conditions: [{ field: 'Title', operator: 'Contains', value: '8b' }],
      };

      it('is flagged in the list', async () => {
        await mount([RULE, LEGACY]);

        const marks = fixture.nativeElement.querySelectorAll('[data-testid="unreachable-condition"]');
        expect(marks.length).toBe(1);
        expect(component.hasUnreachableCondition(LEGACY)).toBeTrue();
        expect(component.hasUnreachableCondition(RULE)).toBeFalse();
      });

      it('editing it shows the condition and blocks saving until fixed', async () => {
        await mount([LEGACY]);

        component.edit(LEGACY);
        fixture.detectChanges();

        expect(component.conditions[0].field).toBe('Title');
        expect(fieldOptions()).toEqual(['AssigneeId', 'Title']);
        expect(component.blocker).toContain('no trae');

        component.save();
        expect(service.update).not.toHaveBeenCalled();
      });
    });
  });

  describe('what blocks saving', () => {
    beforeEach(async () => {
      await mount();
      component.startNew();
      component.actions = [{ type: 'ChangePriority', value: 'Low' }];
    });

    it('an automation without a name', () => {
      component.name = '  ';

      expect(component.blocker).toBeTruthy();
    });

    it('an action without a value', () => {
      component.name = 'Algo';
      component.actions = [{ type: 'ChangePriority', value: '' }];

      expect(component.blocker).toBeTruthy();
    });

    it('a comparing condition that does not say against what', () => {
      component.name = 'Algo';
      component.conditions = [{ field: 'Status', operator: 'EqualTo', value: '' }];

      expect(component.blocker).toBeTruthy();
    });

    /** «Está vacío» es el único operador que no compara contra nada. */
    it('nothing, if the condition uses an operator that needs no value', () => {
      component.name = 'Algo';
      component.conditions = [{ field: 'AssigneeId', operator: 'IsEmpty', value: '' }];

      expect(component.blocker).toBe('');
    });

    it('and when blocked, nothing is sent to the server', () => {
      component.name = '';

      component.save();

      expect(service.create).not.toHaveBeenCalled();
    });
  });

  it('creates the rule with clean conditions and actions', async () => {
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

  it('a condition without value is sent as null, not as an empty string', async () => {
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

  it('editing loads the rule and updates instead of creating', async () => {
    await mount([RULE]);

    component.edit(RULE);
    component.save();

    expect(component.isNew()).toBeFalse();
    expect(service.create).not.toHaveBeenCalled();
    expect(service.update).toHaveBeenCalledWith('r1', jasmine.objectContaining({ name: 'Bajar al cerrar' }));
  });

  it('editing does not touch the listed rule until the server accepts', async () => {
    await mount([RULE]);

    component.edit(RULE);
    component.conditions[0].value = 'In Review';

    expect(component.rules()[0].conditions[0].value).toBe('Done');
  });

  it('if the server rejects, the form stays open with its explanation', async () => {
    await mount();
    service.create.and.returnValue(throwError(() => ({ error: 'Ya hay una automatización con ese nombre' })));
    component.startNew();
    component.name = 'Repetida';
    component.actions = [{ type: 'ChangePriority', value: 'Low' }];

    component.save();

    expect(component.editing()).not.toBeNull();
    expect(component.error()).toBe('Ya hay una automatización con ese nombre');
  });

  describe('turning off and on', () => {
    it('flips the switch and tells the server', async () => {
      await mount([RULE]);

      component.toggleActive(RULE);

      expect(service.setActive).toHaveBeenCalledWith('r1', false);
      expect(component.rules()[0].isActive).toBeFalse();
    });

    /**
     * Una automatización que se ve apagada y sigue ejecutándose es la peor mentira posible en
     * esta pantalla: nadie vuelve a mirarla.
     */
    it('if the server rejects, the switch goes back', async () => {
      await mount([RULE]);
      service.setActive.and.returnValue(throwError(() => ({ error: 'No se pudo' })));

      component.toggleActive(RULE);

      expect(component.rules()[0].isActive).toBeTrue();
      expect(component.error()).toBe('No se pudo');
    });
  });

  it('deleting takes two clicks: one to arm and one to confirm', async () => {
    await mount([RULE]);

    component.deleting.set(RULE.id);
    expect(service.remove).not.toHaveBeenCalled();

    component.remove(RULE);
    expect(service.remove).toHaveBeenCalledWith('r1');
  });

  it('the summary says what the rule does without opening it', async () => {
    await mount([RULE]);

    expect(component.summaryOf(RULE)).toContain('Estado es igual a Done');
    expect(component.summaryOf(RULE)).toContain('Cambiar la prioridad: Low');
  });

  it('if loading fails it says so instead of showing an empty list', async () => {
    service.rules.and.returnValue(throwError(() => ({ error: 'Sin permiso' })));

    fixture = TestBed.createComponent(AdminAutomationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.error()).toBe('Sin permiso');
    expect(component.loading()).toBeFalse();
  });
});
