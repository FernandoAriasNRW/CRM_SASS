import { Component, computed, inject, input } from '@angular/core';
import { UsersService } from '../../core/users.service';
import { dateOfDay, type Day } from './gantt';
import { workloadOf } from './workload';
import type { TaskItem } from './task-create-modal.component';

/**
 * Carga de trabajo por persona y semana.
 *
 * Las horas estimadas de cada tarea se reparten entre los días laborables que ocupa, y eso es
 * **una suposición**: no hay dato de cuánto se dedica cada día. La vista lo dice en pantalla en
 * lugar de presentar el número como un hecho medido.
 *
 * No hay línea de capacidad —«40 horas semanales»— porque este producto no sabe la jornada de
 * nadie. Pintar una sería inventarse el dato más importante de la vista: el que decide si algo
 * está sobrecargado. Las barras se escalan contra la celda más alta de la propia tabla, que
 * compara sin afirmar nada.
 */
@Component({
  selector: 'app-workload',
  standalone: true,
  templateUrl: './workload.component.html',
})
export class WorkloadComponent {
  readonly tasks = input.required<TaskItem[]>();

  private readonly users = inject(UsersService);

  readonly workload = computed(() => workloadOf(this.tasks()));

  nameOf(userId: string | null): string {
    if (!userId) return $localize`Sin asignar`;
    return this.users.getUser(userId)?.name ?? `${userId.slice(0, 8)}…`;
  }

  weekLabel(week: Day): string {
    return dateOfDay(week).toLocaleDateString(undefined, {
      day: '2-digit', month: 'short', timeZone: 'UTC',
    });
  }

  /** El ancho de la barra dentro de la celda, en porcentaje de la celda más alta de la tabla. */
  barWidth(hours: number): number {
    const max = this.workload().max;
    return max > 0 ? Math.round((hours / max) * 100) : 0;
  }
}
