import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideListPlus, lucidePlus, lucideTrash2, lucideEdit3, lucideRefreshCw,
  lucideLoader2, lucideCircleAlert, lucideX,
} from '@ng-icons/lucide';
import {
  CustomFieldsService, ENTIDADES, TIPOS_DE_CAMPO, mensajeDeError,
  type CustomFieldDefinition,
} from '../../../core/custom-fields.service';

/** Lo que el dominio acepta. Repetirlo aquí evita un viaje al servidor para decir lo obvio. */
const LARGO_MAXIMO_DEL_NOMBRE = 80;
const MAXIMO_DE_OPCIONES = 50;

/** Los tipos que se definen con una lista de opciones. Lo decide `TipoDeCampo.UsaOpciones`. */
const TIPOS_CON_OPCIONES = ['Seleccion', 'SeleccionMultiple'];

/**
 * Administración de las definiciones de campos personalizados.
 *
 * **El tipo y la entidad no se pueden cambiar al editar**, y el formulario los bloquea en lugar
 * de dejar intentarlo y que el servidor lo rechace: pasar un campo de texto a número dejaría sin
 * validez todos los valores ya guardados. Para eso se borra y se crea otro, que además deja claro
 * que los datos viejos se pierden.
 *
 * El borrado se confirma en la propia fila y no con un `confirm()` del navegador —como hacen
 * usuarios y equipos—: el diálogo nativo no se traduce y obliga a las pruebas de extremo a extremo
 * a interceptar diálogos para llegar a lo que quieren comprobar.
 */
@Component({
  selector: 'app-admin-custom-fields',
  standalone: true,
  imports: [FormsModule, NgIconComponent],
  viewProviders: [provideIcons({
    lucideListPlus, lucidePlus, lucideTrash2, lucideEdit3, lucideRefreshCw,
    lucideLoader2, lucideCircleAlert, lucideX,
  })],
  templateUrl: './admin-custom-fields.component.html',
})
export class AdminCustomFieldsComponent implements OnInit {
  private readonly servicio = inject(CustomFieldsService);

  readonly entidades = ENTIDADES;
  readonly tipos = TIPOS_DE_CAMPO;
  readonly largoMaximoDelNombre = LARGO_MAXIMO_DEL_NOMBRE;

  readonly entidad = signal<string>(ENTIDADES[0].key);
  readonly definiciones = signal<CustomFieldDefinition[]>([]);
  readonly cargando = signal(false);
  readonly guardando = signal(false);
  readonly error = signal('');

  /** `null` si el formulario está cerrado, `''` si es un campo nuevo, o el id que se edita. */
  readonly editando = signal<string | null>(null);
  readonly borrando = signal<string | null>(null);

  nombre = '';
  tipo: string = TIPOS_DE_CAMPO[0].key;
  obligatorio = false;
  /** Una opción por línea: es lo más rápido de escribir y de reordenar. */
  opciones = '';
  posicion = 0;

  readonly esNuevo = computed(() => this.editando() === '');

  // `tipo`, `nombre` y `opciones` son campos normales atados con ngModel, no señales, así que lo
  // que dependa de ellos tiene que ser un getter: un computed() no volvería a calcularse nunca.
  get usaOpciones(): boolean { return TIPOS_CON_OPCIONES.includes(this.tipo); }

  readonly ordenadas = computed(() =>
    [...this.definiciones()].sort((a, b) => a.posicion - b.posicion || a.nombre.localeCompare(b.nombre))
  );

  ngOnInit(): void {
    this.cargar();
  }

  etiquetaDelTipo(tipo: string): string {
    return TIPOS_DE_CAMPO.find(t => t.key === tipo)?.label ?? tipo;
  }

  cambiarEntidad(entidad: string): void {
    if (entidad === this.entidad()) return;
    this.entidad.set(entidad);
    this.cerrarFormulario();
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.error.set('');

    this.servicio.cargarDefiniciones(this.entidad()).subscribe({
      next: definiciones => {
        this.definiciones.set(definiciones ?? []);
        this.cargando.set(false);
      },
      error: respuesta => {
        this.error.set(mensajeDeError(respuesta, $localize`No se pudieron cargar los campos`));
        this.cargando.set(false);
      },
    });
  }

