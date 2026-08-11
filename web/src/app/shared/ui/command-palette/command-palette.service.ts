import { Injectable, inject, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, of, forkJoin } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ApiService } from '../../../core/api.service';
import { DEFAULT_NAV_ITEMS, type NavItem } from '../../../core/navigation-signal.store';

export type CommandGroup = 'Ir a' | 'Acciones' | 'Proyectos' | 'Tareas' | 'Tickets';

export interface Command {
  id: string;
  label: string;
  group: CommandGroup;
  icon: string;
  /** Texto adicional que también se busca: descripción, estado, etc. */
  keywords?: string;
  /** Pista a la derecha: atajo, estado del elemento… */
  hint?: string;
  run: () => void;
}

/**
 * Fuente de comandos del paletón (⌘K).
 *
 * Separa dos cosas que se comportan distinto:
 *
 * - Los comandos **estáticos** (navegación y acciones) se conocen de antemano y se
 *   filtran en memoria, así que responden en el mismo fotograma que la pulsación.
 * - Los **resultados de búsqueda** exigen ir al servidor. Llegan después y se añaden a
 *   los anteriores en lugar de sustituirlos, para que la lista nunca quede vacía
 *   mientras se espera: parpadear a "sin resultados" y volver a llenarse se percibe como
 *   lentitud aunque la respuesta sea rápida.
 */
@Injectable({ providedIn: 'root' })
export class CommandPaletteService {
  private readonly router = inject(Router);
  private readonly api = inject(ApiService);

  readonly abierto = signal(false);
  readonly consulta = signal('');
  readonly buscando = signal(false);
  private readonly remotos = signal<Command[]>([]);

  abrir(): void {
    this.consulta.set('');
    this.remotos.set([]);
    this.abierto.set(true);
  }

  cerrar(): void {
    this.abierto.set(false);
  }

  alternar(): void {
    if (this.abierto()) {
      this.cerrar();
    } else {
      this.abrir();
    }
  }

  /** Comandos estáticos: navegación a cada sección y acciones globales. */
  private readonly estaticos = computed<Command[]>(() => [
    ...DEFAULT_NAV_ITEMS.map((item: NavItem) => ({
      id: `nav-${item.id}`,
      label: item.label,
      group: 'Ir a' as const,
      icon: item.icon,
      keywords: item.route,
      run: () => void this.router.navigateByUrl(item.route),
    })),
    {
      id: 'accion-nuevo-proyecto',
      label: 'Nuevo proyecto',
      group: 'Acciones',
      icon: 'lucideFolderPlus',
      keywords: 'crear añadir project',
      run: () => void this.router.navigate(['/projects'], { queryParams: { nuevo: 1 } }),
    },
    {
      id: 'accion-nueva-tarea',
      label: 'Nueva tarea',
      group: 'Acciones',
      icon: 'lucidePlus',
      keywords: 'crear añadir task',
      run: () => void this.router.navigate(['/tasks'], { queryParams: { nuevo: 1 } }),
    },
    {
      id: 'accion-nuevo-ticket',
      label: 'Nuevo ticket',
      group: 'Acciones',
      icon: 'lucideTicket',
      keywords: 'crear añadir incidencia soporte',
      run: () => void this.router.navigate(['/tickets'], { queryParams: { nuevo: 1 } }),
    },
    {
      id: 'accion-mis-tareas',
      label: 'Mis tareas',
      group: 'Acciones',
      icon: 'lucideCheckSquare',
      keywords: 'asignadas mí',
      run: () => void this.router.navigate(['/tasks'], { queryParams: { filter: 'mine' } }),
    },
    {
      id: 'accion-perfil',
      label: 'Mi perfil',
      group: 'Acciones',
      icon: 'lucideUser',
      keywords: 'cuenta ajustes preferencias',
      run: () => void this.router.navigateByUrl('/profile'),
    },
    {
      id: 'accion-design-system',
      label: 'Sistema de diseño',
      group: 'Acciones',
      icon: 'lucidePalette',
      keywords: 'componentes guia estilos tokens color',
      run: () => void this.router.navigateByUrl('/design-system'),
    },
    {
      id: 'accion-tema',
      label: 'Cambiar tema claro / oscuro',
      group: 'Acciones',
      icon: 'lucideMoon',
      keywords: 'dark light modo oscuro claro',
      hint: 'Alterna',
      run: () => document.documentElement.classList.toggle('dark'),
    },
  ]);

