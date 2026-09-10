import { Component, computed, input } from '@angular/core';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import type { EChartsOption } from 'echarts';

import { echarts } from './echarts-modulos';

/**
 * Lo que el servidor manda para pintar un recuadro: una tabla y cómo quiere verse.
 *
 * Es la misma forma que devuelve la vista previa del constructor, a propósito: el panel y el
 * constructor enseñan lo mismo porque pintan lo mismo.
 */
export interface DatosDeWidget {
  widgetId: string;
  reportId: string;
  titulo: string;
  forma: string;
  subtitulo: string | null;
  columnas: string[];
  filas: string[][];
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
  selector: 'app-grafica-de-informe',
  standalone: true,
  imports: [NgxEchartsDirective],
  providers: [provideEchartsCore({ echarts })],
  template: `
    @if (datos().error; as error) {
      <!--
        Un recuadro roto lo dice **en su sitio**, con el motivo que manda el servidor. Dejarlo en
        blanco haría pensar que no hay datos, que es otra cosa y se arregla de otra manera.
      -->
      <div class="h-full flex items-center justify-center p-4 text-center">
        <p class="text-sm text-muted-foreground">{{ error }}</p>
      </div>
    } @else if (datos().filas.length === 0) {
      <div class="h-full flex items-center justify-center p-4 text-center">
        <p class="text-sm text-muted-foreground" i18n>Sin datos para este informe.</p>
      </div>
    } @else if (esTabla()) {
      <div class="h-full overflow-auto">
        <table class="w-full text-sm">
          <thead class="sticky top-0 bg-card">
            <tr class="border-b border-border">
              @for (c of datos().columnas; track c) {
                <th class="text-left px-3 py-1.5 font-medium">{{ c }}</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (fila of datos().filas; track $index) {
              <tr class="border-b border-border/50">
                @for (celda of fila; track $index) {
                  <td class="px-3 py-1">{{ celda }}</td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <div echarts [options]="opciones()" [autoResize]="true" class="h-full w-full"></div>
    }
  `
})
export class GraficaDeInformeComponent {
  readonly datos = input.required<DatosDeWidget>();

  /** Si el tema del navegador es oscuro, para que los ejes no salgan negros sobre negro. */
  readonly oscuro = input(false);

  readonly esTabla = computed(() => this.datos().forma === 'tabla');

  /**
   * Los valores numéricos, sacados de la última columna.
   *
   * El servidor manda todo como texto porque ya lo ha formateado para el idioma de quien lee —con
   * coma decimal—, así que hay que deshacer ese formato para la gráfica. Una raya («—», que es lo
   * que escribe cuando no hay nada que promediar) se convierte en `null`, no en 0: ECharts deja el
   * hueco en la serie, que es lo honesto, en vez de dibujar una caída a cero que no ocurrió.
   */
  private readonly valores = computed(() =>
    this.datos().filas.map(fila => {
      const crudo = fila[fila.length - 1];
      if (!crudo || crudo === '—') return null;

      const numero = Number(crudo.replace(/\./g, '').replace(',', '.').replace(/[^\d.-]/g, ''));
      return Number.isFinite(numero) ? numero : null;
    }));

  private readonly etiquetas = computed(() => this.datos().filas.map(f => f[0]));

  readonly opciones = computed<EChartsOption>(() => {
    const d = this.datos();
    const etiquetas = this.etiquetas();
    const valores = this.valores();

    // Un color por serie y el resto de la paleta por defecto: el gris de los ejes se elige según
    // el tema porque ECharts no lo hereda del CSS.
    const textoTenue = this.oscuro() ? '#9CA3AF' : '#6B7280';

    const comunes: EChartsOption = {
      tooltip: { trigger: d.forma === 'tarta' ? 'item' : 'axis' },
      grid: { left: 8, right: 16, top: 24, bottom: 8, containLabel: true },
      textStyle: { color: textoTenue }
    };

    if (d.forma === 'tarta') {
      return {
        ...comunes,
        legend: { bottom: 0, textStyle: { color: textoTenue } },
        series: [{
          type: 'pie',
          radius: ['45%', '70%'],
          // Sin las etiquetas encima: en un recuadro pequeño se pisan entre ellas y tapan la
          // gráfica. La leyenda de abajo y el tooltip dicen lo mismo sin estorbar.
          label: { show: false },
          data: etiquetas.map((nombre, i) => ({ name: nombre, value: valores[i] ?? 0 }))
        }]
      };
    }

    const eje: EChartsOption = {
      ...comunes,
      xAxis: {
        type: 'category',
        data: etiquetas,
        axisLabel: {
          color: textoTenue,
          // Las etiquetas largas —nombres de proyecto, identificadores— se giran para que quepan
          // en vez de recortarse con puntos suspensivos.
          rotate: etiquetas.some(e => e.length > 10) ? 30 : 0
        }
      },
      yAxis: { type: 'value', axisLabel: { color: textoTenue } }
    };

    if (d.forma === 'lineas') {
      return {
        ...eje,
        series: [{
          type: 'line',
          smooth: true,
          // `connectNulls` en false a propósito: un hueco en la serie —un mes sin nada que
          // promediar— se ve como hueco. Uniéndolo, la línea pasaría por encima como si hubiera
          // datos que no hay.
          connectNulls: false,
          data: valores
        }]
      };
    }

    // Barras y barras apiladas comparten forma mientras haya una sola serie: apilar necesita una
    // segunda dimensión que el catálogo todavía no ofrece. Se pinta como barras en vez de
    // rechazarlo, y queda anotado.
    return { ...eje, series: [{ type: 'bar', data: valores }] };
  });
}
