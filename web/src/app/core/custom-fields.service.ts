import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';

/** Los tipos que define el backend. Cambiar esta lista sin cambiar allí no sirve de nada. */
export const TIPOS_DE_CAMPO = [
  { key: 'Texto', label: $localize`Texto` },
  { key: 'Numero', label: $localize`Número` },
  { key: 'Fecha', label: $localize`Fecha` },
  { key: 'Seleccion', label: $localize`Selección` },
  { key: 'SeleccionMultiple', label: $localize`Selección múltiple` },
  { key: 'Usuario', label: $localize`Usuario` },
] as const;

export const ENTIDADES = [
  { key: 'Tarea', label: $localize`Tareas` },
  { key: 'Proyecto', label: $localize`Proyectos` },
] as const;

/** Separador de la selección múltiple. Lo fija el backend: un salto de línea. */
export const SEPARADOR_MULTIPLE = '\n';

/**
 * Saca el mensaje que explica por qué el servidor rechazó algo.
 *
 * Los endpoints de campos personalizados devuelven `BadRequest(result.Error)` —una cadena suelta—
 * y el manejador global de excepciones devuelve `ProblemDetails`. Hay que contemplar los dos: si
 * sólo se mira uno, la mitad de los rechazos se convierten en el mensaje genérico y se pierde la
 * explicación del dominio, que es justo lo que hace falta leer.
 */
export function mensajeDeError(respuesta: unknown, porDefecto: string): string {
  const cuerpo = (respuesta as { error?: unknown })?.error;

  if (typeof cuerpo === 'string' && cuerpo.trim()) return cuerpo;

  const detalle = (cuerpo as { detail?: string } | undefined)?.detail;
  return detalle?.trim() ? detalle : porDefecto;
}

export interface CustomFieldDefinition {
  id: string;
  nombre: string;
  tipo: string;
  entidadDestino: string;
  obligatorio: boolean;
  opciones: string[];
  posicion: number;
}

/** Un campo con su valor para una entidad concreta. */
export interface CustomFieldValue {
  definitionId: string;
  nombre: string;
  tipo: string;
  obligatorio: boolean;
  opciones: string[];
  posicion: number;
  valor: string | null;
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
    return this.api.get<CustomFieldDefinition[]>('/custom-fields', { entidad }).pipe(
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
  guardarValor(definitionId: string, entityId: string, valor: string | null): Observable<void> {
    return this.api.put<void>(`/custom-fields/values/${definitionId}/${entityId}`, { valor });
  }

  definir(campo: Omit<CustomFieldDefinition, 'id'>): Observable<CustomFieldDefinition> {
    return this.api.post<CustomFieldDefinition>('/custom-fields', campo).pipe(
      tap(() => this.invalidar(campo.entidadDestino))
    );
  }

  actualizar(id: string, entidad: string, cambios: Pick<CustomFieldDefinition, 'nombre' | 'obligatorio' | 'opciones' | 'posicion'>): Observable<void> {
    return this.api.put<void>(`/custom-fields/${id}`, cambios).pipe(tap(() => this.invalidar(entidad)));
  }

  borrar(id: string, entidad: string): Observable<void> {
    return this.api.delete<void>(`/custom-fields/${id}`).pipe(tap(() => this.invalidar(entidad)));
  }

  private invalidar(entidad: string): void {
    this.porEntidad.update(actual => {
      const copia = { ...actual };
      delete copia[entidad];
      return copia;
    });
  }
}
