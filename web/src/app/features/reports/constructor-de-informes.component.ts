import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { ButtonComponent } from '../../shared/ui/button.component';
import { ToastService } from '../../shared/services/toast.service';
import { mensajeDeError } from '../../shared/utils/mensaje-de-error';

/** El catálogo tal como lo sirve el servidor. La pantalla no escribe ninguna de estas listas. */
export interface Catalogo {
  origenes: Origen[];
  operadores: Operador[];
  formas: Opcion[];
  granularidades: Opcion[];
}

export interface Origen { clave: string; nombre: string; campos: Campo[]; medidas: Opcion[]; }
export interface Campo {
  clave: string;
  nombre: string;
  tipo: string;
  /** Los operadores de **este** campo, resueltos por el servidor. Ver `operadoresPara`. */
  operadores: string[];
  /** Los valores admitidos si es una lista cerrada; vacío si admite texto libre. */
  valores: string[];
}
export interface Operador { clave: string; nombre: string; tipos: string[]; necesitaValor: boolean; }
export interface Opcion { clave: string; nombre: string; }

export interface Filtro { campo: string; operador: string; valor: string | null; }

export interface Definicion {
  origen: string;
  agrupacion: string;
  medida: string;
  forma: string;
  filtros?: Filtro[];
  granularidad?: string | null;
  maximoDeGrupos?: number | null;
}

interface VistaPrevia {
  titulo: string;
  subtitulo: string | null;
  columnas: string[];
  filas: string[][];
  totalDeFilas: number;
}

/**
 * El constructor de informes: origen, filtros, agrupación, medida y forma.
 *
 * **Todo lo que ofrece sale del catálogo del servidor**, ninguna lista está escrita aquí. Es la
 * lección que este módulo ya dio dos veces: el desplegable de tipos de informe ofrecía dos que el
 * enum del servidor no conocía, y el menú de navegación ofrecía un filtro que nadie leía. En los
 * dos casos el fallo no se veía hasta que alguien lo pulsaba.
 *
 * En un constructor eso sería mucho peor, porque **la definición se guarda**: un campo que la
 * pantalla ofrezca y el motor no sepa traducir produce un informe que falla al exportarse, días
 * después, cuando quien lo construyó ya no está delante.
 *
 * Por eso también hay vista previa: se ve el resultado antes de guardar.
 */