  /**
   * Lista final: estáticos que casan, más lo que haya devuelto el servidor.
   *
   * El filtrado ignora acentos y mayúsculas —escribir "diseno" debe encontrar "Diseño"—
   * porque obligar a teclear el acento exacto rompe el flujo que justifica el paletón.
   */
  readonly resultados = computed<Command[]>(() => {
    const q = normalizar(this.consulta());
    const estaticos = q
      ? this.estaticos().filter(c => normalizar(`${c.label} ${c.keywords ?? ''}`).includes(q))
      : this.estaticos();

    return [...estaticos, ...this.remotos()];
  });

  readonly agrupados = computed(() => {
    const grupos = new Map<CommandGroup, Command[]>();
    for (const c of this.resultados()) {
      (grupos.get(c.group) ?? grupos.set(c.group, []).get(c.group)!).push(c);
    }
    return [...grupos.entries()].map(([nombre, comandos]) => ({ nombre, comandos }));
  });

  /**
   * Busca en proyectos, tareas y tickets a la vez.
   *
   * `forkJoin` con un `catchError` por petición: si un módulo falla o no está disponible,
   * los otros dos siguen dando resultados. Sin eso, un error en cualquiera dejaría el
   * paletón vacío y parecería que no hay nada que encontrar.
   */
  buscarEnServidor(termino: string): void {
    if (termino.trim().length < 2) {
      this.remotos.set([]);
      return;
    }

    this.buscando.set(true);
    const params = { search: termino, page: 1, pageSize: 5 };

    forkJoin({
      proyectos: this.consulta$<{ id: string; name: string; status?: string }>('/projects', params),
      tareas: this.consulta$<{ id: string; title: string; status?: string }>('/tasks', params),
      tickets: this.consulta$<{ id: string; title: string; status?: string }>('/tickets', params),
    }).subscribe({
      next: ({ proyectos, tareas, tickets }) => {
        // Puede haber llegado tarde: si el término cambió mientras tanto, descartar.
        if (normalizar(this.consulta()) !== normalizar(termino)) return;

        this.remotos.set([
          ...proyectos.map(p => this.comando('Proyectos', 'lucideFolderKanban', p.id, p.name, p.status, '/projects')),
          ...tareas.map(t => this.comando('Tareas', 'lucideCheckSquare', t.id, t.title, t.status, '/tasks')),
          ...tickets.map(t => this.comando('Tickets', 'lucideTicket', t.id, t.title, t.status, '/tickets')),
        ]);
        this.buscando.set(false);
      },
      error: () => this.buscando.set(false),
    });
  }

  private comando(
    group: CommandGroup, icon: string, id: string,
    label: string, estado: string | undefined, ruta: string,
  ): Command {
    return {
      id: `${group}-${id}`,
      label,
      group,
      icon,
      hint: estado,
      run: () => void this.router.navigate([ruta], { queryParams: { id } }),
    };
  }

  private consulta$<T>(ruta: string, params: Record<string, string | number>): Observable<T[]> {
    return this.api.get<{ items: T[] }>(ruta, params).pipe(
      map(r => r.items ?? []),
      catchError(() => of([])),
    );
  }
}

/**
 * Minúsculas y sin acentos, para que la búsqueda no dependa de teclearlos.
 *
 * separa cada letra acentuada en letra + marca combinante, y el rango \u0300-\u036f
 * elimina esas marcas. Se escribe con escapes y no con los caracteres literales porque
 * son invisibles en el editor y cualquiera podría borrarlos sin darse cuenta.
 */
function normalizar(texto: string): string {
  return texto.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').trim();
}
