import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';

/**
 * Estas llamadas explican su propio error —junto al campo, o en el formulario que se queda
 * abierto—, así que el interceptor no debe levantar además su aviso: sería el mismo texto dos
 * veces para un solo fallo.
 */
const SIN_AVISO = { sinAviso: true };

/** Los tipos que define el backend. Cambiar esta lista sin cambiar allí no sirve de nada. */
export const TIPOS_DE_CAMPO = [
  { key: 'Text', label: $localize`Texto` },
  { key: 'Number', label: $localize`Número` },
  { key: 'Date', label: $localize`Fecha` },
  { key: 'Select', label: $localize`Selección` },
  { key: 'MultiSelect', label: $localize`Selección múltiple` },
  { key: 'User', label: $localize`Usuario` },
  { key: 'Formula', label: $localize`Calculado` },
] as const;

/** Los que se calculan solos: ni se rellenan ni se pueden marcar obligatorios. */
export function seCalcula(type: string): boolean {
  return type === 'Formula';
}

export const ENTIDADES = [
  { key: 'Task', label: $localize`Tareas` },
  { key: 'Project', label: $localize`Proyectos` },
] as const;

/** Separador de la selección múltiple. Lo fija el backend: un salto de línea. */
export const SEPARADOR_MULTIPLE = '\n';

export interface CustomFieldDefinition {
  id: string;
  name: string;
  type: string;
  targetEntity: string;
  isRequired: boolean;
  options: string[];
  position: number;
  /** La expresión, si es un campo calculado. */
  formula: string | null;
}

/** Un campo con su valor para una entidad concreta. */
export interface CustomFieldValue {
  definitionId: string;
  name: string;
  type: string;
  isRequired: boolean;
  options: string[];
  position: number;
  value: string | null;
  /** La expresión, si es calculado. Se enseña como ayuda junto al resultado. */
  formula?: string | null;
  /**
   * Por qué un campo calculado no tiene valor.
   *
   * Ojo con la distinción: un hueco por falta de datos deja `valor` y `error` en nulo, y es
   * normal —falta rellenar algo—. Esto sólo se llena cuando la fórmula en sí no se puede
   * calcular: divide entre cero, o usa un campo que ya no existe.
   */
  error?: string | null;
}

@Injectable({ providedIn: 'root' })
export class CustomFieldsService {
  private readonly api = inject(ApiService);

  /** Definiciones cacheadas por entidad: son pocas, cambian poco y las pide cada detalle. */
  private readonly porEntidad = signal<Record<string, CustomFieldDefinition[]>>({});

  definiciones(entidad: string): CustomFieldDefinition[] {
    return this.porEntidad()[entidad] ?? [];
  }

  cargarDefiniciones(entidad: string): Observable<CustomFieldDefinition[]> {
    return this.api.get<CustomFieldDefinition[]>('/custom-fields', { entity: entidad }).pipe(
      tap(definiciones => this.porEntidad.update(actual => ({ ...actual, [entidad]: definiciones ?? [] })))
    );
  }

  valoresDe(entidad: string, entityId: string): Observable<CustomFieldValue[]> {
    return this.api.get<CustomFieldValue[]>(`/custom-fields/values/${entidad}/${entityId}`);
  }

  /**
   * Guarda un valor. El backend valida y normaliza: aquí no se transforma nada, porque dos
   * normalizaciones distintas —una en el navegador y otra en el servidor— acaban discrepando.
   */
  guardarValor(definitionId: string, entityId: string, value: string | null): Observable<void> {
    return this.api.put<void>(`/custom-fields/values/${definitionId}/${entityId}`, { value }, SIN_AVISO);
  }

  definir(campo: Omit<CustomFieldDefinition, 'id'>): Observable<CustomFieldDefinition> {
    return this.api.post<CustomFieldDefinition>('/custom-fields', campo, SIN_AVISO).pipe(
      tap(() => this.invalidar(campo.targetEntity))
    );
  }

  actualizar(id: string, entidad: string, cambios: Pick<CustomFieldDefinition, 'name' | 'isRequired' | 'options' | 'position' | 'formula'>): Observable<void> {
    return this.api.put<void>(`/custom-fields/${id}`, cambios, SIN_AVISO).pipe(tap(() => this.invalidar(entidad)));
  }

  borrar(id: string, entidad: string): Observable<void> {
    return this.api.delete<void>(`/custom-fields/${id}`, SIN_AVISO).pipe(tap(() => this.invalidar(entidad)));
  }

  private invalidar(entidad: string): void {
    this.porEntidad.update(actual => {
      const copia = { ...actual };
      delete copia[entidad];
      return copia;
    });
  }
}
