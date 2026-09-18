import { Node, mergeAttributes } from '@tiptap/core';
import type { Node as PmNode } from '@tiptap/pm/model';
import { TextSelection } from '@tiptap/pm/state';

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    columns: {
      /** Inserta un bloque de dos o tres columnas, cada una con un párrafo vacío. */
      insertColumns: (cantidad: 2 | 3) => ReturnType;
      /**
       * Deshace las columnas en las que está el cursor: su contenido queda seguido, una columna
       * detrás de otra, en vez de borrarse.
       */
      unsetColumns: () => ReturnType;
    };
  }
}

/**
 * Una columna. Sólo puede vivir dentro de un bloque de columnas.
 *
 * `isolating` impide que borrar al principio de una columna la fusione con la anterior: sin eso,
 * pulsar retroceso en una columna vacía se llevaba el texto de la de al lado.
 */
export const Column = Node.create({
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
export const Columns = Node.create({
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
      insertColumns: (cantidad: 2 | 3) => ({ chain, state }) => {
        const from = state.selection.from;

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
            let inside: number | null = null;

            tr.doc.nodesBetween(Math.max(0, from - 2), tr.doc.content.size, (node, pos) => {
              if (inside !== null) return false;
              if (node.type.name !== this.name) return true;

              // La apertura de columnas, la de la primera columna y la de su párrafo.
              inside = pos + 3;
              return false;
            });

            if (inside !== null) tr.setSelection(TextSelection.create(tr.doc, inside));
            return true;
          })
          .run();
      },

      unsetColumns: () => ({ state, tr, dispatch }) => {
        const { $from } = state.selection;

        // Se busca hacia arriba el bloque de columnas que contiene el cursor.
        for (let depth = $from.depth; depth > 0; depth--) {
          const node = $from.node(depth);
          if (node.type.name !== this.name) continue;

          const start = $from.before(depth);
          const end = $from.after(depth);

          const blocks: PmNode[] = [];
          node.forEach(column => column.forEach(block => { blocks.push(block); }));

          if (dispatch) {
            tr.replaceWith(start, end, blocks);
            dispatch(tr);
          }

          return true;
        }

        return false;
      }
    };
  }
});
