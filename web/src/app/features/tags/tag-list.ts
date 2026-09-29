import { type TagItem } from '../../shared/services/tags.service';
import { type TableState } from '../../shared/ui/data-table/data-table.component';

/**
 * La lógica de la lista de etiquetas, aparte del componente para poder probarla.
 *
 * Se filtra, ordena y pagina en el navegador, no en el servidor: una organización tiene decenas
 * de etiquetas, `GET /tags` las devuelve todas, y las fichas de tarea y ticket ya las tienen
 * cargadas. Pedir cada página al servidor sería más lento y no ahorraría nada.
 */

/** De dónde sale una etiqueta. Decide si se puede tocar y qué explica el drawer. */
export type TagKind = 'builtIn' | 'automatic' | 'custom';

/** Las categorías que rellena el sistema. Coincide con `TagCategory.IsAutomatic` del backend. */
export const AUTOMATIC_CATEGORIES: readonly string[] = ['Team', 'Project'];

export function tagKind(tag: TagItem): TagKind {
  if (AUTOMATIC_CATEGORIES.includes(tag.category)) return 'automatic';
  return tag.builtInKey ? 'builtIn' : 'custom';
}

export function tagKindLabel(kind: TagKind): string {
  switch (kind) {
    case 'builtIn': return $localize`Predefinida`;
    case 'automatic': return $localize`Automática`;
    default: return $localize`Propia`;
  }
}

/** Una fila de la tabla: la etiqueta y lo que se deriva de ella para pintarla y ordenarla. */
export interface TagRow extends TagItem {
  kind: TagKind;
  kindLabel: string;
}

export function toRows(tags: readonly TagItem[]): TagRow[] {
  return tags.map(tag => {
    const kind = tagKind(tag);
    return { ...tag, kind, kindLabel: tagKindLabel(kind) };
  });
}

/** Las columnas por las que se puede ordenar, y qué valor compara cada una. */
const SORT_VALUES: Record<string, (row: TagRow) => string> = {
  name: row => row.name,
  categoryLabel: row => row.categoryLabel,
  kind: row => row.kindLabel,
};

/** Busca en nombre y categoría, sin distinguir mayúsculas ni tildes. */
export function filterRows(rows: readonly TagRow[], term: string | undefined): TagRow[] {
  const wanted = normalize(term ?? '');
  if (!wanted) return [...rows];
  return rows.filter(row => normalize(`${row.name} ${row.categoryLabel}`).includes(wanted));
}

/** Ordena por la columna pedida y, a igualdad, por nombre. Una columna desconocida no reordena. */
export function sortRows(rows: readonly TagRow[], column: string | undefined, direction: 'asc' | 'desc' = 'asc'): TagRow[] {
  const value = column ? SORT_VALUES[column] : undefined;
  if (!value) return [...rows];

  const sign = direction === 'desc' ? -1 : 1;
  return [...rows].sort((a, b) =>
    sign * value(a).localeCompare(value(b), undefined, { sensitivity: 'base' })
    || a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
}

/** Lo que enseña la tabla con su estado: filtrado y ordenado, y el trozo de la página. */
export function visibleRows(rows: readonly TagRow[], state: TableState): { page: TagRow[]; total: number } {
  const sorted = sortRows(filterRows(rows, state.searchTerm), state.sortColumn, state.sortDirection);
  const start = (Math.max(state.page, 1) - 1) * state.pageSize;
  return { page: sorted.slice(start, start + state.pageSize), total: sorted.length };
}

function normalize(text: string): string {
  return text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();
}
