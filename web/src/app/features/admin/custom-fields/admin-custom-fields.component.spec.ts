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
  const CLIENTE: CustomFieldDefinition = {
    id: 'def-1', name: 'Cliente facturable', type: 'Text', targetEntity: 'Task',
    isRequired: false, options: [], position: 2, formula: null
  };

  const CANAL: CustomFieldDefinition = {
    id: 'def-2', name: 'Canal', type: 'Select', targetEntity: 'Task',
    isRequired: true, options: ['Web', 'Teléfono'], position: 0, formula: null
  };

  let servicio: jasmine.SpyObj<CustomFieldsService>;
  let fixture: ComponentFixture<AdminCustomFieldsComponent>;
  let componente: AdminCustomFieldsComponent;

  async function montar(definiciones: CustomFieldDefinition[] = []): Promise<void> {
    servicio.cargarDefiniciones.and.returnValue(of(definiciones));

    fixture = TestBed.createComponent(AdminCustomFieldsComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    servicio = jasmine.createSpyObj<CustomFieldsService>(
      'CustomFieldsService', ['cargarDefiniciones', 'definir', 'actualizar', 'borrar']
    );
    servicio.definir.and.returnValue(of(CLIENTE));
    servicio.actualizar.and.returnValue(of(void 0));
    servicio.borrar.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AdminCustomFieldsComponent],
      providers: [{ provide: CustomFieldsService, useValue: servicio }],
    }).compileComponents();
  });

  it('arranca pidiendo los campos de tareas', async () => {
    await montar();

    expect(servicio.cargarDefiniciones).toHaveBeenCalledWith('Task');
  });

  it('los enseña por posición, no por el orden en que lleguen', async () => {
    await montar([CLIENTE, CANAL]);

    expect(componente.ordenadas().map(d => d.id)).toEqual(['def-2', 'def-1']);
  });

  it('cambiar de entidad vuelve a pedir y cierra el formulario abierto', async () => {
    await montar([CLIENTE]);
    componente.nuevo();

    componente.cambiarEntidad('Project');

    expect(servicio.cargarDefiniciones).toHaveBeenCalledWith('Project');
    expect(componente.editando()).toBeNull();
  });

  describe('lo que impide guardar', () => {
    beforeEach(async () => {
      await montar();
      componente.nuevo();
    });

    it('un campo sin nombre', () => {
      componente.name = '   ';

      expect(componente.impedimento).toBeTruthy();
    });

    it('un nombre más largo de lo que admite el dominio', () => {
      componente.name = 'x'.repeat(81);

      expect(componente.impedimento).toBeTruthy();
    });

    it('una selección sin ninguna opción', () => {
      componente.name = 'Canal';
      componente.type = 'Select';
      componente.options = '   \n  \n';

      expect(componente.impedimento).toBeTruthy();
    });

    it('nada, cuando el campo está bien', () => {
      componente.name = 'Canal';
      componente.type = 'Select';
      componente.options = 'Web\nTeléfono';

      expect(componente.impedimento).toBe('');
    });

    it('un campo de texto no necesita opciones', () => {
      componente.name = 'Cliente facturable';
      componente.type = 'Text';

      expect(componente.impedimento).toBe('');
    });

    it('e impedido, no se manda nada al servidor', () => {
      componente.name = '';

      componente.guardar();

      expect(servicio.definir).not.toHaveBeenCalled();
    });
  });

  it('crea el campo con el nombre y las opciones ya limpias', async () => {
    await montar();
    componente.nuevo();
    componente.name = '  Canal  ';
    componente.type = 'Select';
    componente.options = 'Web\n  Web  \n\nTeléfono\n';
    componente.isRequired = true;
    componente.position = 3;

    componente.guardar();

    expect(servicio.definir).toHaveBeenCalledWith({
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
    await montar();
    componente.nuevo();
    componente.type = 'Select';
    componente.options = 'Web\nTeléfono';
    componente.type = 'Text';
    componente.name = 'Cliente facturable';

    componente.guardar();

    expect(servicio.definir).toHaveBeenCalledWith(jasmine.objectContaining({ options: [] }));
  });

  it('el campo nuevo se coloca detrás del último', async () => {
    await montar([CLIENTE, CANAL]);

    componente.nuevo();

    expect(componente.position).toBe(3);
  });

  it('editar carga el campo y deja de ser nuevo, que es lo que bloquea el tipo', async () => {
    await montar([CANAL]);

    componente.editar(CANAL);

    expect(componente.esNuevo()).toBeFalse();
    expect(componente.name).toBe('Canal');
    expect(componente.options).toBe('Web\nTeléfono');
  });

  it('editar actualiza en lugar de crear', async () => {
    await montar([CANAL]);
    componente.editar(CANAL);
    componente.name = 'Canal de entrada';

    componente.guardar();

    expect(servicio.definir).not.toHaveBeenCalled();
    expect(servicio.actualizar).toHaveBeenCalledWith('def-2', 'Task', {
      name: 'Canal de entrada',
      isRequired: true,
      options: ['Web', 'Teléfono'],
      position: 0, formula: null
    });
  });

  it('tras guardar cierra el formulario y relee la lista', async () => {
    await montar([CLIENTE]);
    componente.nuevo();
    componente.name = 'Otro';

    componente.guardar();

    expect(componente.editando()).toBeNull();
    expect(servicio.cargarDefiniciones).toHaveBeenCalledTimes(2);
  });

  it('si el servidor rechaza, el formulario sigue abierto con su explicación', async () => {
    await montar();
    servicio.definir.and.returnValue(throwError(() => ({ error: 'Ya hay un campo con ese nombre para esa entidad' })));
    componente.nuevo();
    componente.name = 'Canal';

    componente.guardar();

    // Cerrar el formulario perdería lo escrito y dejaría el error sin nada a lo que referirse.
    expect(componente.editando()).not.toBeNull();
    expect(componente.error()).toBe('Ya hay un campo con ese nombre para esa entidad');
  });

  it('el borrado se pide dos veces: una para armarlo y otra para confirmarlo', async () => {
    await montar([CANAL]);

    componente.borrando.set(CANAL.id);
    expect(servicio.borrar).not.toHaveBeenCalled();

    componente.borrar(CANAL);
    expect(servicio.borrar).toHaveBeenCalledWith('def-2', 'Task');
    expect(componente.borrando()).toBeNull();
  });

  it('un borrado que falla lo dice y desarma la confirmación', async () => {
    await montar([CANAL]);
    servicio.borrar.and.returnValue(throwError(() => ({ error: { detail: 'No se pudo borrar' } })));
    componente.borrando.set(CANAL.id);

    componente.borrar(CANAL);

    expect(componente.error()).toBe('No se pudo borrar');
    expect(componente.borrando()).toBeNull();
  });

  it('si la carga falla lo dice en lugar de enseñar una lista vacía', async () => {
    servicio.cargarDefiniciones.and.returnValue(throwError(() => ({ error: 'Sin permiso' })));

    fixture = TestBed.createComponent(AdminCustomFieldsComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();

    expect(componente.error()).toBe('Sin permiso');
    expect(componente.cargando()).toBeFalse();
  });
});
