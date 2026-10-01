import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { ButtonComponent } from '../../shared/ui/button.component';
import { ToastService } from '../../shared/services/toast.service';
import { errorMessage } from '../../shared/utils/error-message';

/** El catálogo tal como lo sirve el servidor. La pantalla no escribe ninguna de estas listas. */
export interface ReportCatalog {
  dataSources: CatalogDataSource[];
  operators: CatalogOperator[];
  visualizations: CatalogOption[];
  granularities: CatalogOption[];
}

export interface CatalogDataSource { key: string; name: string; fields: CatalogField[]; measures: CatalogOption[]; }
export interface CatalogField {
  key: string;
  name: string;
  type: string;
  /** Los operadores de **este** campo, resueltos por el servidor. Ver `operatorsFor`. */
  operators: string[];
  /** Los valores admitidos si es una lista cerrada; vacío si admite texto libre. */
  values: string[];
}
export interface CatalogOperator { key: string; name: string; types: string[]; needsValue: boolean; }
export interface CatalogOption { key: string; name: string; }

export interface ReportFilter { field: string; operator: string; value: string | null; }

export interface ReportDefinition {
  dataSource: string;
  groupBy: string;
  measure: string;
  visualization: string;
  filters?: ReportFilter[];
  granularity?: string | null;
  maxGroups?: number | null;
}

