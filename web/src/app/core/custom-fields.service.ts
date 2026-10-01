import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';

/**
 * Estas llamadas explican su propio error —junto al campo, o en el formulario que se queda
 * abierto—, así que el interceptor no debe levantar además su aviso: sería el mismo texto dos
 * veces para un solo fallo.
 */
const SILENT = { silent: true };

/** Los tipos que define el backend. Cambiar esta lista sin cambiar allí no sirve de nada. */
export const FIELD_TYPES = [
  { key: 'Text', label: $localize`Texto` },
  { key: 'Number', label: $localize`Número` },
  { key: 'Date', label: $localize`Fecha` },
  { key: 'Select', label: $localize`Selección` },
  { key: 'MultiSelect', label: $localize`Selección múltiple` },
  { key: 'User', label: $localize`Usuario` },
  { key: 'Formula', label: $localize`Calculado` },
] as const;

/** Los que se calculan solos: ni se rellenan ni se pueden marcar obligatorios. */
export function isComputed(type: string): boolean {
  return type === 'Formula';
}

export const TARGET_ENTITIES = [
  { key: 'Task', label: $localize`Tareas` },
  { key: 'Project', label: $localize`Proyectos` },
] as const;

/** Separador de la selección múltiple. Lo fija el backend: un salto de línea. */
export const MULTI_SEPARATOR = '\n';

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
  private readonly byEntity = signal<Record<string, CustomFieldDefinition[]>>({});

  definitions(entity: string): CustomFieldDefinition[] {
    return this.byEntity()[entity] ?? [];
  }

  loadDefinitions(entity: string): Observable<CustomFieldDefinition[]> {
    return this.api.get<CustomFieldDefinition[]>('/custom-fields', { entity: entity }).pipe(
      tap(definitions => this.byEntity.update(actual => ({ ...actual, [entity]: definitions ?? [] })))
    );
  }

  valuesOf(entity: string, entityId: string): Observable<CustomFieldValue[]> {
    return this.api.get<CustomFieldValue[]>(`/custom-fields/values/${entity}/${entityId}`);
  }

  /**
   * Guarda un valor. El backend valida y normaliza: aquí no se transforma nada, porque dos
   * normalizaciones distintas —una en el navegador y otra en el servidor— acaban discrepando.
   */
  saveValue(definitionId: string, entityId: string, value: string | null): Observable<void> {
    return this.api.put<void>(`/custom-fields/values/${definitionId}/${entityId}`, { value }, SILENT);
  }

  define(field: Omit<CustomFieldDefinition, 'id'>): Observable<CustomFieldDefinition> {
    return this.api.post<CustomFieldDefinition>('/custom-fields', field, SILENT).pipe(
      tap(() => this.invalidate(field.targetEntity))
    );
  }

  update(id: string, entity: string, changes: Pick<CustomFieldDefinition, 'name' | 'isRequired' | 'options' | 'position' | 'formula'>): Observable<void> {
    return this.api.put<void>(`/custom-fields/${id}`, changes, SILENT).pipe(tap(() => this.invalidate(entity)));
  }

  remove(id: string, entity: string): Observable<void> {
    return this.api.delete<void>(`/custom-fields/${id}`, SILENT).pipe(tap(() => this.invalidate(entity)));
  }

  private invalidate(entity: string): void {
    this.byEntity.update(actual => {
      const copy = { ...actual };
      delete copy[entity];
      return copy;
    });
  }
}
