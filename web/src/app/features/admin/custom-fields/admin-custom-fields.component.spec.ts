import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminCustomFieldsComponent } from './admin-custom-fields.component';
import { CustomFieldsService, type CustomFieldDefinition } from '../../../core/custom-fields.service';

/**
 * La pantalla de definiciones repite a propósito las reglas del dominio —nombre obligatorio, largo
 * máximo, una selección necesita opciones— para no gastar un viaje al servidor en decir lo obvio.
 * Repetir una regla es aceptar que puede desviarse, así que estas pruebas la fijan aquí y el
 * dominio la fija en su lado; si algún día discrepan, una de las dos suites lo dirá.
 */
describe('AdminCustomFieldsComponent', () => {
  const CLIENT_FIELD: CustomFieldDefinition = {
    id: 'def-1', name: 'Cliente facturable', type: 'Text', targetEntity: 'Task',
    isRequired: false, options: [], position: 2, formula: null
  };

  const CANAL: CustomFieldDefinition = {
    id: 'def-2', name: 'Canal', type: 'Select', targetEntity: 'Task',
    isRequired: true, options: ['Web', 'Teléfono'], position: 0, formula: null
  };

  let service: jasmine.SpyObj<CustomFieldsService>;
  let fixture: ComponentFixture<AdminCustomFieldsComponent>;
  let component: AdminCustomFieldsComponent;

  async function mount(definitions: CustomFieldDefinition[] = []): Promise<void> {
    service.loadDefinitions.and.returnValue(of(definitions));

    fixture = TestBed.createComponent(AdminCustomFieldsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    service = jasmine.createSpyObj<CustomFieldsService>(
      'CustomFieldsService', ['loadDefinitions', 'define', 'update', 'remove']
    );
    service.define.and.returnValue(of(CLIENT_FIELD));
    service.update.and.returnValue(of(void 0));
    service.remove.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AdminCustomFieldsComponent],
      providers: [{ provide: CustomFieldsService, useValue: service }],
    }).compileComponents();
  });

  it('starts by asking for the task fields', async () => {
    await mount();

    expect(service.loadDefinitions).toHaveBeenCalledWith('Task');
  });

  it('shows them by position, not by arrival order', async () => {
    await mount([CLIENT_FIELD, CANAL]);

    expect(component.sorted().map(d => d.id)).toEqual(['def-2', 'def-1']);
  });

  it('switching entity reloads and closes the open form', async () => {
    await mount([CLIENT_FIELD]);
    component.startNew();

    component.changeEntity('Project');

    expect(service.loadDefinitions).toHaveBeenCalledWith('Project');
    expect(component.editing()).toBeNull();
  });

  describe('what blocks saving', () => {
    beforeEach(async () => {
      await mount();
      component.startNew();
    });

    it('a field without a name', () => {
      component.name = '   ';

      expect(component.blocker).toBeTruthy();
    });

    it('a name longer than the domain allows', () => {
      component.name = 'x'.repeat(81);

      expect(component.blocker).toBeTruthy();
    });

    it('a select without any option', () => {
      component.name = 'Canal';
      component.type = 'Select';
      component.options = '   \n  \n';

      expect(component.blocker).toBeTruthy();
    });

    it('nothing, when the field is fine', () => {
      component.name = 'Canal';
      component.type = 'Select';
      component.options = 'Web\nTeléfono';

      expect(component.blocker).toBe('');
    });

    it('a text field needs no options', () => {
      component.name = 'Cliente facturable';
      component.type = 'Text';

      expect(component.blocker).toBe('');
    });

    it('and when blocked, nothing is sent to the server', () => {
      component.name = '';

      component.save();

      expect(service.define).not.toHaveBeenCalled();
    });
  });

  it('creates the field with the name and options already cleaned', async () => {
    await mount();
    component.startNew();
    component.name = '  Canal  ';
    component.type = 'Select';
    component.options = 'Web\n  Web  \n\nTeléfono\n';
    component.isRequired = true;
    component.position = 3;

    component.save();

    expect(service.define).toHaveBeenCalledWith({
      name: 'Canal',
      isRequired: true,
      options: ['Web', 'Teléfono'],
      position: 3,
      // Nula porque este campo es de selección: la fórmula sólo se manda en los calculados,
      // y guardarla en los demás confundiría a quien leyera la definición después.
      formula: null,
      type: 'Select',
      targetEntity: 'Task',
    });
  });

  it('a field without options does not send them even if typed before', async () => {
    await mount();
    component.startNew();
    component.type = 'Select';
    component.options = 'Web\nTeléfono';
    component.type = 'Text';
    component.name = 'Cliente facturable';

    component.save();

    expect(service.define).toHaveBeenCalledWith(jasmine.objectContaining({ options: [] }));
  });

  it('the new field goes after the last one', async () => {
    await mount([CLIENT_FIELD, CANAL]);

    component.startNew();

    expect(component.position).toBe(3);
  });

  it('editing loads the field and stops being new, which locks the type', async () => {
    await mount([CANAL]);

    component.edit(CANAL);

    expect(component.isNew()).toBeFalse();
    expect(component.name).toBe('Canal');
    expect(component.options).toBe('Web\nTeléfono');
  });

  it('editing updates instead of creating', async () => {
    await mount([CANAL]);
    component.edit(CANAL);
    component.name = 'Canal de entrada';

    component.save();

    expect(service.define).not.toHaveBeenCalled();
    expect(service.update).toHaveBeenCalledWith('def-2', 'Task', {
      name: 'Canal de entrada',
      isRequired: true,
      options: ['Web', 'Teléfono'],
      position: 0, formula: null
    });
  });

  it('after saving it closes the form and reloads the list', async () => {
    await mount([CLIENT_FIELD]);
    component.startNew();
    component.name = 'Otro';

    component.save();

    expect(component.editing()).toBeNull();
    expect(service.loadDefinitions).toHaveBeenCalledTimes(2);
  });

  it('if the server rejects, the form stays open with its explanation', async () => {
    await mount();
    service.define.and.returnValue(throwError(() => ({ error: 'Ya hay un campo con ese nombre para esa entidad' })));
    component.startNew();
    component.name = 'Canal';

    component.save();

    // Cerrar el formulario perdería lo escrito y dejaría el error sin nada a lo que referirse.
    expect(component.editing()).not.toBeNull();
    expect(component.error()).toBe('Ya hay un campo con ese nombre para esa entidad');
  });

  it('deleting takes two clicks: one to arm and one to confirm', async () => {
    await mount([CANAL]);

    component.deleting.set(CANAL.id);
    expect(service.remove).not.toHaveBeenCalled();

    component.remove(CANAL);
    expect(service.remove).toHaveBeenCalledWith('def-2', 'Task');
    expect(component.deleting()).toBeNull();
  });

  it('a failed delete says so and disarms the confirmation', async () => {
    await mount([CANAL]);
    service.remove.and.returnValue(throwError(() => ({ error: { detail: 'No se pudo borrar' } })));
    component.deleting.set(CANAL.id);

    component.remove(CANAL);

    expect(component.error()).toBe('No se pudo borrar');
    expect(component.deleting()).toBeNull();
  });

  it('if loading fails it says so instead of showing an empty list', async () => {
    service.loadDefinitions.and.returnValue(throwError(() => ({ error: 'Sin permiso' })));

    fixture = TestBed.createComponent(AdminCustomFieldsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.error()).toBe('Sin permiso');
    expect(component.loading()).toBeFalse();
  });
});
