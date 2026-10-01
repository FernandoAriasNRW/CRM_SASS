import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

/**
 * Estas llamadas explican su propio error —junto al campo, o en el formulario que se queda
 * abierto—, así que el interceptor no debe levantar además su aviso: sería el mismo texto dos
 * veces para un solo fallo.
 */
const SIN_AVISO = { sinAviso: true };

/**
 * Las reglas de automatización.
 *
 * **El vocabulario —disparadores, campos, operadores, acciones— lo sirve el servidor.** No se
 * repite aquí a propósito: una lista duplicada se desincroniza el día que se añada un disparador,
 * y entonces esta pantalla dejaría configurar algo que el servidor no entiende, o escondería algo
 * que sí admite.
 */
export interface VocabularioDeAutomatizacion {
  triggers: string[];
  fields: string[];
  operators: string[];
  actions: string[];
}

export interface CondicionDeRegla {
  field: string;
  operator: string;
  value: string | null;
}

export interface AccionDeRegla {
  type: string;
  value: string;
}

export interface ReglaDeAutomatizacion {
  id: string;
  name: string;
  trigger: string;
  isActive: boolean;
  conditions: CondicionDeRegla[];
  actions: AccionDeRegla[];
  executionCount: number;
  lastExecutedAtUtc: string | null;
}

/** Lo que hace falta para crear o actualizar una regla. */
export interface ReglaEditable {
  name: string;
  trigger: string;
  conditions: CondicionDeRegla[];
  actions: AccionDeRegla[];
}

/**
 * El único operador que no compara contra nada. Lo decide el dominio; aquí se repite para no
 * pedir un valor que el servidor va a ignorar.
 */
export const OPERADOR_SIN_VALOR = 'IsEmpty';

/**
 * Cómo se lee cada código del vocabulario.
 *
 * El servidor habla en códigos (`TaskStatusChanged`, `EqualTo`…) y la pantalla los enseña en
 * castellano: el código es para las máquinas y la etiqueta para quien configura la regla. Un código
 * que llegue del servidor y no esté aquí se enseña tal cual, en vez de dejar el desplegable con una
 * opción en blanco: se ve raro, pero se puede elegir.
 */
const AUTOMATION_LABELS: Record<string, string> = {
  TaskCreated: $localize`Se crea una tarea`,
  TaskStatusChanged: $localize`Cambia el estado de una tarea`,
  TaskPriorityChanged: $localize`Cambia la prioridad de una tarea`,
  TaskDueSoon: $localize`Una tarea está por vencer`,

  Status: $localize`Estado`,
  PreviousStatus: $localize`Estado anterior`,
  Priority: $localize`Prioridad`,
  PreviousPriority: $localize`Prioridad anterior`,
  ProjectId: $localize`Proyecto`,
  AssigneeId: $localize`Responsable`,
  DaysUntilDue: $localize`Días para vencer`,
  Title: $localize`Título`,

  EqualTo: $localize`es igual a`,
  NotEqualTo: $localize`es distinto de`,
  Contains: $localize`contiene`,
  IsEmpty: $localize`está vacío`,
  LessOrEqual: $localize`es menor o igual que`,
  GreaterOrEqual: $localize`es mayor o igual que`,

  ChangeStatus: $localize`Cambiar el estado`,
  ChangePriority: $localize`Cambiar la prioridad`,
  AssignTo: $localize`Asignar a`,
  Notify: $localize`Avisar`,
};

export function automationLabel(code: string): string {
  return AUTOMATION_LABELS[code] ?? code;
}

@Injectable({ providedIn: 'root' })
export class AutomationsService {
  private readonly api = inject(ApiService);

  vocabulario(): Observable<VocabularioDeAutomatizacion> {
    return this.api.get<VocabularioDeAutomatizacion>('/automations/vocabulary');
  }

  reglas(): Observable<ReglaDeAutomatizacion[]> {
    return this.api.get<ReglaDeAutomatizacion[]>('/automations');
  }

  crear(regla: ReglaEditable): Observable<ReglaDeAutomatizacion> {
    return this.api.post<ReglaDeAutomatizacion>('/automations', regla, SIN_AVISO);
  }

  actualizar(id: string, regla: ReglaEditable): Observable<void> {
    return this.api.put<void>(`/automations/${id}`, regla, SIN_AVISO);
  }

  /** Apagar y encender tiene su propia llamada: es lo que se hace con prisa. */
  activar(id: string, isActive: boolean): Observable<void> {
    return this.api.put<void>(`/automations/${id}/active`, { isActive }, SIN_AVISO);
  }

  borrar(id: string): Observable<void> {
    return this.api.delete<void>(`/automations/${id}`, SIN_AVISO);
  }
}
