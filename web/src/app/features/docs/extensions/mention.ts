import { mergeAttributes, Node } from '@tiptap/core';
import Suggestion from '@tiptap/suggestion';
import { PluginKey } from '@tiptap/pm/state';
import tippy, { type Instance } from 'tippy.js';

/**
 * Qué se puede mencionar. Los nombres son los que el servidor lee de
 * `MentionableTypes`; escribir otro deja la mención fuera del índice **sin dar ningún error**.
 */
export type MentionType = 'Person' | 'Team' | 'Task' | 'Ticket' | 'Project' | 'Document';

/**
 * Cómo se le llama a cada tipo en pantalla. El valor de {@link MentionType} es un dato que se
 * guarda y viaja a la API; enseñarlo tal cual pondría «Task» delante de una tarea.
 */
export const MENTION_TYPE_LABELS: Record<MentionType, string> = {
  Person: $localize`Persona`,
  Team: $localize`Equipo`,
  Task: $localize`Tarea`,
  Ticket: $localize`Ticket`,
  Project: $localize`Proyecto`,
  Document: $localize`Documento`
};

/** Un candidato del desplegable de menciones. */
export interface MentionCandidate {
  id: string;
  label: string;
  type: MentionType;
  /** Contexto para distinguir dos con el mismo nombre: el proyecto, el estado… */
  detail?: string;
}

/** Quién busca los candidatos. Lo aporta el componente, que es quien sabe llamar a la API. */
export type MentionSearch = (
  trigger: string, query: string
) => Promise<MentionCandidate[]>;

/**
 * <b>El contrato con el servidor, en un solo sitio.</b>
 *
 * El servidor extrae las menciones leyendo estos dos atributos del HTML guardado
 * (`MentionReader`). Si aquí cambiaran los nombres, las menciones dejarían de indexarse y
 * ninguna tarea volvería a saber qué documentos hablan de ella — **sin que nada fallara**.
 *
 * Por eso están como constantes con nombre y no escritos a mano en el renderizador, y por eso hay
 * una prueba de integración que escribe una mención con este formato y comprueba que la tarea la
 * ve desde el otro lado.
 */
export const TYPE_ATTRIBUTE = 'data-mention-type';
export const ID_ATTRIBUTE = 'data-mention-id';

/**
 * Menciones dentro del editor: `@` para personas y `#` para tareas, tickets y proyectos.
 *
 * <b>Dos disparadores y un solo nodo.</b> `@` y `#` son costumbres distintas —nadie escribe
 * `@tarea`— pero lo que producen es lo mismo: un enlace a algo del producto. Con dos nodos habría
 * que duplicar el renderizado, el parseo y el contrato con el servidor.
 *
 * Es la mitad visible del diferencial. La otra —que la tarea sepa qué documentos hablan de ella—
 * la resuelve el servidor a partir de lo que este nodo escribe.
 */
export const Mention = Node.create<{ search: MentionSearch | null }>({
  name: 'mention',

  group: 'inline',
  inline: true,

  // Átomo: la mención se borra entera de una vez. Sin esto se puede dejar media dentro del texto,
  // y media mención es un enlace roto que el servidor ya no sabe indexar.
  atom: true,
  selectable: true,

  addOptions() {
    return { search: null };
  },

  addAttributes() {
    return {
      type: {
        default: null,
        parseHTML: (element) => element.getAttribute(TYPE_ATTRIBUTE),
        renderHTML: (attributes) => (attributes['type'] ? { [TYPE_ATTRIBUTE]: attributes['type'] } : {})
      },
      entityId: {
        default: null,
        parseHTML: (element) => element.getAttribute(ID_ATTRIBUTE),
        renderHTML: (attributes) => (attributes['entityId'] ? { [ID_ATTRIBUTE]: attributes['entityId'] } : {})
      },
      label: {
        default: '',
        parseHTML: (element) => element.textContent ?? '',
        // La etiqueta no se escribe como atributo: **es el texto del nodo**. Guardarla también en
        // un atributo daría dos copias del mismo dato que pueden discrepar al editar.
        renderHTML: () => ({})
      }
    };
  },

  parseHTML() {
    return [{ tag: `span[${TYPE_ATTRIBUTE}]` }];
  },

  renderHTML({ node, HTMLAttributes }) {
    const type = node.attrs['type'] as MentionType | null;

    return [
      'span',
      mergeAttributes(HTMLAttributes, {
        class: 'mention inline-flex items-center rounded px-1 bg-primary/10 text-primary font-medium'
      }),
      `${type === 'Person' ? '@' : '#'}${node.attrs['label']}`
    ];
  },

  renderText({ node }) {
    const type = node.attrs['type'] as MentionType | null;
    return `${type === 'Person' ? '@' : '#'}${node.attrs['label']}`;
  },

  addProseMirrorPlugins() {
    const search = this.options.search;

    // Un plugin por disparador, con su propia clave: ProseMirror exige claves distintas, y con la
    // misma el segundo pisa al primero en silencio — sólo funcionaría uno de los dos.
    return ['@', '#'].map((trigger) =>
      Suggestion({
        editor: this.editor,
        char: trigger,
        pluginKey: new PluginKey(`mention-${trigger}`),
        allowSpaces: false,

        items: async ({ query }) => (search ? search(trigger, query) : []),

        command: ({ editor, range, props }) => {
          const candidate = props as unknown as MentionCandidate;

          editor.chain().focus()
            .insertContentAt(range, [
              {
                type: 'mention',
                attrs: {
                  type: candidate.type,
                  entityId: candidate.id,
                  label: candidate.label
                }
              },
              // Un espacio detrás: sin él, lo siguiente que se escribe se pega a la mención y
              // parece parte de ella.
              { type: 'text', text: ' ' }
            ])
            .run();
        },

        render: renderDropdown
      })
    );
  }
});

