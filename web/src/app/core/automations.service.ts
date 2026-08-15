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
  disparadores: string[];
  campos: string[];
  operadores: string[];
  acciones: string[];
}

export interface CondicionDeRegla {
  campo: string;
  operador: string;
  valor: string | null;
}

export interface AccionDeRegla {
  tipo: string;
  valor: string;
}

export interface ReglaDeAutomatizacion {
  id: string;
  nombre: string;
  disparador: string;
  activa: boolean;
  condiciones: CondicionDeRegla[];
  acciones: AccionDeRegla[];
  vecesEjecutada: number;
  ultimaEjecucionUtc: string | null;
}

/** Lo que hace falta para crear o actualizar una regla. */
export interface ReglaEditable {
  nombre: string;
  disparador: string;
  condiciones: CondicionDeRegla[];
  acciones: AccionDeRegla[];
}

/**
 * El único operador que no compara contra nada. Lo decide el dominio; aquí se repite para no
 * pedir un valor que el servidor va a ignorar.
 */
export const OPERADOR_SIN_VALOR = 'EstaVacio';

@Injectable({ providedIn: 'root' })
export class AutomationsService {
  private readonly api = inject(ApiService);

  vocabulario(): Observable<VocabularioDeAutomatizacion> {
    return this.api.get<VocabularioDeAutomatizacion>('/automations/vocabulario');
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
  activar(id: string, activa: boolean): Observable<void> {
    return this.api.put<void>(`/automations/${id}/active`, { activa }, SIN_AVISO);
  }

  borrar(id: string): Observable<void> {
    return this.api.delete<void>(`/automations/${id}`, SIN_AVISO);
  }
}
