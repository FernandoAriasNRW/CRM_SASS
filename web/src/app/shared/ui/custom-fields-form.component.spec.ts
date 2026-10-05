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

  it('renders not even a heading when the tenant has no fields', async () => {
    await mount([]);

    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });

  it('renders one field per definition, with its name', async () => {
    await mount([TEXT_FIELD, MULTIPLE]);

    expect(fixture.nativeElement.textContent).toContain('Cliente facturable');
    expect(fixture.nativeElement.textContent).toContain('Canales');
  });

  it('saves the value as is, without normalizing it in the browser', async () => {
    await mount([TEXT_FIELD]);

    // La coma decimal la arregla el servidor: normalizar aquí serían dos reglas que discrepan.
    fixture.componentInstance.save(TEXT_FIELD, '1,5');

    expect(service.saveValue).toHaveBeenCalledWith('def-texto', 'tarea-1', '1,5');
  });

  it('an empty value is sent as null, which is how it is cleared', async () => {
    await mount([TEXT_FIELD]);

    fixture.componentInstance.save(TEXT_FIELD, '');

    expect(service.saveValue).toHaveBeenCalledWith('def-texto', 'tarea-1', null);
  });

  it('if the server rejects, it reverts the value and shows the explanation', async () => {
    await mount([TEXT_FIELD]);
    service.saveValue.and.returnValue(throwError(() => ({ error: 'No es un número' })));

    fixture.componentInstance.save(TEXT_FIELD, 'no soy un número');
    fixture.detectChanges();

    expect(fixture.componentInstance.fields()[0].value).toBe('Acme');
    expect(fixture.componentInstance.errors()['def-texto']).toBe('No es un número');
    expect(fixture.nativeElement.textContent).toContain('No es un número');
  });

  it('also understands the global handler ProblemDetails', async () => {
    await mount([TEXT_FIELD]);
    service.saveValue.and.returnValue(throwError(() => ({ error: { detail: 'El campo es obligatorio' } })));

    fixture.componentInstance.save(TEXT_FIELD, '');

    expect(fixture.componentInstance.errors()['def-texto']).toBe('El campo es obligatorio');
  });

  it('a rejection without message still explains something to the user', async () => {
    await mount([TEXT_FIELD]);
    service.saveValue.and.returnValue(throwError(() => ({ status: 500 })));

    fixture.componentInstance.save(TEXT_FIELD, 'algo');

    expect(fixture.componentInstance.errors()['def-texto']).toBeTruthy();
  });

  it('a successful save makes the new value the one to revert to', async () => {
    await mount([TEXT_FIELD]);

    fixture.componentInstance.save(TEXT_FIELD, 'Globex');
    service.saveValue.and.returnValue(throwError(() => ({ error: 'No vale' })));
    fixture.componentInstance.save({ ...TEXT_FIELD, value: 'Globex' }, 'Initech');

    expect(fixture.componentInstance.fields()[0].value).toBe('Globex');
  });

  describe('multi select', () => {
    it('checks an option adding it to the existing ones', async () => {
      await mount([MULTIPLE]);

      fixture.componentInstance.toggleOption(MULTIPLE, 'Correo');

      expect(service.saveValue).toHaveBeenCalledWith('def-multiple', 'tarea-1', 'Web\nCorreo');
    });

    it('unchecks the one already checked', async () => {
      await mount([MULTIPLE]);

      fixture.componentInstance.toggleOption(MULTIPLE, 'Web');

      // Sin ninguna marcada se manda nulo: una cadena vacía no es «ninguna opción», es basura.
      expect(service.saveValue).toHaveBeenCalledWith('def-multiple', 'tarea-1', null);
    });

    it('knows which ones are checked', async () => {
      await mount([MULTIPLE]);
      const component = fixture.componentInstance;

      expect(component.isChecked(MULTIPLE, 'Web')).toBeTrue();
      expect(component.isChecked(MULTIPLE, 'Correo')).toBeFalse();
    });
  });

  it('a type this version cannot render shows the raw value', async () => {
    const unknown: CustomFieldValue = {
      definitionId: 'def-raro', name: 'Fórmula', type: 'Calculado',
      isRequired: false, options: [], position: 0, value: '42',
    };

    await mount([unknown]);

    // Esconder el campo haría creer que el dato se ha perdido.
    expect(fixture.nativeElement.textContent).toContain('42');
  });
});
