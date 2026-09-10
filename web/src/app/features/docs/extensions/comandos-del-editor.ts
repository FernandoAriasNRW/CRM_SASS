import type { Editor, Range } from '@tiptap/core';

// Importaciones de sólo tipos, sin nada en tiempo de ejecución. Los comandos `setImage`,
// `setYoutubeVideo` y `setHorizontalRule` no existen en `ChainedCommands` por sí solos: cada
// extensión los añade declarando el módulo. Sin esto compila dentro de la aplicación —porque el
// componente sí importa las extensiones— y **no compila al probar este fichero solo**, que es
// justo cuando interesa poder probarlo.
import type {} from '@tiptap/extension-image';
import type {} from '@tiptap/extension-youtube';
import type {} from '@tiptap/starter-kit';
import type {} from '@tiptap/extension-table';
import type {} from '@tiptap/extension-task-list';
import {
  lucideCode, lucideHeading1, lucideHeading2, lucideHeading3, lucideImage, lucideList,
  lucideListChecks, lucideListOrdered, lucideMinus, lucidePaperclip, lucideQuote, lucideTable,
  lucideType, lucideYoutube
} from '@ng-icons/lucide';

/**
 * Qué se le pide al componente cuando hace falta una dirección.
 *
 * La clase la interpreta quien enseña el modal —cambia el título y la ayuda—; aquí sólo se elige
 * cuál toca. `enlace` no lo usa ningún comando de esta lista: lo usa la barra flotante, que hace
 * la misma pregunta y comparte el mismo modal para no acabar con dos validaciones distintas.
 */
export type PedirUrl = (clase: 'imagen' | 'video' | 'adjunto' | 'enlace') => Promise<string | null>;

export interface ComandoDelEditor {
  clave: string;
  titulo: string;
  descripcion: string;
  /** Marcado SVG, tal como lo exporta `@ng-icons/lucide`. */
  icono: string;
  grupo: string;
  /**
   * Otras formas de escribirlo.
   *
   * Existen porque el filtro anterior comparaba con `startsWith` sobre el título **en inglés**:
   * `/lista` no encontraba nada, `/list` tampoco encontraba «Bullet List», y `/h1` tampoco. Sólo
   * acertaba quien supiera de memoria la primera palabra en inglés.
   */
  alias: readonly string[];
  /**
   * Aplica el comando. El resultado se ignora: `run()` devuelve si la cadena se pudo aplicar, y
   * eso no es el resultado del comando.
   */
  ejecutar: (contexto: { editor: Editor; range: Range; pedirUrl: PedirUrl }) => unknown;
}

const GRUPO_BASICO = $localize`Básico`;
const GRUPO_LISTAS = $localize`Listas`;
const GRUPO_BLOQUES = $localize`Bloques`;
const GRUPO_INSERTAR = $localize`Insertar`;

/**
 * Los comandos del menú <code>/</code>.
 *
 * <b>Estaban en inglés y sin marcar</b> dentro de una aplicación en español: «Heading 1», «Bullet
 * List», «Task List». Aquí van traducidos y marcados, con su grupo, su icono y una descripción,
 * que es lo que convierte una lista de palabras sueltas en algo que se puede recorrer sin saberse
 * los nombres.
 */
