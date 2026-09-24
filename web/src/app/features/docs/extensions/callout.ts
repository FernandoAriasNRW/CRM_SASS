import { Node, mergeAttributes } from '@tiptap/core';

/**
 * Los tonos de aviso. La clave se guarda en el documento; el color lo pone la hoja de estilos.
 * Las plantillas predefinidas del servidor (`BuiltInTemplates`) escriben estas mismas claves.
 */
export const CALLOUT_TONES = ['note', 'warning', 'danger', 'success'] as const;
export type CalloutTone = typeof CALLOUT_TONES[number];

const EMOJI: Record<CalloutTone, string> = {
  note: '💡',
  warning: '⚠️',
  danger: '🚫',
  success: '✅'
};

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    callout: {
      /** Convierte el bloque actual en un aviso, o lo devuelve a párrafo si ya lo era. */
      toggleCallout: (tone?: CalloutTone) => ReturnType;
      /** Cambia el tono de un aviso ya existente. */
      setCalloutTone: (tone: CalloutTone) => ReturnType;
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
 * que no se puede leer. Con `warning` guardado, el aspecto lo decide la hoja de estilos y cambia con
 * el tema.
 *
 * El emoji se pinta desde CSS —`::before`— y no como contenido: dentro del documento sería texto
 * de verdad, se podría borrar dejando el aviso sin icono, y viajaría a las exportaciones como un
 * carácter suelto delante del párrafo.
 */
export const Callout = Node.create({
  name: 'callout',
  group: 'block',
  content: 'block+',
  defining: true,

  addAttributes() {
    return {
      tone: {
        default: 'note' as CalloutTone,
        parseHTML: (element) => {
          const tone = element.getAttribute('data-tone');
          return CALLOUT_TONES.includes(tone as CalloutTone) ? tone : 'note';
        },
        renderHTML: (attributes) => ({ 'data-tone': attributes['tone'] })
      }
    };
  },

  parseHTML() {
    return [{ tag: 'div[data-type="callout"]' }];
  },

  renderHTML({ HTMLAttributes }) {
    return ['div', mergeAttributes(HTMLAttributes, { 'data-type': 'callout', class: 'callout' }), 0];
  },

  addCommands() {
    return {
      toggleCallout: (tone: CalloutTone = 'note') => ({ commands }) =>
        commands.toggleWrap(this.name, { tone }),

      setCalloutTone: (tone: CalloutTone) => ({ commands }) =>
        commands.updateAttributes(this.name, { tone })
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
export function toneEmoji(tone: CalloutTone): string {
  return EMOJI[tone];
}