interface ReportPreview {
  title: string;
  subtitle: string | null;
  columns: string[];
  rows: string[][];
  totalRows: number;
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
  selector: 'app-report-builder',
  standalone: true,
  imports: [FormsModule, ButtonComponent],
  template: `
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" (click)="close()">
      <div class="bg-card rounded-lg shadow-xl w-full max-w-4xl max-h-[90vh] overflow-y-auto"
           (click)="$event.stopPropagation()">

        <div class="px-5 py-4 border-b border-border flex items-center justify-between">
          <h2 class="text-lg font-semibold" i18n>Constructor de informes</h2>
          <button type="button" class="text-muted-foreground hover:text-foreground" (click)="close()"
                  i18n-aria-label aria-label="Cerrar">✕</button>
        </div>

        @if (loading()) {
          <p class="p-6 text-sm text-muted-foreground" i18n>Cargando el catálogo…</p>
        } @else if (!catalog()) {
          <p class="p-6 text-sm text-destructive" i18n>No se pudo cargar el catálogo de informes.</p>
        } @else {
          <div class="p-5 space-y-5">

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label class="text-sm font-medium block mb-1" i18n>Datos de</label>
                <select class="w-full border border-border rounded-md px-3 py-2 text-sm bg-background"
                        [ngModel]="dataSource()" (ngModelChange)="changeDataSource($event)" name="dataSource">
                  @for (o of catalog()!.dataSources; track o.key) {
                    <option [value]="o.key">{{ o.name }}</option>
                  }
                </select>
              </div>

              <div>
                <label class="text-sm font-medium block mb-1" i18n>Agrupado por</label>
                <select class="w-full border border-border rounded-md px-3 py-2 text-sm bg-background"
                        [(ngModel)]="groupBy" name="groupBy">
                  @for (c of sourceFields(); track c.key) {
                    <option [value]="c.key">{{ c.name }}</option>
                  }
                </select>
              </div>

              <!-- La granularidad sólo aparece si se agrupa por una fecha: en otro caso el
                   servidor la rechaza, y ofrecerla sería prometer algo que no se acepta. -->
              @if (groupsByDate()) {
                <div>
                  <label class="text-sm font-medium block mb-1" i18n>Agrupar la fecha</label>
                  <select class="w-full border border-border rounded-md px-3 py-2 text-sm bg-background"
                          [(ngModel)]="granularity" name="granularity">
                    @for (g of catalog()!.granularities; track g.key) {
                      <option [value]="g.key">{{ g.name }}</option>
                    }
                  </select>
                </div>
              }

              <div>
                <label class="text-sm font-medium block mb-1" i18n>Midiendo</label>
                <select class="w-full border border-border rounded-md px-3 py-2 text-sm bg-background"
                        [(ngModel)]="measure" name="measure">
                  @for (m of sourceMeasures(); track m.key) {
                    <option [value]="m.key">{{ m.name }}</option>
                  }
                </select>
              </div>

              <div>
                <label class="text-sm font-medium block mb-1" i18n>Pintado como</label>
                <select class="w-full border border-border rounded-md px-3 py-2 text-sm bg-background"
                        [(ngModel)]="visualization" name="visualization">
                  @for (f of catalog()!.visualizations; track f.key) {
                    <option [value]="f.key">{{ f.name }}</option>
                  }
                </select>
              </div>
            </div>

            <div>
              <div class="flex items-center justify-between mb-2">
                <label class="text-sm font-medium" i18n>Filtros</label>
                <button uiButton variant="outline" size="sm" type="button" (click)="addFilter()" i18n>
                  Añadir filtro
                </button>
              </div>

              @if (filters().length === 0) {
                <p class="text-xs text-muted-foreground" i18n>Sin filtros: entra todo.</p>
              }

              @for (f of filters(); track $index) {
                <div class="flex items-center gap-2 mb-2">
                  <select class="flex-1 border border-border rounded-md px-2 py-1.5 text-sm bg-background"
                          [ngModel]="f.field" (ngModelChange)="changeFilterField($index, $event)"
                          [name]="'filter-field-' + $index">
                    @for (c of sourceFields(); track c.key) {
                      <option [value]="c.key">{{ c.name }}</option>
                    }
                  </select>

                  <!-- Sólo los operadores que valen para el tipo del campo elegido. «Mayor que»
                       sobre un estado no significa nada, y el servidor lo rechaza. -->
                  <select class="flex-1 border border-border rounded-md px-2 py-1.5 text-sm bg-background"
                          [(ngModel)]="f.operator" [name]="'filter-op-' + $index">
                    @for (o of operatorsFor(f.field); track o.key) {
                      <option [value]="o.key">{{ o.name }}</option>
                    }
                  </select>

                  @if (needsValue(f.operator)) {
                    <!--
                      Si el campo es una lista cerrada, se elige; si no, se escribe.

                      Escribir «Open» a mano es cómo se acaba filtrando por un valor que no
                      existe, y el resultado es un informe vacío que parece un informe sin datos.
                      Los valores los sirve el servidor, así que un estado nuevo aparece solo.
                    -->
                    @if (valuesOf(f.field); as values) {
                      @if (values.length > 0) {
                        <select class="flex-1 border border-border rounded-md px-2 py-1.5 text-sm bg-background"
                                [(ngModel)]="f.value" [name]="'filter-value-' + $index">
                          @for (v of values; track v) {
                            <option [value]="v">{{ v }}</option>
                          }
                        </select>
                      } @else {
                        <input class="flex-1 border border-border rounded-md px-2 py-1.5 text-sm bg-background"
                               [(ngModel)]="f.value" [name]="'filter-value-' + $index"
                               i18n-placeholder placeholder="Valor" />
                      }
                    }
                  }

                  <button type="button" class="text-muted-foreground hover:text-destructive px-2"
                          (click)="removeFilter($index)" i18n-aria-label aria-label="Quitar filtro">✕</button>
                </div>
              }
            </div>

            @if (error()) {
              <!-- El mensaje viene del servidor y dice qué pieza falla; se enseña entero. -->
              <p class="text-sm text-destructive border border-destructive/30 rounded-md px-3 py-2">
                {{ error() }}
              </p>
            }

            @if (preview(); as p) {
              <div class="border border-border rounded-md">
                <div class="px-3 py-2 border-b border-border">
                  <p class="text-sm font-medium">{{ p.title }}</p>
                  @if (p.subtitle) { <p class="text-xs text-muted-foreground">{{ p.subtitle }}</p> }
                </div>

                @if (p.rows.length === 0) {
                  <p class="px-3 py-4 text-sm text-muted-foreground" i18n>
                    Con estos filtros no hay datos. No es un error: es lo que hay.
                  </p>
                } @else {
                  <table class="w-full text-sm">
                    <thead>
                      <tr class="border-b border-border">
                        @for (c of p.columns; track c) {
                          <th class="text-left px-3 py-2 font-medium">{{ c }}</th>
                        }
                      </tr>
                    </thead>
                    <tbody>
                      @for (row of p.rows; track $index) {
                        <tr class="border-b border-border/50">
                          @for (cell of row; track $index) {
                            <td class="px-3 py-1.5">{{ cell }}</td>
                          }
                        </tr>
                      }
                    </tbody>
                  </table>

                  @if (p.totalRows > p.rows.length) {
                    <p class="px-3 py-2 text-xs text-muted-foreground">
                      Se enseñan {{ p.rows.length }} de {{ p.totalRows }} filas.
                    </p>
                  }
                }
              </div>
            }
          </div>

          <div class="px-5 py-4 border-t border-border flex items-center justify-end gap-2">
            <button uiButton variant="outline" type="button" (click)="close()" i18n>Cancelar</button>
            <button uiButton variant="outline" type="button" [disabled]="busy()" (click)="showPreview()" i18n>
              Ver resultado
            </button>
            <button uiButton type="button" [disabled]="busy()" (click)="save()" i18n>
              Guardar
            </button>
          </div>
        }
      </div>
    </div>
  `
})
export class ReportBuilderComponent {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  /** El informe cuya definición se está construyendo. */
  readonly reportId = input.required<string>();
  readonly reportTitle = input<string>('Informe');