export const COMANDOS_DEL_EDITOR: readonly ComandoDelEditor[] = [
  {
    clave: 'parrafo',
    titulo: $localize`Texto`,
    descripcion: $localize`Un párrafo normal`,
    icono: lucideType,
    grupo: GRUPO_BASICO,
    alias: ['texto', 'text', 'parrafo', 'párrafo', 'paragraph', 'p'],
    ejecutar: ({ editor, range }) => editor.chain().focus().deleteRange(range).setParagraph().run()
  },
  {
    clave: 'h1',
    titulo: $localize`Título 1`,
    descripcion: $localize`Encabezado grande`,
    icono: lucideHeading1,
    grupo: GRUPO_BASICO,
    alias: ['h1', 'titulo1', 'título1', 'heading1', 'encabezado'],
    ejecutar: ({ editor, range }) =>
      editor.chain().focus().deleteRange(range).setNode('heading', { level: 1 }).run()
  },
  {
    clave: 'h2',
    titulo: $localize`Título 2`,
    descripcion: $localize`Encabezado mediano`,
    icono: lucideHeading2,
    grupo: GRUPO_BASICO,
    alias: ['h2', 'titulo2', 'título2', 'heading2', 'encabezado'],
    ejecutar: ({ editor, range }) =>
      editor.chain().focus().deleteRange(range).setNode('heading', { level: 2 }).run()
  },
  {
    clave: 'h3',
    titulo: $localize`Título 3`,
    descripcion: $localize`Encabezado pequeño`,
    icono: lucideHeading3,
    grupo: GRUPO_BASICO,
    alias: ['h3', 'titulo3', 'título3', 'heading3', 'encabezado'],
    ejecutar: ({ editor, range }) =>
      editor.chain().focus().deleteRange(range).setNode('heading', { level: 3 }).run()
  },
  {
    clave: 'vinetas',
    titulo: $localize`Lista con viñetas`,
    descripcion: $localize`Una lista sin orden`,
    icono: lucideList,
    grupo: GRUPO_LISTAS,
    alias: ['lista', 'list', 'vinetas', 'viñetas', 'bullet', 'ul'],
    ejecutar: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleBulletList().run()
  },
  {
    clave: 'numerada',
    titulo: $localize`Lista numerada`,
    descripcion: $localize`Una lista con orden`,
    icono: lucideListOrdered,
    grupo: GRUPO_LISTAS,
    alias: ['lista', 'list', 'numerada', 'numbered', 'ordered', 'ol'],
    ejecutar: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleOrderedList().run()
  },
  {
    clave: 'tareas',
    titulo: $localize`Lista de tareas`,
    descripcion: $localize`Casillas para ir marcando`,
    icono: lucideListChecks,
    grupo: GRUPO_LISTAS,
    alias: ['tareas', 'task', 'todo', 'checkbox', 'casillas', 'lista'],
    ejecutar: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleTaskList().run()
  },
  {
    clave: 'cita',
    titulo: $localize`Cita`,
    descripcion: $localize`Texto destacado en un margen`,
    icono: lucideQuote,
    grupo: GRUPO_BLOQUES,
    alias: ['cita', 'quote', 'blockquote'],
    ejecutar: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleBlockquote().run()
  },
  {
    clave: 'codigo',
    titulo: $localize`Bloque de código`,
    descripcion: $localize`Código con su formato`,
    icono: lucideCode,
    grupo: GRUPO_BLOQUES,
    alias: ['codigo', 'código', 'code', 'pre'],
    ejecutar: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleCodeBlock().run()
  },
  {
    clave: 'tabla',
    titulo: $localize`Tabla`,
    descripcion: $localize`Tres por tres, con cabecera`,
    icono: lucideTable,
    grupo: GRUPO_BLOQUES,
    alias: ['tabla', 'table', 'cuadro'],
    ejecutar: ({ editor, range }) =>
      editor.chain().focus().deleteRange(range)
        .insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()
  },
  {
    clave: 'separador',
    titulo: $localize`Separador`,
    descripcion: $localize`Una línea entre secciones`,
    icono: lucideMinus,
    grupo: GRUPO_BLOQUES,
    alias: ['separador', 'divider', 'linea', 'línea', 'hr'],
    ejecutar: ({ editor, range }) => editor.chain().focus().deleteRange(range).setHorizontalRule().run()
  },
  {
    clave: 'imagen',
    titulo: $localize`Imagen`,
    descripcion: $localize`Desde una dirección web`,
    icono: lucideImage,
    grupo: GRUPO_INSERTAR,
    alias: ['imagen', 'image', 'foto', 'img'],
    ejecutar: async ({ editor, range, pedirUrl }) => {
      // Antes esto era un `window.prompt`: bloquea la pestaña entera, no se puede dar estilo, no
      // valida nada y se sale del editor. Ahora lo pide la aplicación, con el mismo aspecto que
      // el resto.
      const url = await pedirUrl('imagen');
      if (url) editor.chain().focus().deleteRange(range).setImage({ src: url }).run();
    }
  },
  {
    clave: 'video',
    titulo: $localize`Vídeo de YouTube`,
    descripcion: $localize`Pegando el enlace del vídeo`,
    icono: lucideYoutube,
    grupo: GRUPO_INSERTAR,
    alias: ['video', 'vídeo', 'youtube', 'yt'],
    ejecutar: async ({ editor, range, pedirUrl }) => {
      const url = await pedirUrl('video');
      if (url) editor.chain().focus().deleteRange(range).setYoutubeVideo({ src: url }).run();
    }
  },
  {
    clave: 'adjunto',
    titulo: $localize`Adjunto`,
    descripcion: $localize`Un fichero, como tarjeta`,
    icono: lucidePaperclip,
    grupo: GRUPO_INSERTAR,
    alias: ['adjunto', 'fichero', 'archivo', 'file', 'attachment', 'pdf'],
    ejecutar: async ({ editor, range, pedirUrl }) => {
      // El nodo `fileAttachment` estaba registrado en el editor **sin ninguna forma de crearlo**:
      // ni comando, ni botón, ni pegado. Sólo aparecía si el HTML ya lo traía de fuera.
      const url = await pedirUrl('adjunto');
      if (!url) return;

      editor.chain().focus().deleteRange(range).insertContent({
        type: 'fileAttachment',
        attrs: { href: url, title: nombreDelFichero(url) }
      }).run();
    }
  }
];

/** El último tramo de la dirección, que es lo que se enseña en la tarjeta del adjunto. */
function nombreDelFichero(url: string): string {
  try {
    const camino = new URL(url, 'https://ejemplo.invalido').pathname;
    return decodeURIComponent(camino.split('/').filter(Boolean).pop() ?? url);
  } catch {
    return url;
  }
}

/**
 * Los comandos que casan con lo escrito.
 *
 * Compara **por subcadena** contra el título y los alias, no con `startsWith` sobre el título
 * entero, que era lo que hacía que casi ninguna búsqueda encontrara nada. Sin acentos y sin
 * mayúsculas: quien escribe deprisa dentro del texto no va a poner la tilde de «vídeo».
 */
export function comandosQueCasan(consulta: string): ComandoDelEditor[] {
  const texto = normalizar(consulta);
  if (!texto) return [...COMANDOS_DEL_EDITOR];

  return COMANDOS_DEL_EDITOR.filter(comando =>
    normalizar(comando.titulo).includes(texto) ||
    comando.alias.some(alias => normalizar(alias).includes(texto)));
}

function normalizar(texto: string): string {
  return texto
    .toLowerCase()
    .normalize('NFD')
    // Quita los signos diacríticos ya separados por NFD: «título» y «titulo» tienen que casar.
    .replace(/[̀-ͯ]/g, '');
}
