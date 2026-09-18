import { Mark, mergeAttributes } from '@tiptap/core';

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    comment: {
      /** Marca lo seleccionado como comentado, con el identificador de su anotación. */
      setComment: (anotacionId: string) => ReturnType;
      /** Quita la marca del comentario indicado, esté donde esté el cursor. */
      unsetComment: (anotacionId: string) => ReturnType;
    };
  }
}

/**
 * La marca de un comentario en línea.
 *
 * Un comentario en línea es <b>una marca sobre un rango con un identificador</b>. No guarda el
 * texto del comentario ni quién lo escribió: sólo dice «aquí hay una conversación, y se llama
 * así». El hilo vive en el módulo Comments y el anclaje en Docs.
 *
 * <b>Se puede solapar.</b> `inclusive: false` evita que lo que se escriba justo detrás de un
 * fragmento comentado herede la marca, que es cómo un comentario sobre tres palabras acaba
 * abarcando el párrafo entero sin que nadie lo pida.
 *
 * La marca viaja dentro del HTML del documento, así que un comentario sobrevive a cerrar y abrir
 * la página. Si alguien borra el texto marcado, la marca se va con él y la anotación se queda sin
 * sitio: por eso la anotación guarda una copia del texto citado y el panel la puede seguir
 * enseñando como huérfana en vez de desaparecer sin explicación.
 */
export const CommentMark = Mark.create({
  name: 'comentario',
  inclusive: false,

  // Dos comentarios sobre trozos que se cruzan son normales en una revisión, así que la marca
  // no excluye a otras de su mismo tipo.
  excludes: '',

  addAttributes() {
    return {
      anotacionId: {
        default: null,
        parseHTML: (element) => element.getAttribute('data-anotacion'),
        renderHTML: (attributes) =>
          attributes['anotacionId'] ? { 'data-anotacion': attributes['anotacionId'] } : {}
      }
    };
  },

  parseHTML() {
    return [{ tag: 'span[data-anotacion]' }];
  },

  renderHTML({ HTMLAttributes }) {
    return ['span', mergeAttributes(HTMLAttributes, { class: 'comentado' }), 0];
  },

  addCommands() {
    return {
      setComment: (anotacionId: string) => ({ commands }) =>
        commands.setMark(this.name, { anotacionId }),

      unsetComment: (anotacionId: string) => ({ tr, state, dispatch }) => {
        const markType = state.schema.marks[this.name];
        if (!markType) return false;

        let found = false;

        // Se recorre el documento en vez de usar `unsetMark`, que actúa sobre la selección: el
        // botón de quitar está en el panel lateral, donde el cursor no tiene por qué estar dentro
        // del fragmento comentado.
        state.doc.descendants((node, pos) => {
          if (!node.isText) return;

          const mark = node.marks.find(
            m => m.type === markType && m.attrs['anotacionId'] === anotacionId);

          if (!mark) return;

          tr.removeMark(pos, pos + node.nodeSize, mark);
          found = true;
        });

        if (found && dispatch) dispatch(tr);
        return found;
      }
    };
  }
});
