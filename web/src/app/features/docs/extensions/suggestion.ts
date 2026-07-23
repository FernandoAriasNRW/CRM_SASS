// We are in Angular, so we can't use VueRenderer or ReactRenderer easily.
// Instead, we will use vanilla JS DOM elements with Tippy for the suggestion menu.
import tippy from 'tippy.js';

export const getSuggestionItems = ({ query }: { query: string }) => {
  return [
    {
      title: 'Heading 1',
      command: ({ editor, range }: any) => {
        editor.chain().focus().deleteRange(range).setNode('heading', { level: 1 }).run();
      },
    },
    {
      title: 'Heading 2',
      command: ({ editor, range }: any) => {
        editor.chain().focus().deleteRange(range).setNode('heading', { level: 2 }).run();
      },
    },
    {
      title: 'Heading 3',
      command: ({ editor, range }: any) => {
        editor.chain().focus().deleteRange(range).setNode('heading', { level: 3 }).run();
      },
    },
    {
      title: 'Bullet List',
      command: ({ editor, range }: any) => {
        editor.chain().focus().deleteRange(range).toggleBulletList().run();
      },
    },
    {
      title: 'Numbered List',
      command: ({ editor, range }: any) => {
        editor.chain().focus().deleteRange(range).toggleOrderedList().run();
      },
    },
    {
      title: 'Image',
      command: ({ editor, range }: any) => {
        const url = window.prompt('Image URL');
        if (url) {
          editor.chain().focus().deleteRange(range).setImage({ src: url }).run();
        }
      },
    },
    {
      title: 'Table',
      command: ({ editor, range }: any) => {
        editor.chain().focus().deleteRange(range).insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run();
      },
    },
    {
      title: 'Task List',
      command: ({ editor, range }: any) => {
        editor.chain().focus().deleteRange(range).toggleTaskList().run();
      },
    },
    {
      title: 'Code Block',
      command: ({ editor, range }: any) => {
        editor.chain().focus().deleteRange(range).toggleCodeBlock().run();
      },
    },
    {
      title: 'Quote',
      command: ({ editor, range }: any) => {
        editor.chain().focus().deleteRange(range).toggleBlockquote().run();
      },
    },
    {
      title: 'Divider',
      command: ({ editor, range }: any) => {
        editor.chain().focus().deleteRange(range).setHorizontalRule().run();
      },
    },
    {
      title: 'YouTube Video',
      command: ({ editor, range }: any) => {
        const url = window.prompt('YouTube URL');
        if (url) {
          editor.chain().focus().deleteRange(range).setYoutubeVideo({ src: url }).run();
        }
      },
    }
  ].filter(item => item.title.toLowerCase().startsWith(query.toLowerCase())).slice(0, 10);
};

export const renderItems = () => {
  let component: HTMLElement;
  let popup: any;

  return {
    onStart: (props: any) => {
      component = document.createElement('div');
      component.classList.add('slash-menu');
      component.style.background = 'white';
      component.style.border = '1px solid #e4e4e7';
      component.style.borderRadius = '0.5rem';
      component.style.boxShadow = '0 10px 15px -3px rgb(0 0 0 / 0.1)';
      component.style.padding = '4px';
      component.style.display = 'flex';
      component.style.flexDirection = 'column';
      component.style.gap = '2px';
      component.style.minWidth = '200px';

      const updateItems = (items: any[]) => {
        component.innerHTML = '';
        if (!items.length) {
          component.innerHTML = '<div style="padding: 4px 8px; color: #71717a; font-size: 14px;">No results</div>';
          return;
        }

        items.forEach((item, index) => {
          const btn = document.createElement('button');
          btn.textContent = item.title;
          btn.style.padding = '8px';
          btn.style.textAlign = 'left';
          btn.style.background = 'transparent';
          btn.style.border = 'none';
          btn.style.cursor = 'pointer';
          btn.style.borderRadius = '0.25rem';
          btn.style.fontSize = '14px';
          btn.style.color = '#18181b';
          
          btn.addEventListener('mouseenter', () => {
            btn.style.background = '#f4f4f5';
          });
          btn.addEventListener('mouseleave', () => {
            btn.style.background = 'transparent';
          });
          btn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            item.command(props);
            popup[0].hide();
          });
          
          component.appendChild(btn);
        });
      };

      updateItems(props.items);

      popup = tippy('body', {
        getReferenceClientRect: props.clientRect,
        appendTo: () => document.body,
        content: component,
        showOnCreate: true,
        interactive: true,
        trigger: 'manual',
        placement: 'bottom-start',
      });
    },

    onUpdate(props: any) {
      if (!props.clientRect) {
        return;
      }
      
      const component = popup[0].props.content;
      component.innerHTML = '';
      if (!props.items.length) {
        component.innerHTML = '<div style="padding: 4px 8px; color: #71717a; font-size: 14px;">No results</div>';
      } else {
        props.items.forEach((item: any, index: number) => {
          const btn = document.createElement('button');
          btn.textContent = item.title;
          btn.style.padding = '8px';
          btn.style.textAlign = 'left';
          btn.style.background = 'transparent';
          btn.style.border = 'none';
          btn.style.cursor = 'pointer';
          btn.style.borderRadius = '0.25rem';
          btn.style.fontSize = '14px';
          btn.style.color = '#18181b';
          
          btn.addEventListener('mouseenter', () => {
            btn.style.background = '#f4f4f5';
          });
          btn.addEventListener('mouseleave', () => {
            btn.style.background = 'transparent';
          });
          btn.addEventListener('click', () => {
            item.command(props);
            popup[0].hide();
          });
          
          component.appendChild(btn);
        });
      }

      popup[0].setProps({
        getReferenceClientRect: props.clientRect,
      });
    },

    onKeyDown(props: any) {
      if (props.event.key === 'Escape') {
        popup[0].hide();
        return true;
      }
      return false;
    },

    onExit() {
      popup[0].destroy();
    },
  };
};
