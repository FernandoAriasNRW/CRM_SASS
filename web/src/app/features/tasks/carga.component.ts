import { Component, computed, inject, input } from '@angular/core';
import { UsersService } from '../../core/users.service';
import { fechaDelDia, type Dia } from './gantt';
import { cargaDe } from './carga';
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
  selector: 'app-carga',
  standalone: true,
  templateUrl: './carga.component.html',
})
export class CargaComponent {
  readonly tareas = input.required<TaskItem[]>();

  private readonly usuarios = inject(UsersService);

  readonly carga = computed(() => cargaDe(this.tareas()));

  nombreDe(personaId: string | null): string {
    if (!personaId) return $localize`Sin asignar`;
    return this.usuarios.getUser(personaId)?.name ?? `${personaId.slice(0, 8)}…`;
  }

  etiquetaDeSemana(semana: Dia): string {
    return fechaDelDia(semana).toLocaleDateString(undefined, {
      day: '2-digit', month: 'short', timeZone: 'UTC',
    });
  }

  /** El ancho de la barra dentro de la celda, en porcentaje de la celda más alta de la tabla. */
  proporcion(horas: number): number {
    const maximo = this.carga().maximo;
    return maximo > 0 ? Math.round((horas / maximo) * 100) : 0;
  }
}
