import { Component, computed, input, output } from '@angular/core';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideBan, lucideDiamond } from '@ng-icons/lucide';
import { PRIORITIES, type TaskItem } from './task-create-modal.component';
import {
  barsOf, isWeekend, dateOfDay, arrowsOf, todayAsDay, axisTicks, rangeOf,
  type DependencyEdge, type Bar, type Day,
} from './gantt';

/** Ancho de un día, en píxeles. Con menos, un hito de un día deja de poder pulsarse. */
const DAY_WIDTH = 28;

/** Alto de una fila, en píxeles. Tiene que cuadrar con la clase `h-9` de la plantilla. */
const ROW_HEIGHT = 36;

/**
 * Diagrama de Gantt de las tareas cargadas.
 *
 * **Pinta lo que hay, no lo que se podría suponer.** Una tarea sin fecha de inicio sale como un
 * hito en su vencimiento, no como una barra de duración inventada; una tarea sin vencimiento no
 * sale, porque no hay dónde ponerla. Es la misma decisión que en el resto del producto: preferir
 * un hueco visible a un dato que parece real y no lo es.
 *
 * No es interactivo todavía: arrastrar una barra para reprogramar llega después, y dejarlo a
 * medias —que se mueva y no se guarde— sería peor que no tenerlo.
 */
@Component({
  selector: 'app-gantt',
  standalone: true,
  imports: [NgIconComponent],
  viewProviders: [provideIcons({ lucideBan, lucideDiamond })],
  templateUrl: './gantt.component.html',
})
export class GanttComponent {
  readonly tasks = input.required<TaskItem[]>();
  /** El grafo entero de dependencias. Vacío mientras se carga: se pinta sin flechas y ya. */
  readonly dependencies = input<DependencyEdge[]>([]);
  readonly open = output<TaskItem>();

  readonly dayWidth = DAY_WIDTH;
  readonly rowHeight = ROW_HEIGHT;

  readonly range = computed(() => rangeOf(this.tasks()));

  readonly bars = computed<Bar[]>(() => {
    const range = this.range();
    return range ? barsOf(this.tasks(), range) : [];
  });

  readonly ticks = computed(() => {
    const range = this.range();
    return range ? axisTicks(range) : [];
  });

  /** Los días del rango, para el fondo: fines de semana sombreados y la línea de hoy. */
  readonly days = computed(() => {
    const range = this.range();
    if (!range) return [];

    const today = todayAsDay();

    return Array.from({ length: range.days }, (_, i) => {
      const day = range.firstDay + i;
      return { day, weekend: isWeekend(day), isToday: day === today };
    });
  });

  readonly totalWidth = computed(() => (this.range()?.days ?? 0) * DAY_WIDTH);

  readonly arrows = computed(() => arrowsOf(this.dependencies(), this.bars()));

  readonly totalHeight = computed(() => this.bars().length * ROW_HEIGHT);

  readonly hasViolations = computed(() => this.arrows().some(f => f.isViolated));

  /**
   * El trazado de una flecha: sale del final de la barra que bloquea, gira por el pasillo entre
   * las dos filas y entra por la izquierda de la bloqueada.
   *
   * En ortogonal y no en recta a propósito: con varias flechas cruzadas, las diagonales se
   * confunden entre sí y con las barras.
   */
  pathOf(arrow: { fromDay: number; fromRow: number; toDay: number; toRow: number }): string {
    const x1 = arrow.fromDay * DAY_WIDTH;
    const y1 = arrow.fromRow * ROW_HEIGHT + ROW_HEIGHT / 2;
    const x2 = arrow.toDay * DAY_WIDTH;
    const y2 = arrow.toRow * ROW_HEIGHT + ROW_HEIGHT / 2;

    // Un tramo horizontal mínimo antes de girar: sin él, una flecha entre dos barras pegadas
    // sale como una línea vertical suelta que no se entiende.
    const elbow = Math.max(x1 + DAY_WIDTH / 2, x2 - DAY_WIDTH / 2);

    return `M ${x1} ${y1} H ${elbow} V ${y2} H ${x2}`;
  }

  positionOf(timestamp: { day: Day }): number {
    const range = this.range();
    return range ? (timestamp.day - range.firstDay) * DAY_WIDTH : 0;
  }

  colorOf(task: TaskItem): string {
    return PRIORITIES.find(p => p.key === task.priority)?.color ?? 'text-muted-foreground';
  }

  /** El texto que lee un lector de pantalla, que no puede ver la barra. */
  descriptionOf(bar: Bar): string {
    const due = dateOfDay(
      (this.range()?.firstDay ?? 0) + bar.offset + bar.duration - 1)
      .toLocaleDateString(undefined, { timeZone: 'UTC' });

    if (bar.isMilestone) return $localize`${bar.task.title}, vence el ${due}`;

    const start = dateOfDay((this.range()?.firstDay ?? 0) + bar.offset)
      .toLocaleDateString(undefined, { timeZone: 'UTC' });

    return $localize`${bar.task.title}, del ${start} al ${due}`;
  }
}
