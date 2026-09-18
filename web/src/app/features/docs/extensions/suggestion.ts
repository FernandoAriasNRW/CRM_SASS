import type { Editor, Range } from '@tiptap/core';
import tippy, { type Instance } from 'tippy.js';
import { type EditorCommand, type PromptUrl, matchingCommands } from './editor-commands';

/**
 * Los comandos que casan con lo escrito detrás de la barra.
 *
 * Vive aquí por compatibilidad con la extensión, que espera esta forma; la lógica está en
 * `comandos-del-editor.ts`, que es donde se puede probar sin montar un editor.
 */
export const getSuggestionItems = ({ query, editor }: { query: string; editor: Editor }) =>
  matchingCommands(query, { insideColumns: editor.isActive('columnas') });

interface SuggestionProps {
  items: EditorCommand[];
  command: (command: EditorCommand) => void;
  clientRect?: (() => DOMRect | null) | null;
  editor?: Editor;
  range?: Range;
}

/**
 * El desplegable del menú <code>/</code>.
 *
 * <b>Antes no se podía usar con el teclado.</b> Su manejador de teclas sólo atendía `Escape`: las
 * flechas y el Enter no hacían nada y no había ningún elemento resaltado, así que escribías `/`,
 * aparecía el menú, y tenías que soltar el teclado e ir al ratón. En un editor donde el menú `/`
 * es la vía principal para todo, eso es justo lo contrario de para qué existe.
 *
 * Y estaba pintado con estilos escritos a mano en JavaScript —`background = 'white'`,
 * `color = '#18181b'`—, así que <b>no respetaba el tema oscuro</b>. Ahora usa las clases del
 * proyecto, como el desplegable de menciones, que ya lo hacía bien.
 *
 * Se construye con DOM a mano porque TipTap espera un renderizador síncrono y en Angular no hay un
 * equivalente cómodo a `ReactRenderer`. Es la misma técnica que usa `mencion.ts`.
 */
export function renderItems(promptUrl: PromptUrl) {
  return () => {
    let box: HTMLElement;
    let popup: Instance[] | undefined;
    let commands: EditorCommand[] = [];
    let selected = 0;
    let onSelect: ((command: EditorCommand) => void) | null = null;

    const choose = (command: EditorCommand) => onSelect?.(command);

    const render = () => {
      box.innerHTML = '';

      if (commands.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'px-2 py-3 text-sm text-muted-foreground text-center';
        empty.textContent = $localize`Ningún comando coincide`;
        box.appendChild(empty);
        return;
      }

      let renderedGroup: string | null = null;

      commands.forEach((command, i) => {
        // El grupo se pinta al cambiar, no una vez por comando: al filtrar, los grupos que se
        // quedan sin nada desaparecen solos.
        if (command.group !== renderedGroup) {
          renderedGroup = command.group;

          const title = document.createElement('div');
          title.className =
            'px-2 pt-2 pb-1 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground';
          title.textContent = command.group;
          box.appendChild(title);
        }

        const button = document.createElement('button');
        button.type = 'button';
        button.setAttribute('role', 'option');
        button.setAttribute('aria-selected', String(i === selected));
        button.className =
          'w-full text-left px-2 py-1.5 rounded flex items-center gap-2.5 transition-colors ' +
          (i === selected ? 'bg-secondary' : 'bg-transparent');

        const icon = document.createElement('span');
        icon.className =
          'w-7 h-7 shrink-0 rounded border border-border bg-card flex items-center justify-center '
          + 'text-muted-foreground [&>svg]:w-4 [&>svg]:h-4';
        // La variable la define `ng-icon` en sus propios elementos; aquí el SVG va suelto, así que
        // sin esto el trazo sale con el grosor que herede y los iconos se ven descuadrados.
        icon.style.setProperty('--ng-icon__stroke-width', '2');
        icon.innerHTML = command.icon;
        button.appendChild(icon);

        const texts = document.createElement('span');
        texts.className = 'flex flex-col min-w-0';

        const title = document.createElement('span');
        title.className = 'text-sm text-foreground truncate';
        title.textContent = command.title;
        texts.appendChild(title);

        const description = document.createElement('span');
        description.className = 'text-xs text-muted-foreground truncate';
        description.textContent = command.description;
        texts.appendChild(description);

        button.appendChild(texts);

        button.addEventListener('mousedown', (e) => {
          // `mousedown` y no `click`: al pulsar, el editor pierde el foco antes de que llegue el
          // `click`, y el comando se aplicaba en el sitio equivocado o no se aplicaba.
          e.preventDefault();
          choose(command);
        });

        button.addEventListener('mouseenter', () => {
          selected = i;
          render();
        });

        box.appendChild(button);
      });

      // Mantener a la vista el resaltado al recorrer con las flechas. Sin esto, la selección se
      // va por debajo del borde y parece que el menú ha dejado de responder.
      box.querySelector('[aria-selected="true"]')?.scrollIntoView({ block: 'nearest' });
    };

    const move = (step: number) => {
      if (commands.length === 0) return;
      selected = (selected + step + commands.length) % commands.length;
      render();
    };

    return {
      onStart: (props: SuggestionProps) => {
        commands = props.items;
        selected = 0;
        onSelect = props.command;

        box = document.createElement('div');
        box.setAttribute('role', 'listbox');
        box.setAttribute('aria-label', $localize`Comandos del editor`);
        box.className =
          'bg-card border border-border rounded-lg shadow-lg p-1 w-72 max-h-80 overflow-y-auto';

        render();

        popup = tippy('body', {
          getReferenceClientRect: props.clientRect as () => DOMRect,
          appendTo: () => document.body,
          content: box,
          showOnCreate: true,
          interactive: true,
          trigger: 'manual',
          placement: 'bottom-start'
        });
      },

      onUpdate: (props: SuggestionProps) => {
        commands = props.items;
        selected = 0;
        onSelect = props.command;
        render();

        popup?.[0]?.setProps({ getReferenceClientRect: props.clientRect as () => DOMRect });
      },

      onKeyDown: (props: { event: KeyboardEvent }) => {
        if (props.event.key === 'ArrowDown') { move(1); return true; }
        if (props.event.key === 'ArrowUp') { move(-1); return true; }

        // Tabulador también, porque en un desplegable de autocompletado es lo que mucha gente
        // pulsa por costumbre.
        if (props.event.key === 'Tab') {
          move(props.event.shiftKey ? -1 : 1);
          return true;
        }

        if (props.event.key === 'Enter') {
          const chosen = commands[selected];
          if (!chosen) return false;
          choose(chosen);
          return true;
        }

        if (props.event.key === 'Escape') {
          popup?.[0]?.hide();
          return true;
        }

        return false;
      },

      onExit: () => {
        popup?.[0]?.destroy();
        popup = undefined;
      }
    };
  };
}

export type { PromptUrl };
