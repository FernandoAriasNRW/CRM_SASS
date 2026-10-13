/**
 * La rejilla del panel: dónde va cada recuadro y cómo se recolocan los demás al mover uno.
 *
 * Son funciones puras, sin Angular, para poder probarlas con casos concretos: la lógica de
 * colocar es la parte que se rompe en silencio —un solape no da error, sólo se ve mal—.
 *
 * Los límites son los que valida el servidor (`Widget.Validate`): si la pantalla dejara colocar
 * algo que el servidor rechaza, el arrastre parecería funcionar y al recargar volvería a su sitio.
 */

/** Un recuadro tal como lo guarda el servidor. Se reenvía entero: el PUT sustituye la disposición. */
export interface Placement {
  id: string;
  reportId: string;
  x: number;
  y: number;
  width: number;
  height: number;
  visualization?: string | null;
  title?: string | null;
}

export const GRID = {
  columns: 12,
  minWidth: 3,
  minHeight: 2,
  maxHeight: 12,
} as const;

/** Ajusta un recuadro a la rejilla: ni más estrecho, ni más alto, ni fuera de las columnas. */
export function clampToGrid(p: Placement): Placement {
  const width = Math.min(GRID.columns, Math.max(GRID.minWidth, Math.round(p.width)));
  const height = Math.min(GRID.maxHeight, Math.max(GRID.minHeight, Math.round(p.height)));
  const x = Math.min(GRID.columns - width, Math.max(0, Math.round(p.x)));
  const y = Math.max(0, Math.round(p.y));
  return { ...p, x, y, width, height };
}

export function overlaps(a: Placement, b: Placement): boolean {
  return a.x < b.x + b.width && b.x < a.x + a.width
      && a.y < b.y + b.height && b.y < a.y + a.height;
}

/**
 * Coloca uno donde se pide y recoloca el resto.
 *
 * El que se mueve manda: se queda donde se soltó. Los demás, por orden de arriba abajo, suben
 * todo lo que pueden sin pisar a nadie; así no quedan huecos y nadie acaba encima de otro. Al
 * final el movido también sube si tiene hueco encima, para que soltarlo muy abajo no deje una
 * franja vacía.
 */
export function settle(layout: readonly Placement[], movedId: string): Placement[] {
  const moved = layout.find(p => p.id === movedId);
  if (!moved) return [...layout];

  const placed: Placement[] = [clampToGrid(moved)];
  const others = layout
    .filter(p => p.id !== movedId)
    .map(clampToGrid)
    .sort((a, b) => a.y - b.y || a.x - b.x);

  for (const widget of others) {
    let candidate = { ...widget, y: 0 };
    while (placed.some(p => overlaps(candidate, p))) candidate = { ...candidate, y: candidate.y + 1 };
    placed.push(candidate);
  }

  const rest = placed.slice(1);
  let first = placed[0];
  while (first.y > 0 && !rest.some(p => overlaps({ ...first, y: first.y - 1 }, p))) first = { ...first, y: first.y - 1 };

  // El orden de salida es el de entrada: la pantalla los pinta en ese orden y el foco del teclado
  // no debe saltar a otro recuadro porque uno se haya movido.
  const byId = new Map([first, ...rest].map(p => [p.id, p]));
  return layout.map(p => byId.get(p.id)!);
}

/** Mueve un recuadro a una columna y fila, y recoloca el resto. */
export function moveTo(layout: readonly Placement[], id: string, x: number, y: number): Placement[] {
  return settle(layout.map(p => p.id === id ? { ...p, x, y } : p), id);
}

/** Cambia el tamaño de un recuadro, y recoloca el resto. */
export function resizeTo(layout: readonly Placement[], id: string, width: number, height: number): Placement[] {
  return settle(layout.map(p => p.id === id ? { ...p, width, height } : p), id);
}

/** Si dos disposiciones colocan igual cada recuadro: para no guardar cuando no ha cambiado nada. */
export function sameLayout(a: readonly Placement[], b: readonly Placement[]): boolean {
  return a.length === b.length && a.every(p => {
    const q = b.find(x => x.id === p.id);
    return !!q && q.x === p.x && q.y === p.y && q.width === p.width && q.height === p.height;
  });
}
