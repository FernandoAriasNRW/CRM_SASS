import { Injectable, inject, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, of, forkJoin } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ApiService } from '../../../core/api.service';
import { DEFAULT_NAV_ITEMS, type NavItem } from '../../../core/navigation-signal.store';
import { LANGUAGES, currentLanguage, urlInLanguage } from '../../../core/language';

export type CommandGroup = 'Ir a' | 'Acciones' | 'Proyectos' | 'Tareas' | 'Tickets';

/** Cómo se lee cada grupo. La clave se queda como está: es con lo que se agrupa. */
export function groupName(group: CommandGroup): string {
  switch (group) {
    case 'Ir a': return $localize`Ir a`;
    case 'Acciones': return $localize`Acciones`;
    case 'Proyectos': return $localize`Proyectos`;
    case 'Tareas': return $localize`Tareas`;
    case 'Tickets': return $localize`Tickets`;
  }
}

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

  readonly isOpen = signal(false);
  readonly query = signal('');
  readonly searching = signal(false);
  private readonly remote = signal<Command[]>([]);

  open(): void {
    this.query.set('');
    this.remote.set([]);
    this.isOpen.set(true);
  }

  close(): void {
    this.isOpen.set(false);
  }

  toggle(): void {
    if (this.isOpen()) {
      this.close();
    } else {
      this.open();
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
      run: () => void this.router.navigate(['/projects'], { queryParams: { create: 1 } }),
    },
    {
      id: 'accion-nueva-tarea',
      label: 'Nueva tarea',
      group: 'Acciones',
      icon: 'lucidePlus',
      keywords: 'crear añadir task',
      run: () => void this.router.navigate(['/tasks'], { queryParams: { create: 1 } }),
    },
    {
      id: 'accion-nuevo-ticket',
      label: 'Nuevo ticket',
      group: 'Acciones',
      icon: 'lucideTicket',
      keywords: 'crear añadir incidencia soporte',
      run: () => void this.router.navigate(['/tickets'], { queryParams: { create: 1 } }),
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
    // Un comando por idioma disponible, salvo el que ya se está usando.
    ...LANGUAGES.filter(i => i.code !== currentLanguage()).map(i => ({
      id: `accion-idioma-${i.code}`,
      label: $localize`Cambiar idioma a ${i.name}:idioma:`,
      group: 'Acciones' as const,
      icon: 'lucideLanguages',
      keywords: `language idioma ${i.code} ${i.name}`,
      // Navegación del navegador, no del router: cada idioma es una aplicación distinta
      // servida bajo su propio prefijo, así que hay que salir de esta.
      run: () => { window.location.href = urlInLanguage(i.code); },
    })),
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
  readonly results = computed<Command[]>(() => {
    const q = normalize(this.query());
    const estaticos = q
      ? this.estaticos().filter(c => normalize(`${c.label} ${c.keywords ?? ''}`).includes(q))
      : this.estaticos();

    return [...estaticos, ...this.remote()];
  });

  readonly grouped = computed(() => {
    const groups = new Map<CommandGroup, Command[]>();
    for (const c of this.results()) {
      (groups.get(c.group) ?? groups.set(c.group, []).get(c.group)!).push(c);
    }
    // El nombre que se lee se traduce; la clave del grupo no.
    //
    // `CommandGroup` es a la vez la clave con la que se agrupa y lo que se pintaba en pantalla,
    // así que en la versión inglesa salían «Proyectos» y «Tareas» en medio de todo lo demás.
    // Traducir la clave habría roto el agrupado; por eso van separados.
    return [...groups.entries()].map(([key, commands]) => ({
      key,
      name: groupName(key),
      commands
    }));
  });

  /**
   * Busca en proyectos, tareas y tickets a la vez.
   *
   * `forkJoin` con un `catchError` por petición: si un módulo falla o no está disponible,
   * los otros dos siguen dando resultados. Sin eso, un error en cualquiera dejaría el
   * paletón vacío y parecería que no hay nada que encontrar.
   */
  searchServer(term: string): void {
    if (term.trim().length < 2) {
      this.remote.set([]);
      return;
    }

    this.searching.set(true);
    const params = { search: term, page: 1, pageSize: 5 };

    forkJoin({
      projects: this.consulta$<{ id: string; name: string; status?: string }>('/projects', params),
      tasks: this.consulta$<{ id: string; title: string; status?: string }>('/tasks', params),
      tickets: this.consulta$<{ id: string; title: string; status?: string }>('/tickets', params),
    }).subscribe({
      next: ({ projects, tasks, tickets }) => {
        // Puede haber llegado tarde: si el término cambió mientras tanto, descartar.
        if (normalize(this.query()) !== normalize(term)) return;

        this.remote.set([
          ...projects.map(p => this.command('Proyectos', 'lucideFolderKanban', p.id, p.name, p.status, '/projects')),
          ...tasks.map(t => this.command('Tareas', 'lucideCheckSquare', t.id, t.title, t.status, '/tasks')),
          ...tickets.map(t => this.command('Tickets', 'lucideTicket', t.id, t.title, t.status, '/tickets')),
        ]);
        this.searching.set(false);
      },
      error: () => this.searching.set(false),
    });
  }

  private command(
    group: CommandGroup, icon: string, id: string,
    label: string, state: string | undefined, route: string,
  ): Command {
    return {
      id: `${group}-${id}`,
      label,
      group,
      icon,
      hint: state,
      run: () => void this.router.navigate([route], { queryParams: { id } }),
    };
  }

  private consulta$<T>(route: string, params: Record<string, string | number>): Observable<T[]> {
    return this.api.get<{ items: T[] }>(route, params).pipe(
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
function normalize(text: string): string {
  return text.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').trim();
}
