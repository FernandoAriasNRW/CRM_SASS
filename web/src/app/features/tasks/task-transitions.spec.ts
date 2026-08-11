import { puedeMover, TRANSICIONES_VALIDAS } from './task-transitions';

/**
 * Estas reglas replican las del dominio (`TaskStatus.GetValidTransitions`). Si allí
 * cambian y aquí no, el tablero permitirá movimientos que el servidor rechace: la
 * reversión los deshará, pero el usuario verá la tarjeta ir y volver.
 *
 * Las cuatro combinaciones prohibidas de abajo son exactamente las que un usuario puede
 * intentar arrastrando entre las columnas del tablero, y las que antes se daban por
 * buenas en pantalla.
 */
describe('transiciones del tablero', () => {
  describe('permitidas', () => {
    const casos: [string, string][] = [
      ['To Do', 'In Progress'],
      ['To Do', 'Done'],
      ['In Progress', 'In Review'],
      ['In Progress', 'Done'],
      ['In Review', 'In Progress'],
      ['In Review', 'Done'],
      ['Done', 'To Do'],
    ];

    for (const [desde, hasta] of casos) {
      it(`${desde} → ${hasta}`, () => expect(puedeMover(desde, hasta)).toBeTrue());
    }
  });

  describe('rechazadas', () => {
    const casos: [string, string][] = [
      ['To Do', 'In Review'],
      ['In Review', 'To Do'],
      ['Done', 'In Progress'],
      ['Done', 'In Review'],
    ];

    for (const [desde, hasta] of casos) {
      it(`${desde} → ${hasta}`, () => expect(puedeMover(desde, hasta)).toBeFalse());
    }
  });

  it('soltar en la misma columna siempre vale', () => {
    for (const estado of Object.keys(TRANSICIONES_VALIDAS)) {
      expect(puedeMover(estado, estado)).toBeTrue();
    }
  });

  it('un estado desconocido se deja pasar, para que decida el servidor', () => {
    // Prohibirlo bloquearía el tablero en cuanto el dominio añada un estado nuevo,
    // que es peor que dejar que el servidor lo rechace y revertir.
    expect(puedeMover('Archivada', 'Done')).toBeTrue();
  });
});
