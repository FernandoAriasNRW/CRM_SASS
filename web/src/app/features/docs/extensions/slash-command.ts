import { Extension } from '@tiptap/core';
import Suggestion from '@tiptap/suggestion';
import type { ComandoDelEditor, PedirUrl, SubirFichero } from './comandos-del-editor';
import { getSuggestionItems, renderItems } from './suggestion';

interface OpcionesDelMenu {
  /**
   * Cómo se pide una dirección web.
   *
   * Se inyecta desde fuera, igual que el buscador de menciones: la extensión sabe insertar el
   * nodo y nada más. Antes esto era un `window.prompt`, que bloquea la pestaña entera y no se
   * puede dar estilo, y que además se sale del editor.
   */
  pedirUrl: PedirUrl;

  /** Cómo se pide un fichero del ordenador y se sube. */
  subirFichero: SubirFichero;
}

export default Extension.create<OpcionesDelMenu>({
  name: 'slashCommand',

  addOptions() {
    return {
      // De reserva: sin nada inyectado, los comandos que necesitan dirección no insertan nada en
      // vez de reventar. Que el editor funcione a medias es preferible a que no arranque.
      pedirUrl: async () => null,
      subirFichero: async () => null
    };
  },

  addProseMirrorPlugins() {
    const pedirUrl = this.options.pedirUrl;
    const subirFichero = this.options.subirFichero;

    return [
      Suggestion({
        editor: this.editor,
        char: '/',
        items: getSuggestionItems,
        render: renderItems(pedirUrl),
        command: ({ editor, range, props }) => {
          const comando = props as unknown as ComandoDelEditor;
          void comando.ejecutar({ editor, range, pedirUrl, subirFichero });
        }
      })
    ];
  }
});
