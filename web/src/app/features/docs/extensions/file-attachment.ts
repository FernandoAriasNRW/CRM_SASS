import { Node, mergeAttributes } from '@tiptap/core';

export const FileAttachment = Node.create({
  name: 'fileAttachment',

  group: 'block',

  atom: true,

  addAttributes() {
    return {
      href: {
        default: null,
      },
      title: {
        default: null,
      },
      fileType: {
        default: 'file', // pdf, csv, excel, etc
      },
    };
  },

  parseHTML() {
    return [
      {
        tag: 'a[data-type="file-attachment"]',
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    // We render a styled card for the attachment
    return [
      'a',
      mergeAttributes(HTMLAttributes, {
        'data-type': 'file-attachment',
        class: 'flex items-center gap-3 p-3 border border-zinc-200 dark:border-zinc-700 rounded-lg bg-zinc-50 dark:bg-zinc-800/50 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors no-underline text-zinc-900 dark:text-zinc-100 my-2 cursor-pointer',
        target: '_blank',
        rel: 'noopener noreferrer'
      }),
      ['span', { class: 'text-2xl' }, '📄'], // Icon
      ['span', { class: 'font-medium' }, HTMLAttributes['title'] || 'Attachment']
    ];
  },
});
