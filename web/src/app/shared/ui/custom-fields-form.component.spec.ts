import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CustomFieldsFormComponent } from './custom-fields-form.component';
import { CustomFieldsService, type CustomFieldValue } from '../../core/custom-fields.service';
import { UsersService } from '../../core/users.service';

/**
 * Lo que se comprueba aquí es la promesa del formulario: **lo que se ve en pantalla es lo que el
 * servidor aceptó**. El componente guarda campo a campo y pinta el valor nuevo antes de tener
 * respuesta; si el servidor lo rechaza y el valor se quedara puesto, la pantalla estaría mintiendo
 * —el mismo defecto que ya se corrigió en tableros y en prioridad—.
 */
describe('CustomFieldsFormComponent', () => {
  const TEXT_FIELD: CustomFieldValue = {
    definitionId: 'def-texto', name: 'Cliente facturable', type: 'Text',
    isRequired: false, options: [], position: 0, value: 'Acme',
  };

  const MULTIPLE: CustomFieldValue = {
    definitionId: 'def-multiple', name: 'Canales', type: 'MultiSelect',
    isRequired: false, options: ['Web', 'Teléfono', 'Correo'], position: 1, value: 'Web',
  };

  let service: jasmine.SpyObj<CustomFieldsService>;
  let fixture: ComponentFixture<CustomFieldsFormComponent>;

  async function mount(fields: CustomFieldValue[]): Promise<void> {
    service.valuesOf.and.returnValue(of(fields));

    fixture = TestBed.createComponent(CustomFieldsFormComponent);
    fixture.componentRef.setInput('entity', 'Task');
    fixture.componentRef.setInput('entityId', 'tarea-1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    service = jasmine.createSpyObj<CustomFieldsService>('CustomFieldsService', ['valuesOf', 'saveValue']);
    service.saveValue.and.returnValue(of(void 0));

    const users = {
      users: () => [],
      loadTenantUsers: () => of([]),
      getUser: () => undefined,
    };

    await TestBed.configureTestingModule({
      imports: [CustomFieldsFormComponent],
      providers: [
        { provide: CustomFieldsService, useValue: service },
        { provide: UsersService, useValue: users },
      ],
    }).compileComponents();
  });

  it('no pinta ni encabezado cuando el inquilino no ha definido campos', async () => {
    await mount([]);

    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });

  it('pinta un campo por definición, con su nombre', async () => {
    await mount([TEXT_FIELD, MULTIPLE]);

    expect(fixture.nativeElement.textContent).toContain('Cliente facturable');
    expect(fixture.nativeElement.textContent).toContain('Canales');
  });

  it('guarda el valor tal cual, sin normalizarlo en el navegador', async () => {
    await mount([TEXT_FIELD]);

    // La coma decimal la arregla el servidor: normalizar aquí serían dos reglas que discrepan.
    fixture.componentInstance.save(TEXT_FIELD, '1,5');

    expect(service.saveValue).toHaveBeenCalledWith('def-texto', 'tarea-1', '1,5');
  });

  it('el valor vacío se manda como nulo, que es como se borra', async () => {
    await mount([TEXT_FIELD]);

    fixture.componentInstance.save(TEXT_FIELD, '');

    expect(service.saveValue).toHaveBeenCalledWith('def-texto', 'tarea-1', null);
  });

  it('si el servidor rechaza, revierte el valor y enseña su explicación', async () => {
    await mount([TEXT_FIELD]);
    service.saveValue.and.returnValue(throwError(() => ({ error: 'No es un número' })));

    fixture.componentInstance.save(TEXT_FIELD, 'no soy un número');
    fixture.detectChanges();

    expect(fixture.componentInstance.fields()[0].value).toBe('Acme');
    expect(fixture.componentInstance.errors()['def-texto']).toBe('No es un número');
    expect(fixture.nativeElement.textContent).toContain('No es un número');
  });

  it('entiende también el ProblemDetails del manejador global', async () => {
    await mount([TEXT_FIELD]);
    service.saveValue.and.returnValue(throwError(() => ({ error: { detail: 'El campo es obligatorio' } })));

    fixture.componentInstance.save(TEXT_FIELD, '');

    expect(fixture.componentInstance.errors()['def-texto']).toBe('El campo es obligatorio');
  });

  it('un rechazo sin mensaje no deja al usuario sin explicación', async () => {
    await mount([TEXT_FIELD]);
    service.saveValue.and.returnValue(throwError(() => ({ status: 500 })));

    fixture.componentInstance.save(TEXT_FIELD, 'algo');

    expect(fixture.componentInstance.errors()['def-texto']).toBeTruthy();
  });

  it('un guardado correcto deja el valor nuevo como el bueno al que revertir', async () => {
    await mount([TEXT_FIELD]);

    fixture.componentInstance.save(TEXT_FIELD, 'Globex');
    service.saveValue.and.returnValue(throwError(() => ({ error: 'No vale' })));
    fixture.componentInstance.save({ ...TEXT_FIELD, value: 'Globex' }, 'Initech');

    expect(fixture.componentInstance.fields()[0].value).toBe('Globex');
  });

  describe('selección múltiple', () => {
    it('marca una opción añadiéndola a las que ya había', async () => {
      await mount([MULTIPLE]);

      fixture.componentInstance.toggleOption(MULTIPLE, 'Correo');

      expect(service.saveValue).toHaveBeenCalledWith('def-multiple', 'tarea-1', 'Web\nCorreo');
    });

    it('desmarca la que ya estaba', async () => {
      await mount([MULTIPLE]);

      fixture.componentInstance.toggleOption(MULTIPLE, 'Web');

      // Sin ninguna marcada se manda nulo: una cadena vacía no es «ninguna opción», es basura.
      expect(service.saveValue).toHaveBeenCalledWith('def-multiple', 'tarea-1', null);
    });

    it('sabe cuáles están marcadas', async () => {
      await mount([MULTIPLE]);
      const component = fixture.componentInstance;

      expect(component.isChecked(MULTIPLE, 'Web')).toBeTrue();
      expect(component.isChecked(MULTIPLE, 'Correo')).toBeFalse();
    });
  });

  it('un tipo que esta versión no sabe pintar enseña el valor en crudo', async () => {
    const unknown: CustomFieldValue = {
      definitionId: 'def-raro', name: 'Fórmula', type: 'Calculado',
      isRequired: false, options: [], position: 0, value: '42',
    };

    await mount([unknown]);

    // Esconder el campo haría creer que el dato se ha perdido.
    expect(fixture.nativeElement.textContent).toContain('42');
  });
});
