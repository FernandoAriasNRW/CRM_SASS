import { Extension } from '@tiptap/core';
import Suggestion from '@tiptap/suggestion';
import { getSuggestionItems, renderItems } from './suggestion';

export default Extension.create({
  name: 'slashCommand',

  addOptions() {
    return {
      suggestion: {
        char: '/',
        items: getSuggestionItems,
        render: renderItems,
        command: ({ editor, range, props }: any) => {
          props.command({ editor, range });
        },
      },
    };
  },

  addProseMirrorPlugins() {
    return [
      Suggestion({
        editor: this.editor,
        ...this.options.suggestion,
      }),
    ];
  },
});
