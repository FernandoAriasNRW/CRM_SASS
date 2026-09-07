import { mergeAttributes, Node } from '@tiptap/core';
import Suggestion from '@tiptap/suggestion';
import { PluginKey } from '@tiptap/pm/state';
import tippy, { type Instance } from 'tippy.js';

/**
 * Qué se puede mencionar. Los nombres son los que el servidor lee de
 * `TiposMencionables`; escribir otro deja la mención fuera del índice **sin dar ningún error**.
 */
export type TipoDeMencion = 'Persona' | 'Tarea' | 'Ticket' | 'Proyecto' | 'Documento';

/** Un candidato del desplegable de menciones. */
export interface CandidatoDeMencion {
  id: string;
  etiqueta: string;
  tipo: TipoDeMencion;
  /** Contexto para distinguir dos con el mismo nombre: el proyecto, el estado… */
  detalle?: string;
}

/** Quién busca los candidatos. Lo aporta el componente, que es quien sabe llamar a la API. */
export type BuscadorDeMenciones = (
  disparador: string, consulta: string
) => Promise<CandidatoDeMencion[]>;

/**
 * <b>El contrato con el servidor, en un solo sitio.</b>
 *
 * El servidor extrae las menciones leyendo estos dos atributos del HTML guardado
 * (`LectorDeMenciones`). Si aquí cambiaran los nombres, las menciones dejarían de indexarse y
 * ninguna tarea volvería a saber qué documentos hablan de ella — **sin que nada fallara**.
 *
 * Por eso están como constantes con nombre y no escritos a mano en el renderizador, y por eso hay
 * una prueba de integración que escribe una mención con este formato y comprueba que la tarea la
 * ve desde el otro lado.
 */