  readonly closed = output<void>();
  readonly saved = output<void>();

  readonly catalog = signal<ReportCatalog | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly preview = signal<ReportPreview | null>(null);

  readonly dataSource = signal('');
  groupBy = '';
  measure = '';
  visualization = 'bar';
  granularity: string | null = 'month';

  readonly filters = signal<ReportFilter[]>([]);

  constructor() {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      const catalog = await firstValueFrom(this.api.get<ReportCatalog>('/reports/catalog'));
      this.catalog.set(catalog);

      // Si el informe ya tenía definición se reabre con ella; si no, se empieza por el primer
      // origen. `sinAviso` porque un informe sin definición todavía es lo normal, no un error
      // que haya que anunciar.
      const stored = await firstValueFrom(
        this.api.get<ReportDefinition>(`/reports/${this.reportId()}/definition`, undefined, { silent: true })
      ).catch(() => null);

      if (stored) this.apply(stored);
      else this.changeDataSource(catalog.dataSources[0]?.key ?? '');
    } catch (e) {
      this.error.set(errorMessage(e, $localize`No se pudo cargar el catálogo.`));
    } finally {
      this.loading.set(false);
    }
  }

  private apply(d: ReportDefinition): void {
    this.dataSource.set(d.dataSource);
    this.groupBy = d.groupBy;
    this.measure = d.measure;
    this.visualization = d.visualization;
    this.granularity = d.granularity ?? 'month';
    this.filters.set([...(d.filters ?? [])]);
  }

  readonly currentSource = computed(() =>
    this.catalog()?.dataSources.find(o => o.key === this.dataSource()) ?? null);

  readonly sourceFields = computed(() => this.currentSource()?.fields ?? []);
  readonly sourceMeasures = computed(() => this.currentSource()?.measures ?? []);

  readonly groupsByDate = computed(() =>
    this.sourceFields().find(c => c.key === this.groupBy)?.type === 'Date');

  /**
   * Cambiar de origen limpia lo que dependía del anterior.
   *
   * Sin esto quedaría una agrupación de tickets sobre un informe de tareas: el servidor lo
   * rechaza, pero la pantalla habría dejado ver una combinación imposible como si valiera.
   */
  changeDataSource(key: string): void {
    this.dataSource.set(key);

    const source = this.currentSource();
    this.groupBy = source?.fields[0]?.key ?? '';
    this.measure = source?.measures[0]?.key ?? 'count';
    this.filters.set([]);
    this.preview.set(null);
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
  operatorsFor(fieldKey: string): CatalogOperator[] {
    const field = this.sourceFields().find(c => c.key === fieldKey);
    if (!field) return [];

    return this.catalog()?.operators.filter(o => field.operators.includes(o.key)) ?? [];
  }

  /** Los valores admitidos de un campo, o lista vacía si admite texto libre. */
  valuesOf(fieldKey: string): string[] {
    return this.sourceFields().find(c => c.key === fieldKey)?.values ?? [];
  }

  needsValue(operatorKey: string): boolean {
    return this.catalog()?.operators.find(o => o.key === operatorKey)?.needsValue ?? true;
  }

  addFilter(): void {
    const field = this.sourceFields()[0];
    if (!field) return;

    const operator = this.operatorsFor(field.key)[0];

    this.filters.update(f => [...f, {
      field: field.key,
      operator: operator?.key ?? 'is',
      // Si el campo tiene lista, se empieza por su primer valor: un desplegable que arranca
      // vacío deja mandar un filtro sin valor sin que se note.
      value: field.values[0] ?? ''
    }]);
  }

  /** Al cambiar el campo, el operador puede dejar de valer para su tipo: se reajusta. */
  changeFilterField(index: number, key: string): void {
    this.filters.update(filters => filters.map((f, i) => {
      if (i !== index) return f;

      const operators = this.operatorsFor(key);
      const stillValid = operators.some(o => o.key === f.operator);

      // El valor del campo anterior casi nunca vale para el nuevo, y si el nuevo es una lista
      // cerrada hay que empezar por uno de los suyos.
      const values = this.valuesOf(key);

      return {
        field: key,
        operator: stillValid ? f.operator : operators[0]?.key ?? 'is',
        value: values.length > 0 ? values[0] : ''
      };
    }));
  }

  removeFilter(index: number): void {
    this.filters.update(f => f.filter((_, i) => i !== index));
  }

  private definition(): ReportDefinition {
    return {
      dataSource: this.dataSource(),
      groupBy: this.groupBy,
      measure: this.measure,
      visualization: this.visualization,
      filters: this.filters(),
      granularity: this.groupsByDate() ? this.granularity : null
    };
  }

  async showPreview(): Promise<void> {
    this.busy.set(true);
    this.error.set('');

    try {
      const preview = await firstValueFrom(this.api.post<ReportPreview>(
        '/reports/preview',
        { definition: this.definition(), title: this.reportTitle() },
        { silent: true }));

      this.preview.set(preview);
    } catch (e) {
      // El servidor dice qué pieza no encaja. Se enseña dentro del constructor, junto a los
      // desplegables, en vez de como aviso flotante: aquí es donde hay que corregirlo.
      this.preview.set(null);
      this.error.set(errorMessage(e, $localize`No se pudo calcular el resultado.`));
    } finally {
      this.busy.set(false);
    }
  }

  async save(): Promise<void> {
    this.busy.set(true);
    this.error.set('');

    try {
      await firstValueFrom(this.api.put(
        `/reports/${this.reportId()}/definition`, this.definition(), { silent: true }));

      this.toast.success($localize`Informe guardado.`);
      this.saved.emit();
      this.closed.emit();
    } catch (e) {
      this.error.set(errorMessage(e, $localize`No se pudo guardar el informe.`));
    } finally {
      this.busy.set(false);
    }
  }

  close(): void {
    this.closed.emit();
  }
}
