import type { Editor, Range } from '@tiptap/core';
import tippy, { type Instance } from 'tippy.js';
import { type ComandoDelEditor, type PedirUrl, comandosQueCasan } from './comandos-del-editor';

/**
 * Los comandos que casan con lo escrito detrás de la barra.
 *
 * Vive aquí por compatibilidad con la extensión, que espera esta forma; la lógica está en
 * `comandos-del-editor.ts`, que es donde se puede probar sin montar un editor.
 */
export const getSuggestionItems = ({ query, editor }: { query: string; editor: Editor }) =>
  comandosQueCasan(query, { dentroDeColumnas: editor.isActive('columnas') });

interface PropsDeSugerencia {
  items: ComandoDelEditor[];
  command: (comando: ComandoDelEditor) => void;
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
export function renderItems(pedirUrl: PedirUrl) {
  return () => {
    let caja: HTMLElement;
    let popup: Instance[] | undefined;
    let comandos: ComandoDelEditor[] = [];
    let seleccionado = 0;
    let alElegir: ((comando: ComandoDelEditor) => void) | null = null;

    const elegir = (comando: ComandoDelEditor) => alElegir?.(comando);

    const pintar = () => {
      caja.innerHTML = '';

      if (comandos.length === 0) {
        const vacio = document.createElement('div');
        vacio.className = 'px-2 py-3 text-sm text-muted-foreground text-center';
        vacio.textContent = $localize`Ningún comando coincide`;
        caja.appendChild(vacio);
        return;
      }

      let grupoPintado: string | null = null;

      comandos.forEach((comando, i) => {
        // El grupo se pinta al cambiar, no una vez por comando: al filtrar, los grupos que se
        // quedan sin nada desaparecen solos.
        if (comando.grupo !== grupoPintado) {
          grupoPintado = comando.grupo;

          const titulo = document.createElement('div');
          titulo.className =
            'px-2 pt-2 pb-1 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground';
          titulo.textContent = comando.grupo;
          caja.appendChild(titulo);
        }

        const boton = document.createElement('button');
        boton.type = 'button';
        boton.setAttribute('role', 'option');
        boton.setAttribute('aria-selected', String(i === seleccionado));
        boton.className =
          'w-full text-left px-2 py-1.5 rounded flex items-center gap-2.5 transition-colors ' +
          (i === seleccionado ? 'bg-secondary' : 'bg-transparent');

        const icono = document.createElement('span');
        icono.className =
          'w-7 h-7 shrink-0 rounded border border-border bg-card flex items-center justify-center '
          + 'text-muted-foreground [&>svg]:w-4 [&>svg]:h-4';
        // La variable la define `ng-icon` en sus propios elementos; aquí el SVG va suelto, así que
        // sin esto el trazo sale con el grosor que herede y los iconos se ven descuadrados.
        icono.style.setProperty('--ng-icon__stroke-width', '2');
        icono.innerHTML = comando.icono;
        boton.appendChild(icono);

        const textos = document.createElement('span');
        textos.className = 'flex flex-col min-w-0';

        const titulo = document.createElement('span');
        titulo.className = 'text-sm text-foreground truncate';
        titulo.textContent = comando.titulo;
        textos.appendChild(titulo);

        const descripcion = document.createElement('span');
        descripcion.className = 'text-xs text-muted-foreground truncate';
        descripcion.textContent = comando.descripcion;
        textos.appendChild(descripcion);

        boton.appendChild(textos);

        boton.addEventListener('mousedown', (e) => {
          // `mousedown` y no `click`: al pulsar, el editor pierde el foco antes de que llegue el
          // `click`, y el comando se aplicaba en el sitio equivocado o no se aplicaba.
          e.preventDefault();
          elegir(comando);
        });

        boton.addEventListener('mouseenter', () => {
          seleccionado = i;
          pintar();
        });

        caja.appendChild(boton);
      });

      // Mantener a la vista el resaltado al recorrer con las flechas. Sin esto, la selección se
      // va por debajo del borde y parece que el menú ha dejado de responder.
      caja.querySelector('[aria-selected="true"]')?.scrollIntoView({ block: 'nearest' });
    };

    const mover = (paso: number) => {
      if (comandos.length === 0) return;
      seleccionado = (seleccionado + paso + comandos.length) % comandos.length;
      pintar();
    };

    return {
      onStart: (props: PropsDeSugerencia) => {
        comandos = props.items;
        seleccionado = 0;
        alElegir = props.command;

        caja = document.createElement('div');
        caja.setAttribute('role', 'listbox');
        caja.setAttribute('aria-label', $localize`Comandos del editor`);
        caja.className =
          'bg-card border border-border rounded-lg shadow-lg p-1 w-72 max-h-80 overflow-y-auto';

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

      onUpdate: (props: PropsDeSugerencia) => {
        comandos = props.items;
        seleccionado = 0;
        alElegir = props.command;
        pintar();

        popup?.[0]?.setProps({ getReferenceClientRect: props.clientRect as () => DOMRect });
      },

      onKeyDown: (props: { event: KeyboardEvent }) => {
        if (props.event.key === 'ArrowDown') { mover(1); return true; }
        if (props.event.key === 'ArrowUp') { mover(-1); return true; }

        // Tabulador también, porque en un desplegable de autocompletado es lo que mucha gente
        // pulsa por costumbre.
        if (props.event.key === 'Tab') {
          mover(props.event.shiftKey ? -1 : 1);
          return true;
        }

        if (props.event.key === 'Enter') {
          const elegido = comandos[seleccionado];
          if (!elegido) return false;
          elegir(elegido);
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

export type { PedirUrl };
