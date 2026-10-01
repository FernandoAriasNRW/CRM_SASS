import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideListPlus, lucidePlus, lucideTrash2, lucideEdit3, lucideRefreshCw,
  lucideLoader2, lucideCircleAlert, lucideX,
} from '@ng-icons/lucide';
import {
  CustomFieldsService, TARGET_ENTITIES, isComputed, FIELD_TYPES, type CustomFieldDefinition,
} from '../../../core/custom-fields.service';
import { errorMessage } from '../../../shared/utils/error-message';

/** Lo que el dominio acepta. Repetirlo aquí evita un viaje al servidor para decir lo obvio. */
const MAX_NAME_LENGTH = 80;
const MAX_OPTIONS = 50;

/** Los tipos que se definen con una lista de opciones. Lo decide `TipoDeCampo.UsaOpciones`. */
const TYPES_WITH_OPTIONS = ['Select', 'MultiSelect'];

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
  private readonly service = inject(CustomFieldsService);

  readonly targetEntities = TARGET_ENTITIES;
  readonly fieldTypes = FIELD_TYPES;
  readonly maxNameLength = MAX_NAME_LENGTH;

  readonly entity = signal<string>(TARGET_ENTITIES[0].key);
  readonly definitions = signal<CustomFieldDefinition[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');

  /** `null` si el formulario está cerrado, `''` si es un campo nuevo, o el id que se edita. */
  readonly editing = signal<string | null>(null);
  readonly deleting = signal<string | null>(null);

  name = '';
  type: string = FIELD_TYPES[0].key;
  isRequired = false;
  /** Una opción por línea: es lo más rápido de escribir y de reordenar. */
  options = '';
  position = 0;
  /** La expresión de un campo calculado. */
  formula = '';

  readonly isNew = computed(() => this.editing() === '');

  // `tipo`, `nombre` y `opciones` son campos normales atados con ngModel, no señales, así que lo
  // que dependa de ellos tiene que ser un getter: un computed() no volvería a calcularse nunca.
  get usesOptions(): boolean { return TYPES_WITH_OPTIONS.includes(this.type); }
  get usaFormula(): boolean { return isComputed(this.type); }

  /**
   * Los campos que una fórmula puede usar: los numéricos y otros calculados. Se ofrecen para
   * insertarlos con un clic en vez de tener que teclear el nombre exacto entre corchetes, que
   * es donde se cometen las erratas.
   *
   * Se excluye el que se está editando: ofrecerlo sería invitar a escribir un ciclo que el
   * servidor va a rechazar.
   */
  get usableFields(): CustomFieldDefinition[] {
    const id = this.editing();
    return this.sorted().filter(d => (d.type === 'Number' || isComputed(d.type)) && d.id !== id);
  }

  insertReference(name: string): void {
    this.formula = `${this.formula}[${name}]`;
  }

  readonly sorted = computed(() =>
    [...this.definitions()].sort((a, b) => a.position - b.position || a.name.localeCompare(b.name))
  );

  ngOnInit(): void {
    this.load();
  }

  typeLabel(type: string): string {
    return FIELD_TYPES.find(t => t.key === type)?.label ?? type;
  }

  changeEntity(entity: string): void {
    if (entity === this.entity()) return;
    this.entity.set(entity);
    this.closeForm();
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');

    this.service.loadDefinitions(this.entity()).subscribe({
      next: definitions => {
        this.definitions.set(definitions ?? []);
        this.loading.set(false);
      },
      error: response => {
        this.error.set(errorMessage(response, $localize`No se pudieron cargar los campos`));
        this.loading.set(false);
      },
    });
  }

  startNew(): void {
    this.editing.set('');
    this.name = '';
    this.type = FIELD_TYPES[0].key;
    this.isRequired = false;
    this.options = '';
    this.formula = '';
    // Detrás del último, que es donde se espera que aparezca un campo recién creado.
    this.position = this.sorted().length
      ? Math.max(...this.sorted().map(d => d.position)) + 1
      : 0;
    this.error.set('');
  }

  edit(definition: CustomFieldDefinition): void {
    this.editing.set(definition.id);
    this.name = definition.name;
    this.type = definition.type;
    this.isRequired = definition.isRequired;
    this.options = (definition.options ?? []).join('\n');
    this.position = definition.position;
    this.error.set('');
  }

  closeForm(): void {
    this.editing.set(null);
    this.error.set('');
  }

  /** Lo que se manda al servidor: sin espacios, sin vacías y sin repetidas, igual que el dominio. */
  private cleanOptions(): string[] {
    if (!this.usesOptions) return [];

    const list = this.options
      .split('\n')
      .map(o => o.trim())
      .filter(o => o.length > 0);

    return [...new Set(list)];
  }

  /**
   * El motivo por el que no se puede guardar todavía, o cadena vacía si sí se puede.
   *
   * Repite las reglas del dominio a propósito, para no gastar un viaje al servidor en decir que
   * falta el nombre. El servidor sigue siendo el que manda: si las dos discrepan, gana su error.
   */
  get blocker(): string {
    const name = this.name.trim();

    if (!name) return $localize`El campo necesita un nombre`;
    if (name.length > MAX_NAME_LENGTH) {
      return $localize`El nombre del campo no puede pasar de ${MAX_NAME_LENGTH} caracteres`;
    }

    if (this.usaFormula && !this.formula.trim()) {
      return $localize`Un campo calculado necesita una fórmula`;
    }

    if (this.usesOptions) {
      const options = this.cleanOptions();
      if (!options.length) return $localize`Un campo de selección necesita al menos una opción`;
      if (options.length > MAX_OPTIONS) {
        return $localize`Un campo de selección no puede tener más de ${MAX_OPTIONS} opciones`;
      }
    }

    return '';
  }

  save(): void {
    if (this.blocker || this.saving()) return;

    const id = this.editing();
    if (id === null) return;

    const common = {
      name: this.name.trim(),
      isRequired: this.isRequired,
      options: this.cleanOptions(),
      position: this.position,
      // Sólo si aplica: mandarla en un campo de texto la guardaría para nada y confundiría a
      // quien leyera la definición después.
      formula: this.usaFormula ? this.formula.trim() : null,
    };

    this.saving.set(true);
    this.error.set('');

    // El alta devuelve la definición creada y la edición no devuelve nada; aquí no se usa ninguna
    // de las dos, así que el tipo común basta y evita que la unión deje de ser invocable.
    const request: Observable<unknown> = id === ''
      ? this.service.define({ ...common, type: this.type, targetEntity: this.entity() })
      : this.service.update(id, this.entity(), common);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.closeForm();
        this.load();
      },
      error: response => {
        this.saving.set(false);
        this.error.set(errorMessage(response, $localize`No se pudo guardar el campo`));
      },
    });
  }

  remove(definition: CustomFieldDefinition): void {
    this.saving.set(true);
    this.error.set('');

    this.service.remove(definition.id, this.entity()).subscribe({
      next: () => {
        this.saving.set(false);
        this.deleting.set(null);
        this.load();
      },
      error: response => {
        this.saving.set(false);
        this.deleting.set(null);
        this.error.set(errorMessage(response, $localize`No se pudo borrar el campo`));
      },
    });
  }
}