/**
 * El desplegable de candidatos.
 *
 * Se construye con DOM a mano porque TipTap espera un renderizador síncrono y en Angular no hay un
 * equivalente cómodo a `ReactRenderer`. Es la misma técnica que ya usa el menú `/` de este editor;
 * se sigue igual para que haya una sola forma de hacerlo aquí dentro.
 */
function renderDropdown() {
  let box: HTMLElement;
  let popup: Instance[];
  let selected = 0;
  let candidates: MentionCandidate[] = [];
  let onSelect: ((c: MentionCandidate) => void) | null = null;

  const render = () => {
    box.innerHTML = '';

    if (candidates.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'px-2 py-1.5 text-sm text-muted-foreground';
      empty.textContent = 'Nada que mencionar';
      box.appendChild(empty);
      return;
    }

    candidates.forEach((candidate, i) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className =
        'w-full text-left px-2 py-1.5 text-sm rounded flex items-center gap-2 ' +
        (i === selected ? 'bg-secondary' : 'bg-transparent');

      const label = document.createElement('span');
      label.textContent = candidate.label;
      button.appendChild(label);

      if (candidate.detail) {
        const detail = document.createElement('span');
        detail.className = 'text-xs text-muted-foreground truncate';
        detail.textContent = candidate.detail;
        button.appendChild(detail);
      }

      button.addEventListener('mousedown', (e) => {
        // `mousedown` y no `click`: al hacer clic el editor pierde el foco antes de que llegue el
        // `click`, y la mención se insertaba en el sitio equivocado o no se insertaba.
        e.preventDefault();
        onSelect?.(candidate);
      });

      box.appendChild(button);
    });
  };

  return {
    onStart: (props: { items: MentionCandidate[]; command: (c: MentionCandidate) => void; clientRect?: (() => DOMRect | null) | null }) => {
      candidates = props.items;
      selected = 0;
      onSelect = props.command;

      box = document.createElement('div');
      box.className =
        'bg-card border border-border rounded-md shadow-lg p-1 min-w-[220px] max-h-64 overflow-y-auto';

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

    onUpdate: (props: { items: MentionCandidate[]; command: (c: MentionCandidate) => void; clientRect?: (() => DOMRect | null) | null }) => {
      candidates = props.items;
      selected = 0;
      onSelect = props.command;
      render();

      popup?.[0]?.setProps({ getReferenceClientRect: props.clientRect as () => DOMRect });
    },

    onKeyDown: (props: { event: KeyboardEvent }) => {
      // Las flechas y Enter se manejan aquí para que se pueda elegir sin soltar el teclado, que es
      // como se usa un editor de verdad.
      if (props.event.key === 'ArrowDown') {
        selected = (selected + 1) % Math.max(candidates.length, 1);
        render();
        return true;
      }

      if (props.event.key === 'ArrowUp') {
        selected = (selected - 1 + candidates.length) % Math.max(candidates.length, 1);
        render();
        return true;
      }

      if (props.event.key === 'Enter') {
        const chosen = candidates[selected];
        if (chosen) {
          onSelect?.(chosen);
          return true;
        }
        return false;
      }

      if (props.event.key === 'Escape') {
        popup?.[0]?.hide();
        return true;
      }

      return false;
    },

    onExit: () => {
      popup?.[0]?.destroy();
    }
  };
}
