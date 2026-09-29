import { Component, computed, input } from '@angular/core';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import type { EChartsOption } from 'echarts';

import { echarts } from './echarts-modules';

/**
 * Lo que el servidor manda para pintar un recuadro: una tabla y cómo quiere verse.
 *
 * Es la misma forma que devuelve la vista previa del constructor, a propósito: el panel y el
 * constructor enseñan lo mismo porque pintan lo mismo.
 */
export interface WidgetData {
  widgetId: string;
  reportId: string;
  title: string;
  forma: string;
  subtitle: string | null;
  columns: string[];
  rows: string[][];
  error: string | null;
}

/**
 * Pinta un informe con la forma que pide.
 *
 * **Aquí es donde la definición neutra se convierte en opciones de ECharts, y en ningún otro
 * sitio.** Es la frontera que el plan puso: «lo que se guarda no son opciones de ECharts, sino
 * una definición neutra —origen, filtros, agrupación, medida, forma—. Guardar opciones de ECharts
 * ataría todos los reportes guardados a la librería: cambiarla algún día invalidaría el trabajo de
 * los usuarios, no sólo el nuestro».
 *
 * La consecuencia práctica: cambiar de librería de gráficas algún día es reescribir este fichero,
 * no migrar los informes que la gente haya construido.
 */
@Component({
  selector: 'app-report-chart',
  standalone: true,
  imports: [NgxEchartsDirective],
  providers: [provideEchartsCore({ echarts })],
  template: `
    @if (data().error; as error) {
      <!--
        Un recuadro roto lo dice **en su sitio**, con el motivo que manda el servidor. Dejarlo en
        blanco haría pensar que no hay datos, que es otra cosa y se arregla de otra manera.
      -->
      <div class="h-full flex items-center justify-center p-4 text-center">
        <p class="text-sm text-muted-foreground">{{ error }}</p>
      </div>
    } @else if (data().rows.length === 0) {
      <div class="h-full flex items-center justify-center p-4 text-center">
        <p class="text-sm text-muted-foreground" i18n>Sin datos para este informe.</p>
      </div>
    } @else if (isTable()) {
      <div class="h-full overflow-auto">
        <table class="w-full text-sm">
          <thead class="sticky top-0 bg-card">
            <tr class="border-b border-border">
              @for (c of data().columns; track c) {
                <th class="text-left px-3 py-1.5 font-medium">{{ c }}</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (row of data().rows; track $index) {
              <tr class="border-b border-border/50">
                @for (cell of row; track $index) {
                  <td class="px-3 py-1">{{ cell }}</td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <div echarts [options]="options()" [autoResize]="true" class="h-full w-full"></div>
    }
  `
})
export class ReportChartComponent {
  readonly data = input.required<WidgetData>();

  /** Si el tema del navegador es oscuro, para que los ejes no salgan negros sobre negro. */
  readonly dark = input(false);

  readonly isTable = computed(() => this.data().forma === 'tabla');

  /**
   * Los valores numéricos, sacados de la última columna.
   *
   * El servidor manda todo como texto porque ya lo ha formateado para el idioma de quien lee —con
   * coma decimal—, así que hay que deshacer ese formato para la gráfica. Una raya («—», que es lo
   * que escribe cuando no hay nada que promediar) se convierte en `null`, no en 0: ECharts deja el
   * hueco en la serie, que es lo honesto, en vez de dibujar una caída a cero que no ocurrió.
   */
  private readonly values = computed(() =>
    this.data().rows.map(row => {
      const raw = row[row.length - 1];
      if (!raw || raw === '—') return null;

      const num = Number(raw.replace(/\./g, '').replace(',', '.').replace(/[^\d.-]/g, ''));
      return Number.isFinite(num) ? num : null;
    }));

  private readonly labels = computed(() => this.data().rows.map(f => f[0]));

  readonly options = computed<EChartsOption>(() => {
    const d = this.data();
    const labels = this.labels();
    const values = this.values();

    // Un color por serie y el resto de la paleta por defecto: el gris de los ejes se elige según
    // el tema porque ECharts no lo hereda del CSS.
    const mutedText = this.dark() ? '#9CA3AF' : '#6B7280';

    const common: EChartsOption = {
      tooltip: { trigger: d.forma === 'tarta' ? 'item' : 'axis' },
      grid: { left: 8, right: 16, top: 24, bottom: 8, containLabel: true },
      textStyle: { color: mutedText }
    };

    if (d.forma === 'tarta') {
      return {
        ...common,
        legend: { bottom: 0, textStyle: { color: mutedText } },
        series: [{
          type: 'pie',
          radius: ['45%', '70%'],
          // Sin las etiquetas encima: en un recuadro pequeño se pisan entre ellas y tapan la
          // gráfica. La leyenda de abajo y el tooltip dicen lo mismo sin estorbar.
          label: { show: false },
          data: labels.map((name, i) => ({ name: name, value: values[i] ?? 0 }))
        }]
      };
    }

    const axis: EChartsOption = {
      ...common,
      xAxis: {
        type: 'category',
        data: labels,
        axisLabel: {
          color: mutedText,
          // Las etiquetas largas —nombres de proyecto, identificadores— se giran para que quepan
          // en vez de recortarse con puntos suspensivos.
          rotate: labels.some(e => e.length > 10) ? 30 : 0
        }
      },
      yAxis: { type: 'value', axisLabel: { color: mutedText } }
    };

    if (d.forma === 'lineas') {
      return {
        ...axis,
        series: [{
          type: 'line',
          smooth: true,
          // `connectNulls` en false a propósito: un hueco en la serie —un mes sin nada que
          // promediar— se ve como hueco. Uniéndolo, la línea pasaría por encima como si hubiera
          // datos que no hay.
          connectNulls: false,
          data: values
        }]
      };
    }

    // Barras y barras apiladas comparten forma mientras haya una sola serie: apilar necesita una
    // segunda dimensión que el catálogo todavía no ofrece. Se pinta como barras en vez de
    // rechazarlo, y queda anotado.
    return { ...axis, series: [{ type: 'bar', data: values }] };
  });
}
