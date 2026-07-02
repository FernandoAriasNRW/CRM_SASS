import { Component, input, effect, ElementRef, viewChild, OnDestroy } from '@angular/core';
import { Chart } from 'chart.js/auto';

@Component({
  selector: 'app-doughnut-chart',
  standalone: true,
  template: `
    <div class="relative w-full h-full">
      <canvas #canvas [attr.id]="chartId()"></canvas>
    </div>
  `
})
export class DoughnutChartComponent implements OnDestroy {
  readonly chartId = input<string>(`doughnut-${Math.random().toString(36).substring(2, 9)}`);
  readonly data = input.required<{ labels: string[]; values: number[]; colors: string[] }>();
  readonly title = input<string>('');

  private chart: Chart | null = null;
  private readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('canvas');

  constructor() {
    effect(() => {
      const chartData = this.data();
      const canvas = this.canvasRef();
      if (chartData && canvas) {
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

  private renderChart(chartData: { labels: string[]; values: number[]; colors: string[] }, canvasEl: HTMLCanvasElement): void {
    const ctx = canvasEl.getContext('2d');
    if (!ctx) return;

    if (this.chart) {
      this.chart.destroy();
    }

    this.chart = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: chartData.labels,
        datasets: [{
          data: chartData.values,
          backgroundColor: chartData.colors,
          borderWidth: 0,
          hoverOffset: 4,
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '65%',
        plugins: {
          legend: {
            position: 'bottom',
            labels: {
              padding: 16,
              usePointStyle: true,
              pointStyle: 'circle',
              font: { size: 12 }
            }
          },
          tooltip: {
            callbacks: {
              label: (context) => {
                const value = context.raw as number;
                const total = (context.dataset.data as number[]).reduce((a: number, b: number) => a + b, 0);
                const percentage = total > 0 ? Math.round((value / total) * 100) : 0;
                return `${context.label}: ${value} (${percentage}%)`;
              }
            }
          }
        }
      }
    });
  }
}
