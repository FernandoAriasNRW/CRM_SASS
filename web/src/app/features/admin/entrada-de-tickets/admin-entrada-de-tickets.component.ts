import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideKeyRound, lucidePlus, lucideCopy, lucideCheck, lucideLoader2, lucideCircleAlert, lucideBan,
} from '@ng-icons/lucide';
import { ApiService } from '../../../core/api.service';
import { mensajeDeError } from '../../../shared/utils/mensaje-de-error';
import {
  CAMPOS_OBLIGATORIOS, CAMPOS_OPCIONALES, CLAVE_DE_EJEMPLO, ejemploDeCurl, ejemploDeCurlConAdjuntos, ejemploDeFormulario,
} from './ejemplos-de-entrada';

export interface ClaveDeEntrada {
  id: string;
  nombre: string;
  inicio: string;
  creadaUtc: string;
  ultimoUsoUtc: string | null;
  revocadaUtc: string | null;
}

interface ClaveCreada {
  id: string;
  nombre: string;
  inicio: string;
  clave: string;
}

/** La ruta pública por la que entran los tickets. Coincide con `TicketingEndpoints`. */
const RUTA_DE_ENTRADA = '/entrada/tickets';

/**
 * Las claves con las que la web o el backend de un cliente abren tickets en la organización.
 *
 * **La clave se enseña una sola vez**, justo al crearla, con los ejemplos ya rellenos. El servidor
 * sólo guarda su hash; si se pierde, se revoca y se crea otra. Por eso el aviso está junto a la
 * clave y no en un texto de ayuda aparte que nadie lee a tiempo.
 *
 * Revocar se confirma en la propia fila, como en campos personalizados: el `confirm()` del
 * navegador no se traduce.
 */
@Component({
  selector: 'app-admin-entrada-de-tickets',
  standalone: true,
  imports: [FormsModule, NgIconComponent, DatePipe],
  viewProviders: [provideIcons({
    lucideKeyRound, lucidePlus, lucideCopy, lucideCheck, lucideLoader2, lucideCircleAlert, lucideBan,
  })],
  templateUrl: './admin-entrada-de-tickets.component.html',
})
export class AdminEntradaDeTicketsComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly claves = signal<ClaveDeEntrada[]>([]);
  readonly cargando = signal(true);
  readonly error = signal('');

  nombre = '';
  readonly creando = signal(false);
  readonly recienCreada = signal<ClaveCreada | null>(null);
  readonly aRevocar = signal<string | null>(null);
  readonly copiado = signal<string | null>(null);

  readonly url = this.api.urlDeLaApi(RUTA_DE_ENTRADA);

  /** Los ejemplos llevan la clave recién creada si la hay; si no, un marcador evidente. */
  private readonly claveParaEjemplos = computed(() => this.recienCreada()?.clave ?? CLAVE_DE_EJEMPLO);
  readonly formulario = computed(() => ejemploDeFormulario(this.url, this.claveParaEjemplos()));
  readonly curl = computed(() => ejemploDeCurl(this.url, this.claveParaEjemplos()));
  readonly curlConAdjuntos = computed(() => ejemploDeCurlConAdjuntos(this.url, this.claveParaEjemplos()));
  readonly obligatorios = CAMPOS_OBLIGATORIOS.join(', ');
  readonly opcionales = CAMPOS_OPCIONALES.join(', ');

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.api.get<ClaveDeEntrada[]>('/tickets/claves-de-entrada').subscribe({
      next: claves => { this.claves.set(claves); this.cargando.set(false); },
      error: err => {
        this.error.set(mensajeDeError(err, $localize`No se pudieron cargar las claves`));
        this.cargando.set(false);
      },
    });
  }

  crear(): void {
    const nombre = this.nombre.trim();
    if (!nombre) return;

    this.creando.set(true);
    this.error.set('');
    this.api.post<ClaveCreada>('/tickets/claves-de-entrada', { nombre }).subscribe({
      next: creada => {
        this.recienCreada.set(creada);
        this.nombre = '';
        this.creando.set(false);
        this.cargar();
      },
      error: err => {
        this.error.set(mensajeDeError(err, $localize`No se pudo crear la clave`));
        this.creando.set(false);
      },
    });
  }

  revocar(id: string): void {
    this.api.delete(`/tickets/claves-de-entrada/${id}`).subscribe({
      next: () => {
        this.aRevocar.set(null);
        if (this.recienCreada()?.id === id) this.recienCreada.set(null);
        this.cargar();
      },
      error: err => this.error.set(mensajeDeError(err, $localize`No se pudo revocar la clave`)),
    });
  }

  async copiar(que: string, texto: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(texto);
      this.copiado.set(que);
      setTimeout(() => this.copiado.set(null), 1500);
    } catch {
      this.error.set($localize`No se pudo copiar; selecciona el texto y cópialo a mano`);
    }
  }
}
