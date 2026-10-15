import { TestBed } from '@angular/core/testing';

import { ReportChartComponent, type WidgetData } from './report-chart.component';

/**
 * Las opciones de ECharts que salen de cada informe. Se prueban aquí y no en la e2e porque
 * ECharts dibuja en un lienzo y no deja nada que leer en la página.
 */
describe('ReportChartComponent', () => {
  const widget = (visualization: string, columns: string[], rows: string[][]): WidgetData => ({
    widgetId: 'w', reportId: 'r', title: 'Informe', subtitle: null, visualization, columns, rows, error: null,
  });

  function optionsFor(data: WidgetData) {
    TestBed.configureTestingModule({ imports: [ReportChartComponent] });
    const fixture = TestBed.createComponent(ReportChartComponent);
    fixture.componentRef.setInput('data', data);
    return fixture.componentInstance.options() as {
      series: { type: string; name?: string; stack?: string; data: (number | null)[] }[];
      legend?: unknown;
    };
  }

  it('paints stacked bars as one stacked series per value column, with a legend', () => {
    const options = optionsFor(widget('stacked_bar', ['Estado', 'Alta', 'Baja'], [['Abierto', '2', '1'], ['Cerrado', '0', '3']]));

    expect(options.series.map(s => s.name)).toEqual(['Alta', 'Baja']);
    expect(options.series.every(s => s.type === 'bar' && s.stack === 'total')).toBeTrue();
    expect(options.series[1].data).toEqual([1, 3]);
    expect(options.legend).toBeDefined();
  });

  it('keeps a stacked report with a single value column as plain bars', () => {
    const options = optionsFor(widget('stacked_bar', ['Estado', 'Tickets'], [['Abierto', '2']]));

    expect(options.series.length).toBe(1);
    expect(options.series[0].stack).toBeUndefined();
  });

  it('reads spanish numbers and leaves a dash as a gap, not a zero', () => {
    const options = optionsFor(widget('stacked_bar', ['Mes', 'A', 'B'], [['2026-10', '1.234,5', '—']]));

    expect(options.series[0].data).toEqual([1234.5]);
    expect(options.series[1].data).toEqual([null]);
  });
});