@Component({
  selector: 'app-constructor-de-informes',
  standalone: true,
  imports: [FormsModule, ButtonComponent],
  template: `
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" (click)="cerrar()">
      <div class="bg-card rounded-lg shadow-xl w-full max-w-4xl max-h-[90vh] overflow-y-auto"
           (click)="$event.stopPropagation()">

        <div class="px-5 py-4 border-b border-border flex items-center justify-between">
          <h2 class="text-lg font-semibold" i18n>Constructor de informes</h2>
          <button type="button" class="text-muted-foreground hover:text-foreground" (click)="cerrar()"
                  i18n-aria-label aria-label="Cerrar">✕</button>
        </div>

        @if (cargando()) {
          <p class="p-6 text-sm text-muted-foreground" i18n>Cargando el catálogo…</p>
        } @else if (!catalogo()) {
          <p class="p-6 text-sm text-destructive" i18n>No se pudo cargar el catálogo de informes.</p>
        } @else {
          <div class="p-5 space-y-5">

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label class="text-sm font-medium block mb-1" i18n>Datos de</label>
                <select class="w-full border border-border rounded-md px-3 py-2 text-sm bg-background"
                        [ngModel]="origen()" (ngModelChange)="cambiarOrigen($event)" name="origen">
                  @for (o of catalogo()!.origenes; track o.clave) {
                    <option [value]="o.clave">{{ o.nombre }}</option>
                  }
                </select>
              </div>

              <div>
                <label class="text-sm font-medium block mb-1" i18n>Agrupado por</label>
                <select class="w-full border border-border rounded-md px-3 py-2 text-sm bg-background"
                        [(ngModel)]="agrupacion" name="agrupacion">
                  @for (c of camposDelOrigen(); track c.clave) {
                    <option [value]="c.clave">{{ c.nombre }}</option>
                  }
                </select>
              </div>

              <!-- La granularidad sólo aparece si se agrupa por una fecha: en otro caso el
                   servidor la rechaza, y ofrecerla sería prometer algo que no se acepta. -->
              @if (agrupaPorFecha()) {
                <div>
                  <label class="text-sm font-medium block mb-1" i18n>Agrupar la fecha</label>
                  <select class="w-full border border-border rounded-md px-3 py-2 text-sm bg-background"
                          [(ngModel)]="granularidad" name="granularidad">
                    @for (g of catalogo()!.granularidades; track g.clave) {
                      <option [value]="g.clave">{{ g.nombre }}</option>
                    }
                  </select>
                </div>
              }

              <div>
                <label class="text-sm font-medium block mb-1" i18n>Midiendo</label>
                <select class="w-full border border-border rounded-md px-3 py-2 text-sm bg-background"
                        [(ngModel)]="medida" name="medida">
                  @for (m of medidasDelOrigen(); track m.clave) {
                    <option [value]="m.clave">{{ m.nombre }}</option>
                  }
                </select>
              </div>

              <div>
                <label class="text-sm font-medium block mb-1" i18n>Pintado como</label>
                <select class="w-full border border-border rounded-md px-3 py-2 text-sm bg-background"
                        [(ngModel)]="forma" name="forma">
                  @for (f of catalogo()!.formas; track f.clave) {
                    <option [value]="f.clave">{{ f.nombre }}</option>
                  }
                </select>
              </div>
            </div>

            <div>
              <div class="flex items-center justify-between mb-2">
                <label class="text-sm font-medium" i18n>Filtros</label>
                <button uiButton variant="outline" size="sm" type="button" (click)="anadirFiltro()" i18n>
                  Añadir filtro
                </button>
              </div>

              @if (filtros().length === 0) {
                <p class="text-xs text-muted-foreground" i18n>Sin filtros: entra todo.</p>
              }

              @for (f of filtros(); track $index) {
                <div class="flex items-center gap-2 mb-2">
                  <select class="flex-1 border border-border rounded-md px-2 py-1.5 text-sm bg-background"
                          [ngModel]="f.campo" (ngModelChange)="cambiarCampoDeFiltro($index, $event)"
                          [name]="'filtro-campo-' + $index">
                    @for (c of camposDelOrigen(); track c.clave) {
                      <option [value]="c.clave">{{ c.nombre }}</option>
                    }
                  </select>

                  <!-- Sólo los operadores que valen para el tipo del campo elegido. «Mayor que»
                       sobre un estado no significa nada, y el servidor lo rechaza. -->
                  <select class="flex-1 border border-border rounded-md px-2 py-1.5 text-sm bg-background"
                          [(ngModel)]="f.operador" [name]="'filtro-op-' + $index">
                    @for (o of operadoresPara(f.campo); track o.clave) {
                      <option [value]="o.clave">{{ o.nombre }}</option>
                    }
                  </select>

                  @if (necesitaValor(f.operador)) {
                    <!--
                      Si el campo es una lista cerrada, se elige; si no, se escribe.

                      Escribir «Open» a mano es cómo se acaba filtrando por un valor que no
                      existe, y el resultado es un informe vacío que parece un informe sin datos.
                      Los valores los sirve el servidor, así que un estado nuevo aparece solo.
                    -->
                    @if (valoresDe(f.campo); as valores) {
                      @if (valores.length > 0) {
                        <select class="flex-1 border border-border rounded-md px-2 py-1.5 text-sm bg-background"
                                [(ngModel)]="f.valor" [name]="'filtro-valor-' + $index">
                          @for (v of valores; track v) {
                            <option [value]="v">{{ v }}</option>
                          }
                        </select>
                      } @else {
                        <input class="flex-1 border border-border rounded-md px-2 py-1.5 text-sm bg-background"
                               [(ngModel)]="f.valor" [name]="'filtro-valor-' + $index"
                               i18n-placeholder placeholder="Valor" />
                      }
                    }
                  }

                  <button type="button" class="text-muted-foreground hover:text-destructive px-2"
                          (click)="quitarFiltro($index)" i18n-aria-label aria-label="Quitar filtro">✕</button>
                </div>
              }
            </div>

            @if (error()) {
              <!-- El mensaje viene del servidor y dice qué pieza falla; se enseña entero. -->
              <p class="text-sm text-destructive border border-destructive/30 rounded-md px-3 py-2">
                {{ error() }}
              </p>
            }

            @if (previa(); as p) {
              <div class="border border-border rounded-md">
                <div class="px-3 py-2 border-b border-border">
                  <p class="text-sm font-medium">{{ p.titulo }}</p>
                  @if (p.subtitulo) { <p class="text-xs text-muted-foreground">{{ p.subtitulo }}</p> }
                </div>

                @if (p.filas.length === 0) {
                  <p class="px-3 py-4 text-sm text-muted-foreground" i18n>
                    Con estos filtros no hay datos. No es un error: es lo que hay.
                  </p>
                } @else {
                  <table class="w-full text-sm">
                    <thead>
                      <tr class="border-b border-border">
                        @for (c of p.columnas; track c) {
                          <th class="text-left px-3 py-2 font-medium">{{ c }}</th>
                        }
                      </tr>
                    </thead>
                    <tbody>
                      @for (fila of p.filas; track $index) {
                        <tr class="border-b border-border/50">
                          @for (celda of fila; track $index) {
                            <td class="px-3 py-1.5">{{ celda }}</td>
                          }
                        </tr>
                      }
                    </tbody>
                  </table>

                  @if (p.totalDeFilas > p.filas.length) {
                    <p class="px-3 py-2 text-xs text-muted-foreground">
                      Se enseñan {{ p.filas.length }} de {{ p.totalDeFilas }} filas.
                    </p>
                  }
                }
              </div>
            }
          </div>

          <div class="px-5 py-4 border-t border-border flex items-center justify-end gap-2">
            <button uiButton variant="outline" type="button" (click)="cerrar()" i18n>Cancelar</button>
            <button uiButton variant="outline" type="button" [disabled]="trabajando()" (click)="verPrevia()" i18n>
              Ver resultado
            </button>
            <button uiButton type="button" [disabled]="trabajando()" (click)="guardar()" i18n>
              Guardar
            </button>
          </div>
        }
      </div>
    </div>
  `
})
export class ConstructorDeInformesComponent {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  /** El informe cuya definición se está construyendo. */
  readonly reportId = input.required<string>();
  readonly titulo = input<string>('Informe');

