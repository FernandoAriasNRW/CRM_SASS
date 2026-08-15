import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideZap, lucidePlus, lucideTrash2, lucideEdit3, lucideRefreshCw,
  lucideLoader2, lucideCircleAlert, lucideX,
} from '@ng-icons/lucide';
import {
  AutomationsService, OPERADOR_SIN_VALOR,
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

  readonly vocabulario = signal<VocabularioDeAutomatizacion>({
    disparadores: [], campos: [], operadores: [], acciones: [],
  });

  readonly reglas = signal<ReglaDeAutomatizacion[]>([]);
  readonly cargando = signal(false);
  readonly guardando = signal(false);
  readonly error = signal('');

  /** `null` si el formulario está cerrado, `''` si es una regla nueva, o el id que se edita. */
  readonly editando = signal<string | null>(null);
  readonly borrando = signal<string | null>(null);

  nombre = '';
  disparador = '';
  condiciones: CondicionDeRegla[] = [];
  acciones: AccionDeRegla[] = [];

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
    this.nombre = '';
    this.disparador = this.vocabulario().disparadores[0] ?? '';
    this.condiciones = [];
    // Una regla sin acciones no hace nada, así que el formulario empieza con una.
    this.acciones = [this.accionEnBlanco()];
    this.error.set('');
  }

  editar(regla: ReglaDeAutomatizacion): void {
    this.editando.set(regla.id);
    this.nombre = regla.nombre;
    this.disparador = regla.disparador;
    this.condiciones = regla.condiciones.map(c => ({ ...c }));
    this.acciones = regla.acciones.map(a => ({ ...a }));
    this.error.set('');
  }

  cerrarFormulario(): void {
    this.editando.set(null);
    this.error.set('');
  }

  private accionEnBlanco(): AccionDeRegla {
    return { tipo: this.vocabulario().acciones[0] ?? '', valor: '' };
  }

  agregarCondicion(): void {
    if (this.condiciones.length >= MAXIMO_DE_CONDICIONES) return;

    this.condiciones = [...this.condiciones, {
      campo: this.vocabulario().campos[0] ?? '',
      operador: this.vocabulario().operadores[0] ?? '',
      valor: '',
    }];
  }

  quitarCondicion(indice: number): void {
    this.condiciones = this.condiciones.filter((_, i) => i !== indice);
  }

  agregarAccion(): void {
    if (this.acciones.length >= MAXIMO_DE_ACCIONES) return;
    this.acciones = [...this.acciones, this.accionEnBlanco()];
  }

  quitarAccion(indice: number): void {
    this.acciones = this.acciones.filter((_, i) => i !== indice);
  }

  necesitaValor(condicion: CondicionDeRegla): boolean {
    return condicion.operador !== OPERADOR_SIN_VALOR;
  }

  /**
   * El motivo por el que no se puede guardar todavía, o cadena vacía si sí se puede.
   *
   * Es un getter y no un `computed`: lee campos atados con `ngModel`, que no son señales, y un
   * `computed` sobre eso se quedaría con el primer valor para siempre.
   */
  get impedimento(): string {
    const nombre = this.nombre.trim();

    if (!nombre) return $localize`La automatización necesita un nombre`;
    if (nombre.length > LARGO_MAXIMO_DEL_NOMBRE) {
      return $localize`El nombre no puede pasar de ${LARGO_MAXIMO_DEL_NOMBRE} caracteres`;
    }

    if (!this.disparador) return $localize`Hay que elegir cuándo se dispara`;

    // Una regla sin acciones se ejecutaría entera para no hacer nada.
    if (!this.acciones.length) return $localize`La automatización necesita al menos una acción`;
    if (this.acciones.some(a => !a.tipo || !a.valor.trim())) {
      return $localize`Cada acción necesita un valor`;
    }

    if (this.condiciones.some(c => this.necesitaValor(c) && !(c.valor ?? '').trim())) {
      return $localize`Cada condición necesita un valor con el que comparar`;
    }

    return '';
  }

  guardar(): void {
    if (this.impedimento || this.guardando()) return;

    const id = this.editando();
    if (id === null) return;

    const regla = {
      nombre: this.nombre.trim(),
      disparador: this.disparador,
      condiciones: this.condiciones.map(c => ({
        campo: c.campo,
        operador: c.operador,
        // «Está vacío» no compara contra nada: mandar un valor sería ruido que el servidor tira.
        valor: this.necesitaValor(c) ? (c.valor ?? '').trim() : null,
      })),
      acciones: this.acciones.map(a => ({ tipo: a.tipo, valor: a.valor.trim() })),
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
    const antes = regla.activa;
    this.aplicarEnLista(regla.id, !antes);

    this.servicio.activar(regla.id, !antes).subscribe({
      error: respuesta => {
        this.aplicarEnLista(regla.id, antes);
        this.error.set(mensajeDeError(respuesta, $localize`No se pudo cambiar el estado de la automatización`));
      },
    });
  }

  private aplicarEnLista(id: string, activa: boolean): void {
    this.reglas.update(reglas => reglas.map(r => r.id === id ? { ...r, activa } : r));
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
    const acciones = regla.acciones.map(a => `${a.tipo}: ${a.valor}`).join(', ');

    if (!regla.condiciones.length) return acciones;

    const condiciones = regla.condiciones
      .map(c => c.operador === OPERADOR_SIN_VALOR ? `${c.campo} ${c.operador}` : `${c.campo} ${c.operador} ${c.valor}`)
      .join(' · ');

    return `${condiciones} → ${acciones}`;
  }
}
