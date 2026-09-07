import * as echarts from 'echarts/core';
import { BarChart, LineChart, PieChart } from 'echarts/charts';
import {
  GridComponent,
  LegendComponent,
  TitleComponent,
  TooltipComponent
} from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';

/**
 * ECharts, importada con **los módulos justos**.
 *
 * Es la condición que el estudio de la Fase 5 puso al elegirla: «ECharts pesa. Importada entera
 * se come el presupuesto de bundle, que ya está pasado». Importar `echarts` a secas mete todos
 * los tipos de gráfica, todos los componentes y los dos renderizadores; aquí van los tres tipos
 * que el catálogo de informes sabe pintar y nada más.
 *
 * **El mapa «tipo de gráfica → módulos que carga» vive aquí y en un solo sitio**, que era la otra
 * condición. Añadir un tipo nuevo al catálogo del servidor obliga a pasar por este fichero, y eso
 * es deliberado: es donde se ve lo que cuesta.
 *
 * `CanvasRenderer` y no el de SVG: para gráficas de pocas categorías rinde igual y pesa menos.
 * Si algún día hiciera falta exportar una gráfica desde el servidor, ahí sí se usaría el de SVG
 * —es la razón por la que se eligió ECharts frente a Chart.js— pero eso corre en Node, no aquí.
 */
echarts.use([
  BarChart,
  LineChart,
  PieChart,
  GridComponent,
  LegendComponent,
  TitleComponent,
  TooltipComponent,
  CanvasRenderer
]);

export { echarts };
