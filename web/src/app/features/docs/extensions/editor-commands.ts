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
import type {} from '@tiptap/extension-details';
import type {} from '@tiptap/extension-highlight';
import {
  lucideBan, lucideChevronRight, lucideCircleCheck, lucideCode, lucideHeading1, lucideHeading2,
  lucideHeading3, lucideImage, lucideLightbulb, lucideList, lucideListChecks, lucideListOrdered,
  lucideMinus, lucidePaperclip, lucideQuote, lucideTable, lucideTriangleAlert, lucideType,
  lucideUpload, lucideYoutube, lucideColumns2, lucideColumns3, lucideRows3
} from '@ng-icons/lucide';
import type {} from './callout';
import type {} from './columns';

/**
 * Qué se le pide al componente cuando hace falta una dirección.
 *
 * La clase la interpreta quien enseña el modal —cambia el título y la ayuda—; aquí sólo se elige
 * cuál toca. `enlace` no lo usa ningún comando de esta lista: lo usa la barra flotante, que hace
 * la misma pregunta y comparte el mismo modal para no acabar con dos validaciones distintas.
 */
export type PromptUrl = (kind: 'image' | 'video' | 'attachment' | 'link') => Promise<string | null>;

/**
 * Cómo se pide un fichero del ordenador y se sube.
 *
 * Devuelve la dirección con la que quedó guardado, o `null` si se canceló o falló. Igual que
 * `PedirUrl`, lo inyecta el componente: la extensión no sabe llamar a la API.
 */
export type UploadFile = () => Promise<{ url: string; name: string; isImage: boolean } | null>;

export interface EditorCommand {
  key: string;
  title: string;
  description: string;
  /** Marcado SVG, tal como lo exporta `@ng-icons/lucide`. */
  icon: string;
  group: string;
  /**
   * Otras formas de escribirlo.
   *
   * Existen porque el filtro anterior comparaba con `startsWith` sobre el título **en inglés**:
   * `/lista` no encontraba nada, `/list` tampoco encontraba «Bullet List», y `/h1` tampoco. Sólo
   * acertaba quien supiera de memoria la primera palabra en inglés.
   */
  alias: readonly string[];
  /**
   * Si el comando sólo tiene sentido con el cursor dentro de un bloque concreto.
   *
   * «Deshacer columnas» fuera de unas columnas no hace nada, y ofrecerlo en todas partes sería
   * poner en el menú un botón que no responde.
   */
  onlyInside?: 'columns';
  /**
   * Aplica el comando. El resultado se ignora: `run()` devuelve si la cadena se pudo aplicar, y
   * eso no es el resultado del comando.
   */
  run: (context: {
    editor: Editor;
    range: Range;
    promptUrl: PromptUrl;
    uploadFile: UploadFile;
  }) => unknown;
}

const BASIC_GROUP = $localize`Básico`;
const LISTS_GROUP = $localize`Listas`;
const BLOCKS_GROUP = $localize`Bloques`;
const INSERT_GROUP = $localize`Insertar`;

/**
 * Los comandos del menú <code>/</code>.
 *
 * <b>Estaban en inglés y sin marcar</b> dentro de una aplicación en español: «Heading 1», «Bullet
 * List», «Task List». Aquí van traducidos y marcados, con su grupo, su icono y una descripción,
 * que es lo que convierte una lista de palabras sueltas en algo que se puede recorrer sin saberse
 * los nombres.
 */
