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
        class: 'flex items-center gap-3 p-3 border border-border dark:border-border rounded-lg bg-muted dark:bg-muted/50 hover:bg-muted dark:hover:bg-muted transition-colors no-underline text-foreground dark:text-foreground my-2 cursor-pointer',
        target: '_blank',
        rel: 'noopener noreferrer'
      }),
      ['span', { class: 'text-2xl' }, '📄'], // Icon
      ['span', { class: 'font-medium' }, HTMLAttributes['title'] || 'Attachment']
    ];
  },
});
