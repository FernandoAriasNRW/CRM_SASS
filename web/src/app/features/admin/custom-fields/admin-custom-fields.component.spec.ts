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

  it('arranca pidiendo los campos de tareas', async () => {
    await mount();

    expect(service.loadDefinitions).toHaveBeenCalledWith('Task');
  });

  it('los enseña por posición, no por el orden en que lleguen', async () => {
    await mount([CLIENT_FIELD, CANAL]);

    expect(component.sorted().map(d => d.id)).toEqual(['def-2', 'def-1']);
  });

  it('cambiar de entidad vuelve a pedir y cierra el formulario abierto', async () => {
    await mount([CLIENT_FIELD]);
    component.startNew();

    component.changeEntity('Project');

    expect(service.loadDefinitions).toHaveBeenCalledWith('Project');
    expect(component.editing()).toBeNull();
  });

  describe('lo que impide guardar', () => {
    beforeEach(async () => {
      await mount();
      component.startNew();
    });

    it('un campo sin nombre', () => {
      component.name = '   ';

      expect(component.blocker).toBeTruthy();
    });

    it('un nombre más largo de lo que admite el dominio', () => {
      component.name = 'x'.repeat(81);

      expect(component.blocker).toBeTruthy();
    });

    it('una selección sin ninguna opción', () => {
      component.name = 'Canal';
      component.type = 'Select';
      component.options = '   \n  \n';

      expect(component.blocker).toBeTruthy();
    });

    it('nada, cuando el campo está bien', () => {
      component.name = 'Canal';
      component.type = 'Select';
      component.options = 'Web\nTeléfono';

      expect(component.blocker).toBe('');
    });

    it('un campo de texto no necesita opciones', () => {
      component.name = 'Cliente facturable';
      component.type = 'Text';

      expect(component.blocker).toBe('');
    });

    it('e impedido, no se manda nada al servidor', () => {
      component.name = '';

      component.save();

      expect(service.define).not.toHaveBeenCalled();
    });
  });

  it('crea el campo con el nombre y las opciones ya limpias', async () => {
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

  it('un campo sin opciones no las manda aunque quedaran escritas de antes', async () => {
    await mount();
    component.startNew();
    component.type = 'Select';
    component.options = 'Web\nTeléfono';
    component.type = 'Text';
    component.name = 'Cliente facturable';

    component.save();

    expect(service.define).toHaveBeenCalledWith(jasmine.objectContaining({ options: [] }));
  });

  it('el campo nuevo se coloca detrás del último', async () => {
    await mount([CLIENT_FIELD, CANAL]);

    component.startNew();

    expect(component.position).toBe(3);
  });

  it('editar carga el campo y deja de ser nuevo, que es lo que bloquea el tipo', async () => {
    await mount([CANAL]);

    component.edit(CANAL);

    expect(component.isNew()).toBeFalse();
    expect(component.name).toBe('Canal');
    expect(component.options).toBe('Web\nTeléfono');
  });

  it('editar actualiza en lugar de crear', async () => {
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

  it('tras guardar cierra el formulario y relee la lista', async () => {
    await mount([CLIENT_FIELD]);
    component.startNew();
    component.name = 'Otro';

    component.save();

    expect(component.editing()).toBeNull();
    expect(service.loadDefinitions).toHaveBeenCalledTimes(2);
  });

  it('si el servidor rechaza, el formulario sigue abierto con su explicación', async () => {
    await mount();
    service.define.and.returnValue(throwError(() => ({ error: 'Ya hay un campo con ese nombre para esa entidad' })));
    component.startNew();
    component.name = 'Canal';

    component.save();

    // Cerrar el formulario perdería lo escrito y dejaría el error sin nada a lo que referirse.
    expect(component.editing()).not.toBeNull();
    expect(component.error()).toBe('Ya hay un campo con ese nombre para esa entidad');
  });

  it('el borrado se pide dos veces: una para armarlo y otra para confirmarlo', async () => {
    await mount([CANAL]);

    component.deleting.set(CANAL.id);
    expect(service.remove).not.toHaveBeenCalled();

    component.remove(CANAL);
    expect(service.remove).toHaveBeenCalledWith('def-2', 'Task');
    expect(component.deleting()).toBeNull();
  });

  it('un borrado que falla lo dice y desarma la confirmación', async () => {
    await mount([CANAL]);
    service.remove.and.returnValue(throwError(() => ({ error: { detail: 'No se pudo borrar' } })));
    component.deleting.set(CANAL.id);

    component.remove(CANAL);

    expect(component.error()).toBe('No se pudo borrar');
    expect(component.deleting()).toBeNull();
  });

  it('si la carga falla lo dice en lugar de enseñar una lista vacía', async () => {
    service.loadDefinitions.and.returnValue(throwError(() => ({ error: 'Sin permiso' })));

    fixture = TestBed.createComponent(AdminCustomFieldsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.error()).toBe('Sin permiso');
    expect(component.loading()).toBeFalse();
  });
});
