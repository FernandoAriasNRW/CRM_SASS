import { Node, mergeAttributes } from '@tiptap/core';
import type { Node as NodoPM } from '@tiptap/pm/model';
import { TextSelection } from '@tiptap/pm/state';

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    columnas: {
      /** Inserta un bloque de dos o tres columnas, cada una con un párrafo vacío. */
      insertarColumnas: (cantidad: 2 | 3) => ReturnType;
      /**
       * Deshace las columnas en las que está el cursor: su contenido queda seguido, una columna
       * detrás de otra, en vez de borrarse.
       */
      deshacerColumnas: () => ReturnType;
    };
  }
}

/**
 * Una columna. Sólo puede vivir dentro de un bloque de columnas.
 *
 * `isolating` impide que borrar al principio de una columna la fusione con la anterior: sin eso,
 * pulsar retroceso en una columna vacía se llevaba el texto de la de al lado.
 */
export const Columna = Node.create({
  name: 'columna',
  content: 'block+',
  isolating: true,
  defining: true,

  parseHTML() {
    return [{ tag: 'div[data-tipo="columna"]' }];
  },

  renderHTML({ HTMLAttributes }) {
    return ['div', mergeAttributes(HTMLAttributes, { 'data-tipo': 'columna', class: 'columna' }), 0];
  },
});

/**
 * Dos o tres columnas lado a lado.
 *
 * <b>Era el bloque más caro de la auditoría y el que menos se usa</b>, así que se limita a lo que
 * de verdad hace falta: dos o tres columnas de ancho igual. Nada de columnas redimensionables ni
 * anidadas —una columna dentro de otra no cabe en la pantalla de un portátil—, que es donde se
 * va el coste de esta pieza en otros editores.
 *
 * <b>Deshacer no borra.</b> Quitar las columnas deja su contenido seguido, una detrás de otra. Lo
 * contrario —borrar el bloque— se lleva por delante el texto de todas las columnas por querer
 * cambiar sólo cómo se colocan.
 *
 * En pantallas estrechas se apilan: tres columnas en un móvil son tres tiras ilegibles.
 */
export const Columnas = Node.create({
  name: 'columnas',
  group: 'block',
  content: 'columna{2,3}',
  isolating: true,
  defining: true,

  addAttributes() {
    return {
      cantidad: {
        default: 2,
        parseHTML: (element) => (element.getAttribute('data-cantidad') === '3' ? 3 : 2),
        renderHTML: (attributes) => ({ 'data-cantidad': String(attributes['cantidad']) })
      }
    };
  },

  parseHTML() {
    return [{ tag: 'div[data-tipo="columnas"]' }];
  },

  renderHTML({ HTMLAttributes }) {
    return ['div', mergeAttributes(HTMLAttributes, { 'data-tipo': 'columnas', class: 'columnas' }), 0];
  },

  addCommands() {
    return {
      insertarColumnas: (cantidad: 2 | 3) => ({ chain, state }) => {
        const desde = state.selection.from;

        return chain()
          .insertContent({
            type: this.name,
            attrs: { cantidad },
            content: Array.from({ length: cantidad }, () => ({
              type: 'columna',
              content: [{ type: 'paragraph' }]
            }))
          })
          // El cursor entra en la primera columna. Sin esto se quedaba **detrás** del bloque: se
          // insertaban las columnas y lo siguiente que se escribía caía fuera de ellas, así que
          // había que ir con el ratón a la primera antes de poder usarlas.
          //
          // Se busca el bloque recién insertado en vez de deducirlo de dónde quedó el cursor: si se
          // escribe «/columnas» a mitad de un párrafo, el párrafo se parte en dos y el cursor acaba
          // en la segunda mitad, no justo detrás de las columnas.
          .command(({ tr }) => {
            let dentro: number | null = null;

            tr.doc.nodesBetween(Math.max(0, desde - 2), tr.doc.content.size, (nodo, pos) => {
              if (dentro !== null) return false;
              if (nodo.type.name !== this.name) return true;

              // La apertura de columnas, la de la primera columna y la de su párrafo.
              dentro = pos + 3;
              return false;
            });

            if (dentro !== null) tr.setSelection(TextSelection.create(tr.doc, dentro));
            return true;
          })
          .run();
      },

      deshacerColumnas: () => ({ state, tr, dispatch }) => {
        const { $from } = state.selection;

        // Se busca hacia arriba el bloque de columnas que contiene el cursor.
        for (let profundidad = $from.depth; profundidad > 0; profundidad--) {
          const nodo = $from.node(profundidad);
          if (nodo.type.name !== this.name) continue;

          const inicio = $from.before(profundidad);
          const fin = $from.after(profundidad);

          const bloques: NodoPM[] = [];
          nodo.forEach(columna => columna.forEach(bloque => { bloques.push(bloque); }));

          if (dispatch) {
            tr.replaceWith(inicio, fin, bloques);
            dispatch(tr);
          }

          return true;
        }

        return false;
      }
    };
  }
});