export const ATRIBUTO_TIPO = 'data-mencion-tipo';
export const ATRIBUTO_ID = 'data-mencion-id';

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
export const Mencion = Node.create<{ buscador: BuscadorDeMenciones | null }>({
  name: 'mencion',

  group: 'inline',
  inline: true,

  // Átomo: la mención se borra entera de una vez. Sin esto se puede dejar media dentro del texto,
  // y media mención es un enlace roto que el servidor ya no sabe indexar.
  atom: true,
  selectable: true,

  addOptions() {
    return { buscador: null };
  },

  addAttributes() {
    return {
      tipo: {
        default: null,
        parseHTML: (elemento) => elemento.getAttribute(ATRIBUTO_TIPO),
        renderHTML: (atributos) => (atributos['tipo'] ? { [ATRIBUTO_TIPO]: atributos['tipo'] } : {})
      },
      entidadId: {
        default: null,
        parseHTML: (elemento) => elemento.getAttribute(ATRIBUTO_ID),
        renderHTML: (atributos) => (atributos['entidadId'] ? { [ATRIBUTO_ID]: atributos['entidadId'] } : {})
      },
      etiqueta: {
        default: '',
        parseHTML: (elemento) => elemento.textContent ?? '',
        // La etiqueta no se escribe como atributo: **es el texto del nodo**. Guardarla también en
        // un atributo daría dos copias del mismo dato que pueden discrepar al editar.
        renderHTML: () => ({})
      }
    };
  },

  parseHTML() {
    return [{ tag: `span[${ATRIBUTO_TIPO}]` }];
  },

  renderHTML({ node, HTMLAttributes }) {
    const tipo = node.attrs['tipo'] as TipoDeMencion | null;

    return [
      'span',
      mergeAttributes(HTMLAttributes, {
        class: 'mencion inline-flex items-center rounded px-1 bg-primary/10 text-primary font-medium'
      }),
      `${tipo === 'Persona' ? '@' : '#'}${node.attrs['etiqueta']}`
    ];
  },

  renderText({ node }) {
    const tipo = node.attrs['tipo'] as TipoDeMencion | null;
    return `${tipo === 'Persona' ? '@' : '#'}${node.attrs['etiqueta']}`;
  },

  addProseMirrorPlugins() {
    const buscador = this.options.buscador;

    // Un plugin por disparador, con su propia clave: ProseMirror exige claves distintas, y con la
    // misma el segundo pisa al primero en silencio — sólo funcionaría uno de los dos.
    return ['@', '#'].map((disparador) =>
      Suggestion({
        editor: this.editor,
        char: disparador,
        pluginKey: new PluginKey(`mencion-${disparador}`),
        allowSpaces: false,

        items: async ({ query }) => (buscador ? buscador(disparador, query) : []),

        command: ({ editor, range, props }) => {
          const candidato = props as unknown as CandidatoDeMencion;

          editor.chain().focus()
            .insertContentAt(range, [
              {
                type: 'mencion',
                attrs: {
                  tipo: candidato.tipo,
                  entidadId: candidato.id,
                  etiqueta: candidato.etiqueta
                }
              },
              // Un espacio detrás: sin él, lo siguiente que se escribe se pega a la mención y
              // parece parte de ella.
              { type: 'text', text: ' ' }
            ])
            .run();
        },

        render: renderizarDesplegable
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
function renderizarDesplegable() {
  let caja: HTMLElement;
  let popup: Instance[];
  let seleccionado = 0;
  let candidatos: CandidatoDeMencion[] = [];
  let alElegir: ((c: CandidatoDeMencion) => void) | null = null;

  const pintar = () => {
    caja.innerHTML = '';

    if (candidatos.length === 0) {
      const vacio = document.createElement('div');
      vacio.className = 'px-2 py-1.5 text-sm text-muted-foreground';
      vacio.textContent = 'Nada que mencionar';
      caja.appendChild(vacio);
      return;
    }

    candidatos.forEach((candidato, i) => {
      const boton = document.createElement('button');
      boton.type = 'button';
      boton.className =
        'w-full text-left px-2 py-1.5 text-sm rounded flex items-center gap-2 ' +
        (i === seleccionado ? 'bg-secondary' : 'bg-transparent');

      const etiqueta = document.createElement('span');
      etiqueta.textContent = candidato.etiqueta;
      boton.appendChild(etiqueta);

      if (candidato.detalle) {
        const detalle = document.createElement('span');
        detalle.className = 'text-xs text-muted-foreground truncate';
        detalle.textContent = candidato.detalle;
        boton.appendChild(detalle);
      }

      boton.addEventListener('mousedown', (e) => {
        // `mousedown` y no `click`: al hacer clic el editor pierde el foco antes de que llegue el
        // `click`, y la mención se insertaba en el sitio equivocado o no se insertaba.
        e.preventDefault();
        alElegir?.(candidato);
      });

      caja.appendChild(boton);
    });
  };

  return {
    onStart: (props: { items: CandidatoDeMencion[]; command: (c: CandidatoDeMencion) => void; clientRect?: (() => DOMRect | null) | null }) => {
      candidatos = props.items;
      seleccionado = 0;
      alElegir = props.command;

      caja = document.createElement('div');
      caja.className =
        'bg-card border border-border rounded-md shadow-lg p-1 min-w-[220px] max-h-64 overflow-y-auto';

      pintar();

      popup = tippy('body', {
        getReferenceClientRect: props.clientRect as () => DOMRect,
        appendTo: () => document.body,
        content: caja,
        showOnCreate: true,
        interactive: true,
        trigger: 'manual',
        placement: 'bottom-start'
      });
    },

    onUpdate: (props: { items: CandidatoDeMencion[]; command: (c: CandidatoDeMencion) => void; clientRect?: (() => DOMRect | null) | null }) => {
      candidatos = props.items;
      seleccionado = 0;
      alElegir = props.command;
      pintar();

      popup?.[0]?.setProps({ getReferenceClientRect: props.clientRect as () => DOMRect });
    },

    onKeyDown: (props: { event: KeyboardEvent }) => {
      // Las flechas y Enter se manejan aquí para que se pueda elegir sin soltar el teclado, que es
      // como se usa un editor de verdad.
      if (props.event.key === 'ArrowDown') {
        seleccionado = (seleccionado + 1) % Math.max(candidatos.length, 1);
        pintar();
        return true;
      }

      if (props.event.key === 'ArrowUp') {
        seleccionado = (seleccionado - 1 + candidatos.length) % Math.max(candidatos.length, 1);
        pintar();
        return true;
      }

      if (props.event.key === 'Enter') {
        const elegido = candidatos[seleccionado];
        if (elegido) {
          alElegir?.(elegido);
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
