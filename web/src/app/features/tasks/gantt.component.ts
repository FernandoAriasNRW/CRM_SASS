import { Component, computed, input, output } from '@angular/core';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideBan, lucideDiamond } from '@ng-icons/lucide';
import { PRIORIDADES, type TaskItem } from './task-create-modal.component';
import {
  barrasDe, esFinDeSemana, fechaDelDia, hoyComoDia, marcasDelEje, rangoDe,
  type Barra, type Dia,
} from './gantt';

/** Ancho de un día, en píxeles. Con menos, un hito de un día deja de poder pulsarse. */
const ANCHO_DE_DIA = 28;

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
  readonly tareas = input.required<TaskItem[]>();
  readonly abrir = output<TaskItem>();

  readonly anchoDeDia = ANCHO_DE_DIA;

  readonly rango = computed(() => rangoDe(this.tareas()));

  readonly barras = computed<Barra[]>(() => {
    const rango = this.rango();
    return rango ? barrasDe(this.tareas(), rango) : [];
  });

  readonly marcas = computed(() => {
    const rango = this.rango();
    return rango ? marcasDelEje(rango) : [];
  });

  /** Los días del rango, para el fondo: fines de semana sombreados y la línea de hoy. */
  readonly dias = computed(() => {
    const rango = this.rango();
    if (!rango) return [];

    const hoy = hoyComoDia();

    return Array.from({ length: rango.dias }, (_, i) => {
      const dia = rango.primerDia + i;
      return { dia, finDeSemana: esFinDeSemana(dia), esHoy: dia === hoy };
    });
  });

  readonly anchoTotal = computed(() => (this.rango()?.dias ?? 0) * ANCHO_DE_DIA);

  posicionDe(marca: { dia: Dia }): number {
    const rango = this.rango();
    return rango ? (marca.dia - rango.primerDia) * ANCHO_DE_DIA : 0;
  }

  colorDe(tarea: TaskItem): string {
    return PRIORIDADES.find(p => p.key === tarea.priority)?.color ?? 'text-muted-foreground';
  }

  /** El texto que lee un lector de pantalla, que no puede ver la barra. */
  descripcionDe(barra: Barra): string {
    const vence = fechaDelDia(
      (this.rango()?.primerDia ?? 0) + barra.desplazamiento + barra.duracion - 1)
      .toLocaleDateString(undefined, { timeZone: 'UTC' });

    if (barra.esHito) return $localize`${barra.tarea.title}, vence el ${vence}`;

    const empieza = fechaDelDia((this.rango()?.primerDia ?? 0) + barra.desplazamiento)
      .toLocaleDateString(undefined, { timeZone: 'UTC' });

    return $localize`${barra.tarea.title}, del ${empieza} al ${vence}`;
  }
}
