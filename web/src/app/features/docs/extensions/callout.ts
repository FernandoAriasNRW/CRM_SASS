import { Node, mergeAttributes } from '@tiptap/core';

/** Los tonos de aviso. La clave se guarda en el documento; el color lo pone la hoja de estilos. */
export const CALLOUT_TONES = ['nota', 'ojo', 'peligro', 'bien'] as const;
export type CalloutTone = typeof CALLOUT_TONES[number];

const EMOJI: Record<CalloutTone, string> = {
  nota: '💡',
  ojo: '⚠️',
  peligro: '🚫',
  bien: '✅'
};

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    callout: {
      /** Convierte el bloque actual en un aviso, o lo devuelve a párrafo si ya lo era. */
      toggleCallout: (tono?: CalloutTone) => ReturnType;
      /** Cambia el tono de un aviso ya existente. */
      setCalloutTone: (tono: CalloutTone) => ReturnType;
    };
  }
}

/**
 * El recuadro de «ojo con esto».
 *
 * Es el bloque que más se usa en cualquier procedimiento y no hay extensión oficial de TipTap que
 * lo traiga, así que se escribe aquí. Es poca cosa: un contenedor de bloques con un atributo.
 *
 * <b>El tono se guarda como una palabra, no como un color.</b> Guardar `#FBBF24` dejaría los
 * documentos viejos con el color de la paleta vieja para siempre, y en tema oscuro con un fondo
 * que no se puede leer. Con `ojo` guardado, el aspecto lo decide la hoja de estilos y cambia con
 * el tema.
 *
 * El emoji se pinta desde CSS —`::before`— y no como contenido: dentro del documento sería texto
 * de verdad, se podría borrar dejando el aviso sin icono, y viajaría a las exportaciones como un
 * carácter suelto delante del párrafo.
 */
export const Callout = Node.create({
  name: 'aviso',
  group: 'block',
  content: 'block+',
  defining: true,

  addAttributes() {
    return {
      tono: {
        default: 'nota' as CalloutTone,
        parseHTML: (element) => {
          const tono = element.getAttribute('data-tono');
          return CALLOUT_TONES.includes(tono as CalloutTone) ? tono : 'nota';
        },
        renderHTML: (attributes) => ({ 'data-tono': attributes['tono'] })
      }
    };
  },

  parseHTML() {
    return [{ tag: 'div[data-tipo="aviso"]' }];
  },

  renderHTML({ HTMLAttributes }) {
    return ['div', mergeAttributes(HTMLAttributes, { 'data-tipo': 'aviso', class: 'aviso' }), 0];
  },

  addCommands() {
    return {
      toggleCallout: (tono: CalloutTone = 'nota') => ({ commands }) =>
        commands.toggleWrap(this.name, { tono }),

      setCalloutTone: (tono: CalloutTone) => ({ commands }) =>
        commands.updateAttributes(this.name, { tono })
    };
  },

  addKeyboardShortcuts() {
    return {
      // Salir del aviso con Ctrl+Enter, como en cualquier bloque contenedor. Sin esto hay que
      // llegar al final y pulsar Enter dos veces, que nadie adivina.
      'Mod-Enter': () => this.editor.commands.exitCode()
    };
  }
});

/** El emoji de cada tono, para quien tenga que pintarlo fuera del editor. */
export function toneEmoji(tono: CalloutTone): string {
  return EMOJI[tono];
}