  readonly cerrado = output<void>();
  readonly guardado = output<void>();

  readonly catalogo = signal<Catalogo | null>(null);
  readonly cargando = signal(true);
  readonly trabajando = signal(false);
  readonly error = signal('');
  readonly previa = signal<VistaPrevia | null>(null);

  readonly origen = signal('');
  agrupacion = '';
  medida = '';
  forma = 'barras';
  granularidad: string | null = 'mes';

  readonly filtros = signal<Filtro[]>([]);

  constructor() {
    void this.cargar();
  }

  private async cargar(): Promise<void> {
    try {
      const catalogo = await firstValueFrom(this.api.get<Catalogo>('/reports/catalogo'));
      this.catalogo.set(catalogo);

      // Si el informe ya tenía definición se reabre con ella; si no, se empieza por el primer
      // origen. `sinAviso` porque un informe sin definición todavía es lo normal, no un error
      // que haya que anunciar.
      const guardada = await firstValueFrom(
        this.api.get<Definicion>(`/reports/${this.reportId()}/definicion`, undefined, { sinAviso: true })
      ).catch(() => null);

      if (guardada) this.aplicar(guardada);
      else this.cambiarOrigen(catalogo.origenes[0]?.clave ?? '');
    } catch (e) {
      this.error.set(mensajeDeError(e, $localize`No se pudo cargar el catálogo.`));
    } finally {
      this.cargando.set(false);
    }
  }

  private aplicar(d: Definicion): void {
    this.origen.set(d.origen);
    this.agrupacion = d.agrupacion;
    this.medida = d.medida;
    this.forma = d.forma;
    this.granularidad = d.granularidad ?? 'mes';
    this.filtros.set([...(d.filtros ?? [])]);
  }

  readonly origenActual = computed(() =>
    this.catalogo()?.origenes.find(o => o.clave === this.origen()) ?? null);

  readonly camposDelOrigen = computed(() => this.origenActual()?.campos ?? []);
  readonly medidasDelOrigen = computed(() => this.origenActual()?.medidas ?? []);

  readonly agrupaPorFecha = computed(() =>
    this.camposDelOrigen().find(c => c.clave === this.agrupacion)?.tipo === 'Fecha');

  /**
   * Cambiar de origen limpia lo que dependía del anterior.
   *
   * Sin esto quedaría una agrupación de tickets sobre un informe de tareas: el servidor lo
   * rechaza, pero la pantalla habría dejado ver una combinación imposible como si valiera.
   */
  cambiarOrigen(clave: string): void {
    this.origen.set(clave);

    const origen = this.origenActual();
    this.agrupacion = origen?.campos[0]?.clave ?? '';
    this.medida = origen?.medidas[0]?.clave ?? 'conteo';
    this.filtros.set([]);
    this.previa.set(null);
    this.error.set('');
  }

