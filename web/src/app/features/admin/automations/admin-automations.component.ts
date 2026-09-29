import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideZap, lucidePlus, lucideTrash2, lucideEdit3, lucideRefreshCw,
  lucideLoader2, lucideCircleAlert, lucideX,
} from '@ng-icons/lucide';
import {
  AutomationsService, OPERADOR_SIN_VALOR, automationLabel,
  type AccionDeRegla, type CondicionDeRegla, type ReglaDeAutomatizacion,
  type VocabularioDeAutomatizacion,
} from '../../../core/automations.service';
import { mensajeDeError } from '../../../shared/utils/mensaje-de-error';

/** Lo que el dominio acepta. Repetirlo evita un viaje al servidor para decir lo obvio. */
const LARGO_MAXIMO_DEL_NOMBRE = 100;
const MAXIMO_DE_CONDICIONES = 10;
const MAXIMO_DE_ACCIONES = 5;

/**
 * Administración de las automatizaciones.
 *
 * **El formulario se construye con el vocabulario que sirve el servidor**, no con listas
 * escritas aquí: una copia se desincroniza el día que se añada un disparador, y entonces esta
 * pantalla dejaría configurar algo que el servidor no entiende.
 *
 * La lista enseña cuántas veces se ha ejecutado cada regla. Es lo primero que se mira cuando
 * alguien dice «esta automatización no funciona»: separa «no salta» de «salta y hace otra cosa».
 */
@Component({
  selector: 'app-admin-automations',
  standalone: true,
  imports: [FormsModule, NgIconComponent],
  viewProviders: [provideIcons({
    lucideZap, lucidePlus, lucideTrash2, lucideEdit3, lucideRefreshCw,
    lucideLoader2, lucideCircleAlert, lucideX,
  })],
  templateUrl: './admin-automations.component.html',
})
export class AdminAutomationsComponent implements OnInit {
  private readonly servicio = inject(AutomationsService);

  readonly largoMaximoDelNombre = LARGO_MAXIMO_DEL_NOMBRE;
  readonly operadorSinValor = OPERADOR_SIN_VALOR;

  /** Para enseñar cada código del vocabulario en castellano. */
  readonly automationLabel = automationLabel;

  readonly vocabulario = signal<VocabularioDeAutomatizacion>({
    triggers: [], fields: [], operators: [], actions: [],
  });

  readonly reglas = signal<ReglaDeAutomatizacion[]>([]);
  readonly cargando = signal(false);
  readonly guardando = signal(false);
  readonly error = signal('');

  /** `null` si el formulario está cerrado, `''` si es una regla nueva, o el id que se edita. */
  readonly editando = signal<string | null>(null);
  readonly borrando = signal<string | null>(null);

  name = '';
  trigger = '';
  conditions: CondicionDeRegla[] = [];
  actions: AccionDeRegla[] = [];

  readonly esNueva = computed(() => this.editando() === '');

  ngOnInit(): void {
    this.servicio.vocabulario().subscribe({
      next: v => this.vocabulario.set(v),
      error: respuesta => this.error.set(
        mensajeDeError(respuesta, $localize`No se pudo cargar el vocabulario de automatizaciones`)),
    });

    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.error.set('');

    this.servicio.reglas().subscribe({
      next: reglas => {
        this.reglas.set(reglas ?? []);
        this.cargando.set(false);
      },
      error: respuesta => {
        this.error.set(mensajeDeError(respuesta, $localize`No se pudieron cargar las automatizaciones`));
        this.cargando.set(false);
      },
    });
  }

  nueva(): void {
    this.editando.set('');
    this.name = '';
    this.trigger = this.vocabulario().triggers[0] ?? '';
    this.conditions = [];
    // Una regla sin acciones no hace nada, así que el formulario empieza con una.
    this.actions = [this.accionEnBlanco()];
    this.error.set('');
  }

  editar(regla: ReglaDeAutomatizacion): void {
    this.editando.set(regla.id);
    this.name = regla.name;
    this.trigger = regla.trigger;
    this.conditions = regla.conditions.map(c => ({ ...c }));
    this.actions = regla.actions.map(a => ({ ...a }));
    this.error.set('');
  }

  cerrarFormulario(): void {
    this.editando.set(null);
    this.error.set('');
  }

  private accionEnBlanco(): AccionDeRegla {
    return { type: this.vocabulario().actions[0] ?? '', value: '' };
  }

  agregarCondicion(): void {
    if (this.conditions.length >= MAXIMO_DE_CONDICIONES) return;

    this.conditions = [...this.conditions, {
      field: this.vocabulario().fields[0] ?? '',
      operator: this.vocabulario().operators[0] ?? '',
      value: '',
    }];
  }