export const EDITOR_COMMANDS: readonly EditorCommand[] = [
  {
    key: 'paragraph',
    title: $localize`Texto`,
    description: $localize`Un párrafo normal`,
    icon: lucideType,
    group: BASIC_GROUP,
    alias: ['texto', 'text', 'parrafo', 'párrafo', 'paragraph', 'p'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).setParagraph().run()
  },
  {
    key: 'h1',
    title: $localize`Título 1`,
    description: $localize`Encabezado grande`,
    icon: lucideHeading1,
    group: BASIC_GROUP,
    alias: ['h1', 'titulo1', 'título1', 'heading1', 'encabezado'],
    run: ({ editor, range }) =>
      editor.chain().focus().deleteRange(range).setNode('heading', { level: 1 }).run()
  },
  {
    key: 'h2',
    title: $localize`Título 2`,
    description: $localize`Encabezado mediano`,
    icon: lucideHeading2,
    group: BASIC_GROUP,
    alias: ['h2', 'titulo2', 'título2', 'heading2', 'encabezado'],
    run: ({ editor, range }) =>
      editor.chain().focus().deleteRange(range).setNode('heading', { level: 2 }).run()
  },
  {
    key: 'h3',
    title: $localize`Título 3`,
    description: $localize`Encabezado pequeño`,
    icon: lucideHeading3,
    group: BASIC_GROUP,
    alias: ['h3', 'titulo3', 'título3', 'heading3', 'encabezado'],
    run: ({ editor, range }) =>
      editor.chain().focus().deleteRange(range).setNode('heading', { level: 3 }).run()
  },
  {
    key: 'bullets',
    title: $localize`Lista con viñetas`,
    description: $localize`Una lista sin orden`,
    icon: lucideList,
    group: LISTS_GROUP,
    alias: ['lista', 'list', 'vinetas', 'viñetas', 'bullet', 'ul'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleBulletList().run()
  },
  {
    key: 'numbered',
    title: $localize`Lista numerada`,
    description: $localize`Una lista con orden`,
    icon: lucideListOrdered,
    group: LISTS_GROUP,
    alias: ['lista', 'list', 'numerada', 'numbered', 'ordered', 'ol'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleOrderedList().run()
  },
  {
    key: 'tasks',
    title: $localize`Lista de tareas`,
    description: $localize`Casillas para ir marcando`,
    icon: lucideListChecks,
    group: LISTS_GROUP,
    alias: ['tareas', 'task', 'todo', 'checkbox', 'casillas', 'lista'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleTaskList().run()
  },
  {
    key: 'quote',
    title: $localize`Cita`,
    description: $localize`Texto destacado en un margen`,
    icon: lucideQuote,
    group: BLOCKS_GROUP,
    alias: ['cita', 'quote', 'blockquote'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleBlockquote().run()
  },
  {
    key: 'code',
    title: $localize`Bloque de código`,
    description: $localize`Código con su formato`,
    icon: lucideCode,
    group: BLOCKS_GROUP,
    alias: ['codigo', 'código', 'code', 'pre'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleCodeBlock().run()
  },
  {
    key: 'table',
    title: $localize`Tabla`,
    description: $localize`Tres por tres, con cabecera`,
    icon: lucideTable,
    group: BLOCKS_GROUP,
    alias: ['tabla', 'table', 'cuadro'],
    run: ({ editor, range }) =>
      editor.chain().focus().deleteRange(range)
        .insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()
  },
  {
    key: 'toggle',
    title: $localize`Desplegable`,
    description: $localize`Contenido que se abre y se cierra`,
    icon: lucideChevronRight,
    group: BLOCKS_GROUP,
    alias: ['desplegable', 'toggle', 'details', 'acordeon', 'acordeón', 'plegar'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).setDetails().run()
  },
  {
    key: 'callout',
    title: $localize`Aviso`,
    description: $localize`Un recuadro para lo importante`,
    icon: lucideLightbulb,
    group: BLOCKS_GROUP,
    alias: ['aviso', 'nota', 'callout', 'recuadro', 'destacado'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleCallout('nota').run()
  },
  {
    key: 'callout-warning',
    title: $localize`Aviso de atención`,
    description: $localize`Un recuadro de «ojo con esto»`,
    icon: lucideTriangleAlert,
    group: BLOCKS_GROUP,
    alias: ['aviso', 'atencion', 'atención', 'ojo', 'cuidado', 'warning'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleCallout('ojo').run()
  },
  {
    key: 'callout-danger',
    title: $localize`Aviso de peligro`,
    description: $localize`Un recuadro de «no hagas esto»`,
    icon: lucideBan,
    group: BLOCKS_GROUP,
    alias: ['aviso', 'peligro', 'danger', 'error', 'no'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleCallout('peligro').run()
  },
  {
    key: 'callout-success',
    title: $localize`Aviso de acierto`,
    description: $localize`Un recuadro de «así sí»`,
    icon: lucideCircleCheck,
    group: BLOCKS_GROUP,
    alias: ['aviso', 'bien', 'acierto', 'ok', 'success', 'correcto'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleCallout('bien').run()
  },
  {
    key: 'columns-2',
    title: $localize`Dos columnas`,
    description: $localize`Contenido lado a lado`,
    icon: lucideColumns2,
    group: BLOCKS_GROUP,
    alias: ['columnas', 'columns', 'dos', 'lado'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).insertColumns(2).run()
  },
  {
    key: 'columns-3',
    title: $localize`Tres columnas`,
    description: $localize`Tres bloques del mismo ancho`,
    icon: lucideColumns3,
    group: BLOCKS_GROUP,
    alias: ['columnas', 'columns', 'tres'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).insertColumns(3).run()
  },
  {
    key: 'columns-unset',
    title: $localize`Deshacer columnas`,
    description: $localize`Deja el contenido seguido, sin borrarlo`,
    icon: lucideRows3,
    group: BLOCKS_GROUP,
    alias: ['deshacer', 'quitar', 'columnas', 'unir'],
    onlyInside: 'columns',
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).unsetColumns().run()
  },
  {
    key: 'divider',
    title: $localize`Separador`,
    description: $localize`Una línea entre secciones`,
    icon: lucideMinus,
    group: BLOCKS_GROUP,
    alias: ['separador', 'divider', 'linea', 'línea', 'hr'],
    run: ({ editor, range }) => editor.chain().focus().deleteRange(range).setHorizontalRule().run()
  },
  {
    key: 'image',
    title: $localize`Imagen`,
    description: $localize`Desde una dirección web`,
    icon: lucideImage,
    group: INSERT_GROUP,
    alias: ['imagen', 'image', 'foto', 'img'],
    run: async ({ editor, range, promptUrl }) => {
      // Antes esto era un `window.prompt`: bloquea la pestaña entera, no se puede dar estilo, no
      // valida nada y se sale del editor. Ahora lo pide la aplicación, con el mismo aspecto que
      // el resto.
      const url = await promptUrl('image');
      if (url) editor.chain().focus().deleteRange(range).setImage({ src: url }).run();
    }
  },
  {
    key: 'video',
    title: $localize`Vídeo de YouTube`,
    description: $localize`Pegando el enlace del vídeo`,
    icon: lucideYoutube,
    group: INSERT_GROUP,
    alias: ['video', 'vídeo', 'youtube', 'yt'],
    run: async ({ editor, range, promptUrl }) => {
      const url = await promptUrl('video');
      if (url) editor.chain().focus().deleteRange(range).setYoutubeVideo({ src: url }).run();
    }
  },
  {
    key: 'upload',
    title: $localize`Subir un fichero`,
    description: $localize`Desde tu ordenador`,
    icon: lucideUpload,
    group: INSERT_GROUP,
    alias: ['subir', 'upload', 'fichero', 'archivo', 'imagen', 'adjuntar'],
    run: async ({ editor, range, uploadFile }) => {
      const uploaded = await uploadFile();
      if (!uploaded) return;

      // Una imagen se enseña; cualquier otra cosa va como tarjeta de adjunto. Meter un PDF en un
      // `<img>` deja un hueco roto en medio del documento.
      const content = uploaded.isImage
        ? { type: 'image', attrs: { src: uploaded.url, alt: uploaded.name } }
        : { type: 'fileAttachment', attrs: { href: uploaded.url, title: uploaded.name } };

      editor.chain().focus().deleteRange(range).insertContent(content).run();
    }
  },
  {
    key: 'attachment',
    title: $localize`Adjunto`,
    description: $localize`Un fichero, como tarjeta`,
    icon: lucidePaperclip,
    group: INSERT_GROUP,
    alias: ['adjunto', 'fichero', 'archivo', 'file', 'attachment', 'pdf'],
    run: async ({ editor, range, promptUrl }) => {
      // El nodo `fileAttachment` estaba registrado en el editor **sin ninguna forma de crearlo**:
      // ni comando, ni botón, ni pegado. Sólo aparecía si el HTML ya lo traía de fuera.
      const url = await promptUrl('attachment');
      if (!url) return;

      editor.chain().focus().deleteRange(range).insertContent({
        type: 'fileAttachment',
        attrs: { href: url, title: fileName(url) }
      }).run();
    }
  }
];

/** El último tramo de la dirección, que es lo que se enseña en la tarjeta del adjunto. */
function fileName(url: string): string {
  try {
    const path = new URL(url, 'https://ejemplo.invalido').pathname;
    return decodeURIComponent(path.split('/').filter(Boolean).pop() ?? url);
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
export function matchingCommands(
  query: string, context: { insideColumns?: boolean } = {}
): EditorCommand[] {
  const available = EDITOR_COMMANDS.filter(command =>
    command.onlyInside !== 'columns' || context.insideColumns === true);

  const text = normalize(query);
  if (!text) return available;

  return available.filter(command =>
    normalize(command.title).includes(text) ||
    command.alias.some(alias => normalize(alias).includes(text)));
}

function normalize(text: string): string {
  return text
    .toLowerCase()
    .normalize('NFD')
    // Quita los signos diacríticos ya separados por NFD: «título» y «titulo» tienen que casar.
    .replace(/[̀-ͯ]/g, '');
}
