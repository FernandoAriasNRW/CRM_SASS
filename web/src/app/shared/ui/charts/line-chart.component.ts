import { Component, input, effect, ElementRef, viewChild, OnDestroy } from '@angular/core';
import { Chart } from 'chart.js/auto';

export interface BurndownDataPoint {
  date: string;
  remainingTasks: number;
  idealTasks: number;
}

@Component({
  selector: 'app-line-chart',
  standalone: true,
  template: `
    <div class="relative w-full h-full">
      <canvas #canvas [attr.id]="chartId()"></canvas>
    </div>
  `
})
export class LineChartComponent implements OnDestroy {
  readonly chartId = input<string>(`line-${Math.random().toString(36).substring(2, 9)}`);
  readonly data = input.required<BurndownDataPoint[]>();
  readonly title = input<string>('');

  private chart: Chart | null = null;
  private readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('canvas');

  constructor() {
    effect(() => {
      const chartData = this.data();
      const canvas = this.canvasRef();
      if (chartData && chartData.length > 0 && canvas) {
        this.renderChart(chartData, canvas.nativeElement);
      }
    });
  }

  ngOnDestroy(): void {
    if (this.chart) {
      this.chart.destroy();
      this.chart = null;
    }
  }

  private renderChart(chartData: BurndownDataPoint[], canvasEl: HTMLCanvasElement): void {
    const ctx = canvasEl.getContext('2d');
    if (!ctx) return;

    if (this.chart) {
      this.chart.destroy();
    }

    const labels = chartData.map(d => {
      const date = new Date(d.date);
      return date.toLocaleDateString('es-ES', { month: 'short', day: 'numeric' });
    });

    const remainingData = chartData.map(d => d.remainingTasks);
    const idealData = chartData.map(d => d.idealTasks);

    this.chart = new Chart(ctx, {
      type: 'line',
      data: {
        labels,
        datasets: [
          {
            label: 'Real',
            data: remainingData,
            borderColor: '#3b82f6',
            backgroundColor: 'rgba(59, 130, 246, 0.1)',
            fill: true,
            tension: 0.3,
            pointRadius: 3,
            pointHoverRadius: 5,
          },
          {
            label: 'Ideal',
            data: idealData,
            borderColor: '#22c55e',
            backgroundColor: 'transparent',
            borderDash: [5, 5],
            tension: 0,
            pointRadius: 0,
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: {
          intersect: false,
          mode: 'index',
        },
        plugins: {
          legend: {
            position: 'top',
            labels: {
              usePointStyle: true,
              padding: 16,
              font: { size: 12 }
            }
          },
          tooltip: {
            callbacks: {
              title: (items) => `Fecha: ${items[0].label}`,
              label: (context) => `${context.dataset.label}: ${context.raw} tareas`
            }
          }
        },
        scales: {
          x: {
            grid: { display: false },
            ticks: { maxTicksLimit: 7 }
          },
          y: {
            beginAtZero: true,
            title: { display: true, text: 'Tareas restantes' },
            grid: { color: 'rgba(0,0,0,0.05)' }
          }
        }
      }
    });
  }
}
