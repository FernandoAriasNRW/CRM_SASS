import { Extension } from '@tiptap/core';
import Suggestion from '@tiptap/suggestion';
import type { EditorCommand, PromptUrl, UploadFile } from './editor-commands';
import { getSuggestionItems, renderItems } from './suggestion';

interface MenuOptions {
  /**
   * Cómo se pide una dirección web.
   *
   * Se inyecta desde fuera, igual que el buscador de menciones: la extensión sabe insertar el
   * nodo y nada más. Antes esto era un `window.prompt`, que bloquea la pestaña entera y no se
   * puede dar estilo, y que además se sale del editor.
   */
  promptUrl: PromptUrl;

  /** Cómo se pide un fichero del ordenador y se sube. */
  uploadFile: UploadFile;
}

export default Extension.create<MenuOptions>({
  name: 'slashCommand',

  addOptions() {
    return {
      // De reserva: sin nada inyectado, los comandos que necesitan dirección no insertan nada en
      // vez de reventar. Que el editor funcione a medias es preferible a que no arranque.
      promptUrl: async () => null,
      uploadFile: async () => null
    };
  },

  addProseMirrorPlugins() {
    const promptUrl = this.options.promptUrl;
    const uploadFile = this.options.uploadFile;

    return [
      Suggestion({
        editor: this.editor,
        char: '/',
        items: getSuggestionItems,
        render: renderItems(promptUrl),
        command: ({ editor, range, props }) => {
          const command = props as unknown as EditorCommand;
          void command.run({ editor, range, promptUrl, uploadFile });
        }
      })
    ];
  }
});
