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
  const TEXTO: CustomFieldValue = {
    definitionId: 'def-texto', nombre: 'Cliente facturable', tipo: 'Texto',
    obligatorio: false, opciones: [], posicion: 0, valor: 'Acme',
  };

  const MULTIPLE: CustomFieldValue = {
    definitionId: 'def-multiple', nombre: 'Canales', tipo: 'SeleccionMultiple',
    obligatorio: false, opciones: ['Web', 'Teléfono', 'Correo'], posicion: 1, valor: 'Web',
  };

  let servicio: jasmine.SpyObj<CustomFieldsService>;
  let fixture: ComponentFixture<CustomFieldsFormComponent>;

  async function montar(campos: CustomFieldValue[]): Promise<void> {
    servicio.valoresDe.and.returnValue(of(campos));

    fixture = TestBed.createComponent(CustomFieldsFormComponent);
    fixture.componentRef.setInput('entidad', 'Tarea');
    fixture.componentRef.setInput('entityId', 'tarea-1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    servicio = jasmine.createSpyObj<CustomFieldsService>('CustomFieldsService', ['valoresDe', 'guardarValor']);
    servicio.guardarValor.and.returnValue(of(void 0));

    const usuarios = {
      users: () => [],
      loadTenantUsers: () => of([]),
      getUser: () => undefined,
    };

    await TestBed.configureTestingModule({
      imports: [CustomFieldsFormComponent],
      providers: [
        { provide: CustomFieldsService, useValue: servicio },
        { provide: UsersService, useValue: usuarios },
      ],
    }).compileComponents();
  });

  it('no pinta ni encabezado cuando el inquilino no ha definido campos', async () => {
    await montar([]);

    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });

  it('pinta un campo por definición, con su nombre', async () => {
    await montar([TEXTO, MULTIPLE]);

    expect(fixture.nativeElement.textContent).toContain('Cliente facturable');
    expect(fixture.nativeElement.textContent).toContain('Canales');
  });

  it('guarda el valor tal cual, sin normalizarlo en el navegador', async () => {
    await montar([TEXTO]);

    // La coma decimal la arregla el servidor: normalizar aquí serían dos reglas que discrepan.
    fixture.componentInstance.guardar(TEXTO, '1,5');

    expect(servicio.guardarValor).toHaveBeenCalledWith('def-texto', 'tarea-1', '1,5');
  });

  it('el valor vacío se manda como nulo, que es como se borra', async () => {
    await montar([TEXTO]);

    fixture.componentInstance.guardar(TEXTO, '');

    expect(servicio.guardarValor).toHaveBeenCalledWith('def-texto', 'tarea-1', null);
  });

  it('si el servidor rechaza, revierte el valor y enseña su explicación', async () => {
    await montar([TEXTO]);
    servicio.guardarValor.and.returnValue(throwError(() => ({ error: 'No es un número' })));

    fixture.componentInstance.guardar(TEXTO, 'no soy un número');
    fixture.detectChanges();

    expect(fixture.componentInstance.campos()[0].valor).toBe('Acme');
    expect(fixture.componentInstance.errores()['def-texto']).toBe('No es un número');
    expect(fixture.nativeElement.textContent).toContain('No es un número');
  });

  it('entiende también el ProblemDetails del manejador global', async () => {
    await montar([TEXTO]);
    servicio.guardarValor.and.returnValue(throwError(() => ({ error: { detail: 'El campo es obligatorio' } })));

    fixture.componentInstance.guardar(TEXTO, '');

    expect(fixture.componentInstance.errores()['def-texto']).toBe('El campo es obligatorio');
  });

  it('un rechazo sin mensaje no deja al usuario sin explicación', async () => {
    await montar([TEXTO]);
    servicio.guardarValor.and.returnValue(throwError(() => ({ status: 500 })));

    fixture.componentInstance.guardar(TEXTO, 'algo');

    expect(fixture.componentInstance.errores()['def-texto']).toBeTruthy();
  });

  it('un guardado correcto deja el valor nuevo como el bueno al que revertir', async () => {
    await montar([TEXTO]);

    fixture.componentInstance.guardar(TEXTO, 'Globex');
    servicio.guardarValor.and.returnValue(throwError(() => ({ error: 'No vale' })));
    fixture.componentInstance.guardar({ ...TEXTO, valor: 'Globex' }, 'Initech');

    expect(fixture.componentInstance.campos()[0].valor).toBe('Globex');
  });

  describe('selección múltiple', () => {
    it('marca una opción añadiéndola a las que ya había', async () => {
      await montar([MULTIPLE]);

      fixture.componentInstance.alternarOpcion(MULTIPLE, 'Correo');

      expect(servicio.guardarValor).toHaveBeenCalledWith('def-multiple', 'tarea-1', 'Web\nCorreo');
    });

    it('desmarca la que ya estaba', async () => {
      await montar([MULTIPLE]);

      fixture.componentInstance.alternarOpcion(MULTIPLE, 'Web');

      // Sin ninguna marcada se manda nulo: una cadena vacía no es «ninguna opción», es basura.
      expect(servicio.guardarValor).toHaveBeenCalledWith('def-multiple', 'tarea-1', null);
    });

    it('sabe cuáles están marcadas', async () => {
      await montar([MULTIPLE]);
      const componente = fixture.componentInstance;

      expect(componente.estaMarcada(MULTIPLE, 'Web')).toBeTrue();
      expect(componente.estaMarcada(MULTIPLE, 'Correo')).toBeFalse();
    });
  });

  it('un tipo que esta versión no sabe pintar enseña el valor en crudo', async () => {
    const desconocido: CustomFieldValue = {
      definitionId: 'def-raro', nombre: 'Fórmula', tipo: 'Calculado',
      obligatorio: false, opciones: [], posicion: 0, valor: '42',
    };

    await montar([desconocido]);

    // Esconder el campo haría creer que el dato se ha perdido.
    expect(fixture.nativeElement.textContent).toContain('42');
  });
});
