/**
 * Transiciones válidas del tablero, replicadas del dominio (`TaskStatus`).
 *
 * Sirve sólo para dar respuesta inmediata mientras se arrastra: bloquear la columna a la
 * que no se puede soltar es mucho mejor que dejar caer la tarjeta y devolverla medio
 * segundo después.
 *
 * **No es la autoridad.** La decisión la sigue tomando el servidor, y el tablero revierte
 * si rechaza el movimiento. Esa reversión es también la red que cubre esta duplicación:
 * si esta tabla se queda atrás respecto al dominio, el peor caso es que permita un
 * movimiento que el servidor rechace y se deshaga solo. Por eso, ante la duda conviene
 * ser permisivo aquí: prohibir de más bloquearía al usuario sin que el servidor tenga
 * nada que objetar.
 *
 * Si las transiciones cambian a menudo, lo correcto es que la API las exponga y esta
 * tabla desaparezca.
 */
export const TRANSICIONES_VALIDAS: Readonly<Record<string, readonly string[]>> = {
  'To Do': ['In Progress', 'Done'],
  'In Progress': ['To Do', 'In Review', 'Done', 'On Hold'],
  'In Review': ['In Progress', 'Done'],
  'Done': ['To Do'],
  'On Hold': ['To Do', 'In Progress'],
};

export function puedeMover(desde: string, hasta: string): boolean {
  if (desde === hasta) return true;

  const permitidas = TRANSICIONES_VALIDAS[desde];

  // Un estado que no conocemos se deja pasar: que decida el servidor. Bloquearlo aquí
  // impediría mover tareas en cuanto el dominio añada un estado nuevo.
  return permitidas ? permitidas.includes(hasta) : true;
}