  /**
   * Los operadores que valen para un campo.
   *
   * Se usa la lista que el servidor manda **por campo**, no se filtra por tipo aquí. El tipo no
   * basta: «está vacío» vale sobre la fecha de resolución de un ticket —puede faltar— y no sobre
   * el vencimiento de una tarea, que siempre tiene. Filtrando por tipo, la pantalla ofrecía una
   * combinación que el servidor rechaza.
   */
  operadoresPara(claveDeCampo: string): Operador[] {
    const campo = this.camposDelOrigen().find(c => c.clave === claveDeCampo);
    if (!campo) return [];

    return this.catalogo()?.operadores.filter(o => campo.operadores.includes(o.clave)) ?? [];
  }

  /** Los valores admitidos de un campo, o lista vacía si admite texto libre. */
  valoresDe(claveDeCampo: string): string[] {
    return this.camposDelOrigen().find(c => c.clave === claveDeCampo)?.valores ?? [];
  }

  necesitaValor(claveDeOperador: string): boolean {
    return this.catalogo()?.operadores.find(o => o.clave === claveDeOperador)?.necesitaValor ?? true;
  }

  anadirFiltro(): void {
    const campo = this.camposDelOrigen()[0];
    if (!campo) return;

    const operador = this.operadoresPara(campo.clave)[0];

    this.filtros.update(f => [...f, {
      campo: campo.clave,
      operador: operador?.clave ?? 'es',
      // Si el campo tiene lista, se empieza por su primer valor: un desplegable que arranca
      // vacío deja mandar un filtro sin valor sin que se note.
      valor: campo.valores[0] ?? ''
    }]);
  }

  /** Al cambiar el campo, el operador puede dejar de valer para su tipo: se reajusta. */
  cambiarCampoDeFiltro(indice: number, clave: string): void {
    this.filtros.update(filtros => filtros.map((f, i) => {
      if (i !== indice) return f;

      const operadores = this.operadoresPara(clave);
      const sigueValiendo = operadores.some(o => o.clave === f.operador);

      // El valor del campo anterior casi nunca vale para el nuevo, y si el nuevo es una lista
      // cerrada hay que empezar por uno de los suyos.
      const valores = this.valoresDe(clave);

      return {
        campo: clave,
        operador: sigueValiendo ? f.operador : operadores[0]?.clave ?? 'es',
        valor: valores.length > 0 ? valores[0] : ''
      };
    }));
  }

  quitarFiltro(indice: number): void {
    this.filtros.update(f => f.filter((_, i) => i !== indice));
  }

  private definicion(): Definicion {
    return {
      origen: this.origen(),
      agrupacion: this.agrupacion,
      medida: this.medida,
      forma: this.forma,
      filtros: this.filtros(),
      granularidad: this.agrupaPorFecha() ? this.granularidad : null
    };
  }

  async verPrevia(): Promise<void> {
    this.trabajando.set(true);
    this.error.set('');

    try {
      const previa = await firstValueFrom(this.api.post<VistaPrevia>(
        '/reports/vista-previa',
        { definicion: this.definicion(), titulo: this.titulo() },
        { sinAviso: true }));

      this.previa.set(previa);
    } catch (e) {
      // El servidor dice qué pieza no encaja. Se enseña dentro del constructor, junto a los
      // desplegables, en vez de como aviso flotante: aquí es donde hay que corregirlo.
      this.previa.set(null);
      this.error.set(mensajeDeError(e, $localize`No se pudo calcular el resultado.`));
    } finally {
      this.trabajando.set(false);
    }
  }

  async guardar(): Promise<void> {
    this.trabajando.set(true);
    this.error.set('');

    try {
      await firstValueFrom(this.api.put(
        `/reports/${this.reportId()}/definicion`, this.definicion(), { sinAviso: true }));

      this.toast.success($localize`Informe guardado.`);
      this.guardado.emit();
      this.cerrado.emit();
    } catch (e) {
      this.error.set(mensajeDeError(e, $localize`No se pudo guardar el informe.`));
    } finally {
      this.trabajando.set(false);
    }
  }

  cerrar(): void {
    this.cerrado.emit();
  }
}