  quitarCondicion(indice: number): void {
    this.conditions = this.conditions.filter((_, i) => i !== indice);
  }

  agregarAccion(): void {
    if (this.actions.length >= MAXIMO_DE_ACCIONES) return;
    this.actions = [...this.actions, this.accionEnBlanco()];
  }

  quitarAccion(indice: number): void {
    this.actions = this.actions.filter((_, i) => i !== indice);
  }

  necesitaValor(condicion: CondicionDeRegla): boolean {
    return condicion.operator !== OPERADOR_SIN_VALOR;
  }

  /**
   * El motivo por el que no se puede guardar todavía, o cadena vacía si sí se puede.
   *
   * Es un getter y no un `computed`: lee campos atados con `ngModel`, que no son señales, y un
   * `computed` sobre eso se quedaría con el primer valor para siempre.
   */
  get impedimento(): string {
    const name = this.name.trim();

    if (!name) return $localize`La automatización necesita un nombre`;
    if (name.length > LARGO_MAXIMO_DEL_NOMBRE) {
      return $localize`El nombre no puede pasar de ${LARGO_MAXIMO_DEL_NOMBRE} caracteres`;
    }

    if (!this.trigger) return $localize`Hay que elegir cuándo se dispara`;

    // Una regla sin acciones se ejecutaría entera para no hacer nada.
    if (!this.actions.length) return $localize`La automatización necesita al menos una acción`;
    if (this.actions.some(a => !a.type || !a.value.trim())) {
      return $localize`Cada acción necesita un valor`;
    }

    if (this.conditions.some(c => this.necesitaValor(c) && !(c.value ?? '').trim())) {
      return $localize`Cada condición necesita un valor con el que comparar`;
    }

    return '';
  }

  guardar(): void {
    if (this.impedimento || this.guardando()) return;

    const id = this.editando();
    if (id === null) return;

    const regla = {
      name: this.name.trim(),
      trigger: this.trigger,
      conditions: this.conditions.map(c => ({
        field: c.field,
        operator: c.operator,
        // «Está vacío» no compara contra nada: mandar un valor sería ruido que el servidor tira.
        value: this.necesitaValor(c) ? (c.value ?? '').trim() : null,
      })),
      actions: this.actions.map(a => ({ type: a.type, value: a.value.trim() })),
    };

    this.guardando.set(true);
    this.error.set('');

    const peticion: Observable<unknown> = id === ''
      ? this.servicio.crear(regla)
      : this.servicio.actualizar(id, regla);

    peticion.subscribe({
      next: () => {
        this.guardando.set(false);
        this.cerrarFormulario();
        this.cargar();
      },
      error: respuesta => {
        this.guardando.set(false);
        this.error.set(mensajeDeError(respuesta, $localize`No se pudo guardar la automatización`));
      },
    });
  }

  /**
   * Apagar o encender una regla.
   *
   * Se pinta el cambio antes de tener respuesta y se revierte si el servidor lo rechaza: dejar
   * en pantalla una automatización apagada que sigue ejecutándose es la peor mentira posible en
   * esta pantalla.
   */
  alternarActiva(regla: ReglaDeAutomatizacion): void {
    const antes = regla.isActive;
    this.aplicarEnLista(regla.id, !antes);

    this.servicio.activar(regla.id, !antes).subscribe({
      error: respuesta => {
        this.aplicarEnLista(regla.id, antes);
        this.error.set(mensajeDeError(respuesta, $localize`No se pudo cambiar el estado de la automatización`));
      },
    });
  }

  private aplicarEnLista(id: string, isActive: boolean): void {
    this.reglas.update(reglas => reglas.map(r => r.id === id ? { ...r, isActive } : r));
  }

  borrar(regla: ReglaDeAutomatizacion): void {
    this.guardando.set(true);

    this.servicio.borrar(regla.id).subscribe({
      next: () => {
        this.guardando.set(false);
        this.borrando.set(null);
        this.cargar();
      },
      error: respuesta => {
        this.guardando.set(false);
        this.borrando.set(null);
        this.error.set(mensajeDeError(respuesta, $localize`No se pudo borrar la automatización`));
      },
    });
  }

  /** Un resumen legible de la regla, para no obligar a abrirla para saber qué hace. */
  resumenDe(regla: ReglaDeAutomatizacion): string {
    const actions = regla.actions.map(a => `${automationLabel(a.type)}: ${a.value}`).join(', ');

    if (!regla.conditions.length) return actions;

    const conditions = regla.conditions
      .map(c => {
        const head = `${automationLabel(c.field)} ${automationLabel(c.operator)}`;
        return c.operator === OPERADOR_SIN_VALOR ? head : `${head} ${c.value}`;
      })
      .join(' · ');

    return `${conditions} → ${actions}`;
  }
}