  nuevo(): void {
    this.editando.set('');
    this.nombre = '';
    this.tipo = TIPOS_DE_CAMPO[0].key;
    this.obligatorio = false;
    this.opciones = '';
    // Detrás del último, que es donde se espera que aparezca un campo recién creado.
    this.posicion = this.ordenadas().length
      ? Math.max(...this.ordenadas().map(d => d.posicion)) + 1
      : 0;
    this.error.set('');
  }

  editar(definicion: CustomFieldDefinition): void {
    this.editando.set(definicion.id);
    this.nombre = definicion.nombre;
    this.tipo = definicion.tipo;
    this.obligatorio = definicion.obligatorio;
    this.opciones = (definicion.opciones ?? []).join('\n');
    this.posicion = definicion.posicion;
    this.error.set('');
  }

  cerrarFormulario(): void {
    this.editando.set(null);
    this.error.set('');
  }

  /** Lo que se manda al servidor: sin espacios, sin vacías y sin repetidas, igual que el dominio. */
  private opcionesLimpias(): string[] {
    if (!this.usaOpciones) return [];

    const lista = this.opciones
      .split('\n')
      .map(o => o.trim())
      .filter(o => o.length > 0);

    return [...new Set(lista)];
  }

  /**
   * El motivo por el que no se puede guardar todavía, o cadena vacía si sí se puede.
   *
   * Repite las reglas del dominio a propósito, para no gastar un viaje al servidor en decir que
   * falta el nombre. El servidor sigue siendo el que manda: si las dos discrepan, gana su error.
   */
  get impedimento(): string {
    const nombre = this.nombre.trim();

    if (!nombre) return $localize`El campo necesita un nombre`;
    if (nombre.length > LARGO_MAXIMO_DEL_NOMBRE) {
      return $localize`El nombre del campo no puede pasar de ${LARGO_MAXIMO_DEL_NOMBRE} caracteres`;
    }

    if (this.usaOpciones) {
      const opciones = this.opcionesLimpias();
      if (!opciones.length) return $localize`Un campo de selección necesita al menos una opción`;
      if (opciones.length > MAXIMO_DE_OPCIONES) {
        return $localize`Un campo de selección no puede tener más de ${MAXIMO_DE_OPCIONES} opciones`;
      }
    }

    return '';
  }

  guardar(): void {
    if (this.impedimento || this.guardando()) return;

    const id = this.editando();
    if (id === null) return;

    const comun = {
      nombre: this.nombre.trim(),
      obligatorio: this.obligatorio,
      opciones: this.opcionesLimpias(),
      posicion: this.posicion,
    };

    this.guardando.set(true);
    this.error.set('');

    // El alta devuelve la definición creada y la edición no devuelve nada; aquí no se usa ninguna
    // de las dos, así que el tipo común basta y evita que la unión deje de ser invocable.
    const peticion: Observable<unknown> = id === ''
      ? this.servicio.definir({ ...comun, tipo: this.tipo, entidadDestino: this.entidad() })
      : this.servicio.actualizar(id, this.entidad(), comun);

    peticion.subscribe({
      next: () => {
        this.guardando.set(false);
        this.cerrarFormulario();
        this.cargar();
      },
      error: respuesta => {
        this.guardando.set(false);
        this.error.set(mensajeDeError(respuesta, $localize`No se pudo guardar el campo`));
      },
    });
  }

  borrar(definicion: CustomFieldDefinition): void {
    this.guardando.set(true);
    this.error.set('');

    this.servicio.borrar(definicion.id, this.entidad()).subscribe({
      next: () => {
        this.guardando.set(false);
        this.borrando.set(null);
        this.cargar();
      },
      error: respuesta => {
        this.guardando.set(false);
        this.borrando.set(null);
        this.error.set(mensajeDeError(respuesta, $localize`No se pudo borrar el campo`));
      },
    });
  }
}
