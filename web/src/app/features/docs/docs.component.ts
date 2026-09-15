import { Component, effect, inject, signal, computed, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewInit, Injector } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { 
  lucideFileText, lucidePlus, lucideFolder, lucideMoreVertical,
  lucideChevronRight, lucideChevronDown, lucideSearch,
  lucideSettings, lucideShare2, lucideClock, lucidePin, lucidePinOff,
  lucideBold, lucideItalic, lucideStrikethrough, lucideLink, lucideTrash,
  lucideUpload, lucideWand2, lucideLayoutTemplate, lucideCopy, lucideBookOpen,
  lucideUsers, lucideCalendar, lucideCheckCircle2, lucideStar, lucideFilter,
  lucideArrowUpDown, lucideTag, lucideX, lucideFileUp, lucideBriefcase, lucideCheck,
  lucideTriangleAlert, lucideCode, lucideQuote, lucideUnlink, lucideRemoveFormatting,
  lucideMessageSquare
} from '@ng-icons/lucide';
import { AnotacionDto, DocsService, DocumentDto, PageDto } from './docs.service';
import { SeccionesDelPanelService } from '../../shared/ui/panel-de-navegacion/secciones-del-panel.service';

/** Las pestañas que el panel de Documentos sabe abrir. Cualquier otra cosa en `?tab=` cae en «all». */
const TABS_DE_DOCS = ['all', 'my', 'shared', 'private', 'meeting-notes', 'templates', 'archived'] as const;
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import Youtube from '@tiptap/extension-youtube';
import SlashCommand from './extensions/slash-command';
import { Mencion } from './extensions/mencion';
import { MencionesService } from './menciones.service';
import { EsquemaDelDocumentoComponent } from './esquema-del-documento.component';
import { FileAttachment } from './extensions/file-attachment';
import { TiptapEditorDirective } from 'ngx-tiptap';
import { Table } from '@tiptap/extension-table';
import TableRow from '@tiptap/extension-table-row';
import TableCell from '@tiptap/extension-table-cell';
import TableHeader from '@tiptap/extension-table-header';
import TaskList from '@tiptap/extension-task-list';
import TaskItem from '@tiptap/extension-task-item';
import Link from '@tiptap/extension-link';
import Placeholder from '@tiptap/extension-placeholder';
import BubbleMenu from '@tiptap/extension-bubble-menu';
import { Details, DetailsContent, DetailsSummary } from '@tiptap/extension-details';
import { DragHandle } from '@tiptap/extension-drag-handle';
import Highlight from '@tiptap/extension-highlight';
import Typography from '@tiptap/extension-typography';
import CharacterCount from '@tiptap/extension-character-count';
import { createLowlight, common } from 'lowlight';
import { Aviso } from './extensions/aviso';
import { BloqueDeCodigo } from './extensions/bloque-de-codigo';
import FileHandler from '@tiptap/extension-file-handler';
import { Comentario } from './extensions/comentario';
import { Columna, Columnas } from './extensions/columnas';
import { ComentariosDelDocumentoComponent } from './comentarios-del-documento.component';
import { EmojiPickerComponent } from './extensions/emoji-picker.component';
import { Subject, debounceTime, firstValueFrom } from 'rxjs';
import { ClickableDirective } from '../../shared/directives/clickable.directive';
import { GuardarPlantillaModalComponent } from './modals/guardar-plantilla-modal.component';
import { ImportarDocumentoModalComponent } from './modals/importar-documento-modal.component';
import { PlantillasDrawerComponent } from './plantillas-drawer.component';
import { ArbolDePaginasComponent, MovimientoDePagina } from './arbol-de-paginas.component';
import { ClaseDeUrl, PedirUrlModalComponent } from './modals/pedir-url-modal.component';
import { ToastService } from '../../shared/services/toast.service';
import { PLANTILLAS_A_LA_VISTA, PlantillaDisponible, plantillasDisponibles } from './plantillas';

@Component({
  selector: 'app-docs',
  standalone: true,
  imports: [EsquemaDelDocumentoComponent, 
    GuardarPlantillaModalComponent, ImportarDocumentoModalComponent, PlantillasDrawerComponent,
    ArbolDePaginasComponent, PedirUrlModalComponent, ComentariosDelDocumentoComponent,
    ClickableDirective, CommonModule, FormsModule, NgIconComponent, TiptapEditorDirective, EmojiPickerComponent],
  providers: [
    provideIcons({
      lucideFileText, lucidePlus, lucideFolder, lucideMoreVertical,
      lucideChevronRight, lucideChevronDown, lucideSearch,
      lucideSettings, lucideShare2, lucideClock, lucidePin, lucidePinOff,
      lucideBold, lucideItalic, lucideStrikethrough, lucideLink, lucideTrash,
      lucideUpload, lucideWand2, lucideLayoutTemplate, lucideCopy, lucideBookOpen,
      lucideUsers, lucideCalendar, lucideCheckCircle2, lucideStar, lucideFilter,
      lucideArrowUpDown, lucideTag, lucideX, lucideFileUp, lucideBriefcase, lucideCheck,
      lucideTriangleAlert, lucideCode, lucideQuote, lucideUnlink, lucideRemoveFormatting,
      lucideMessageSquare
    })
  ],
  templateUrl: './docs.component.html',
  host: {
    class: 'flex h-full w-full bg-white dark:bg-background overflow-hidden relative',
    '(document:click)': 'onGlobalClick($event)'
  }
})
export class DocsComponent implements OnInit, OnDestroy, AfterViewInit {
  private docsService = inject(DocsService);

  /**
   * Quien busca a qué se puede mencionar.
   *
   * Se declara **antes** que el editor a propósito: los campos se inicializan en orden y el editor
   * lo usa al construirse. Declarado después, `this.menciones` sería `undefined` dentro de la
   * extensión y el desplegable de menciones no encontraría nunca nada —sin dar ningún error—.
   */
  private menciones = inject(MencionesService);

  documents = signal<DocumentDto[]>([]);
  pagesByDoc = signal<Record<string, PageDto[]>>({});
  
  activeDocument = signal<DocumentDto | null>(null);
  activePage = signal<PageDto | null>(null);
  isLoading = signal(false);
  expandedPages = signal<Set<string>>(new Set<string>());
  
  private contentUpdate$ = new Subject<{ pageId: string, title: string, content: string }>();

  /** El título del documento se guarda aparte, y con su propio respiro entre teclas. */
  private tituloDelDocumento$ = new Subject<{ documentId: string, title: string }>();

  private readonly toast = inject(ToastService);

  /**
   * En qué punto está el guardado automático.
   *
   * <b>Antes la cabecera decía «Saved just now» y era una cadena escrita a mano</b>, pintada
   * siempre, sin relación con lo que contestara el servidor. Y el guardado se suscribía sin
   * manejador de error: si la petición fallaba —red, sesión caducada, error del servidor— no
   * ocurría nada. Se podía escribir media hora leyendo «guardado» y perderlo entero al recargar.
   */
  readonly estadoDeGuardado = signal<'quieto' | 'pendiente' | 'guardando' | 'guardado' | 'error'>('quieto');

  /** Cuándo se guardó por última vez de verdad, para poder decir la hora en vez de «ahora». */
  readonly guardadoA = signal<Date | null>(null);

  /** Para desactivar los botones de exportar mientras se genera el fichero. */
  readonly exportando = signal(false);

  /** Cuántas palabras lleva la página abierta. */
  readonly palabras = signal(0);

  /** Si hay una subida en marcha, para poder avisarlo en la cabecera. */
  readonly subiendo = signal(false);

  /** Los comentarios en línea de la página abierta. */
  readonly anotaciones = signal<AnotacionDto[]>([]);

  /** Sobre cuál está el cursor ahora mismo, para destacarla en el panel. */
  readonly anotacionActivaId = signal<string | null>(null);

  /** Si el panel lateral enseña el índice o los comentarios. */
  readonly panelLateral = signal<'esquema' | 'comentarios'>('esquema');

  readonly comentariosAbiertos = computed(() => this.anotaciones().filter(a => !a.resueltaUtc).length);

  /** Qué está pidiendo el modal de dirección, o `null` si no hay ninguno abierto. */
  readonly urlPedida = signal<ClaseDeUrl | null>(null);

  /** Cómo se le contesta al comando que está esperando la dirección. */
  private resolverUrl: ((url: string | null) => void) | null = null;

  /**
   * Abre el modal y espera a que se conteste.
   *
   * Devuelve una promesa porque el comando del editor la espera dentro de su `ejecutar`, que es lo
   * que permite que el rango donde se escribió la barra siga siendo válido al insertar.
   */
  private pedirUrl(clase: ClaseDeUrl): Promise<string | null> {
    // Si ya había uno abierto se cierra contestando que no: dejar la promesa anterior colgada
    // mantendría vivo un comando que ya nadie va a completar.
    this.resolverUrl?.(null);

    this.urlPedida.set(clase);
    return new Promise<string | null>(resolver => { this.resolverUrl = resolver; });
  }

  /**
   * Enlaza lo que hay seleccionado.
   *
   * Reutiliza el mismo modal que el menú `/`: es la misma pregunta —«¿qué dirección?»— y tener dos
   * formas distintas de hacerla acabaría con una validando y la otra no.
   *
   * Guarda la selección antes de abrir el modal. Al enfocarse el campo, el editor la pierde, y sin
   * esto el enlace se aplicaría al cursor en vez de al texto elegido.
   */
  async enlazarSeleccion() {
    const { from, to } = this.editor.state.selection;
    if (from === to) return;

    const url = await this.pedirUrl('enlace');
    if (!url) return;

    this.editor.chain().focus().setTextSelection({ from, to }).setLink({ href: url }).run();
  }

  /**
   * Abre el selector de ficheros del sistema y sube lo que se elija.
   *
   * El `input` se crea y se tira: uno permanente en la plantilla conserva el fichero anterior, así
   * que elegir dos veces el mismo fichero seguido no dispara el `change` la segunda vez.
   */
  private elegirYSubir(): Promise<{ url: string; nombre: string; esImagen: boolean } | null> {
    return new Promise(resolver => {
      const campo = document.createElement('input');
      campo.type = 'file';

      campo.addEventListener('change', async () => {
        const fichero = campo.files?.[0];
        if (!fichero) { resolver(null); return; }

        resolver(await this.subir(fichero));
      });

      // Si se cierra el diálogo sin elegir nada no llega ningún evento en algunos navegadores, así
      // que se resuelve al volver el foco a la ventana. Sin esto la promesa queda colgada y el
      // comando del menú nunca termina.
      window.addEventListener('focus', () => setTimeout(() => resolver(null), 500), { once: true });

      campo.click();
    });
  }

  /** Sube el fichero e inserta lo que corresponda donde diga la posición. */
  private async subirEInsertar(fichero: File, posicion?: number) {
    const subido = await this.subir(fichero);
    if (!subido) return;

    const contenido = subido.esImagen
      ? { type: 'image', attrs: { src: subido.url, alt: subido.nombre } }
      : { type: 'fileAttachment', attrs: { href: subido.url, title: subido.nombre } };

    const cadena = this.editor.chain().focus();
    if (posicion !== undefined) cadena.insertContentAt(posicion, contenido);
    else cadena.insertContent(contenido);
    cadena.run();
  }

  /** La subida en sí, con su aviso si falla. */
  private async subir(fichero: File): Promise<{ url: string; nombre: string; esImagen: boolean } | null> {
    this.subiendo.set(true);

    try {
      const { url } = await firstValueFrom(this.docsService.subirFichero(fichero));
      return { url, nombre: fichero.name, esImagen: fichero.type.startsWith('image/') };
    } catch (err) {
      this.toast.error(
        $localize`No se pudo subir el fichero`,
        $localize`«${fichero.name}» no llegó al servidor.`);
      console.error('No se pudo subir el fichero', err);
      return null;
    } finally {
      this.subiendo.set(false);
    }
  }

  /**
   * Comenta el texto seleccionado.
   *
   * La anotación se crea en el servidor **antes** de marcar el texto, y no al revés: el
   * identificador con el que se marca es el suyo. Marcando primero con uno inventado, un fallo al
   * crear dejaría el documento con una marca que no apunta a ninguna conversación.
   */
  async comentarSeleccion() {
    const pagina = this.activePage();
    if (!pagina) return;

    const { from, to } = this.editor.state.selection;
    if (from === to) return;

    const citado = this.editor.state.doc.textBetween(from, to, ' ').trim();
    if (!citado) return;

    try {
      const anotacionId = await firstValueFrom(this.docsService.crearAnotacion(pagina.id, citado));

      this.editor.chain().focus()
        .setTextSelection({ from, to })
        .marcarComentario(anotacionId)
        .run();

      this.panelLateral.set('comentarios');
      this.anotacionActivaId.set(anotacionId);
      this.cargarAnotaciones(pagina.id);
    } catch (err) {
      this.toast.error($localize`No se pudo crear el comentario`);
      console.error('No se pudo crear la anotación', err);
    }
  }

  /** Lleva el cursor hasta el texto señalado por una anotación. */
  irAAnotacion(anotacion: AnotacionDto) {
    this.anotacionActivaId.set(anotacion.id);

    let encontrada: { from: number; to: number } | null = null;

    this.editor.state.doc.descendants((nodo, pos) => {
      if (encontrada || !nodo.isText) return;

      const tiene = nodo.marks.some(
        m => m.type.name === 'comentario' && m.attrs['anotacionId'] === anotacion.id);

      if (tiene) encontrada = { from: pos, to: pos + nodo.nodeSize };
    });

    // Puede no estar: si alguien borró el texto comentado, la marca se fue con él. La anotación
    // sigue en el panel con su cita, que es justamente para esto.
    if (!encontrada) {
      this.toast.info($localize`El texto comentado ya no está en la página`);
      return;
    }

    const rango = encontrada as { from: number; to: number };
    this.editor.chain().focus().setTextSelection(rango).scrollIntoView().run();
  }

  /** Marca una anotación como resuelta, o la vuelve a abrir. */
  resolverAnotacion(anotacion: AnotacionDto) {
    const pagina = this.activePage();
    if (!pagina) return;

    this.docsService.resolverAnotacion(anotacion.id, !anotacion.resueltaUtc).subscribe({
      next: () => this.cargarAnotaciones(pagina.id),
      error: (err) => {
        this.toast.error($localize`No se pudo cambiar el estado del comentario`);
        console.error('No se pudo resolver la anotación', err);
      }
    });
  }

  /**
   * Quita un comentario en línea: la anotación y su marca en el texto.
   *
   * Las dos cosas, y en este orden. Quitar sólo la anotación dejaría el texto subrayado
   * apuntando a una conversación que ya no existe.
   */
  borrarAnotacion(anotacion: AnotacionDto) {
    const pagina = this.activePage();
    if (!pagina) return;

    this.docsService.borrarAnotacion(anotacion.id).subscribe({
      next: () => {
        this.editor.chain().focus().quitarComentario(anotacion.id).run();
        this.cargarAnotaciones(pagina.id);
      },
      error: (err) => {
        this.toast.error($localize`No se pudo quitar el comentario`);
        console.error('No se pudo borrar la anotación', err);
      }
    });
  }

  private cargarAnotaciones(pageId: string) {
    this.docsService.getAnotaciones(pageId).subscribe({
      next: (anotaciones) => this.anotaciones.set(Array.isArray(anotaciones) ? anotaciones : []),
      // Sin anotaciones el editor sigue siendo utilizable; no merece parar la pantalla.
      error: (err) => console.warn('No se pudieron leer los comentarios del documento', err)
    });
  }

  /** Contesta al comando que esperaba y cierra el modal. */
  responderUrl(url: string | null) {
    this.urlPedida.set(null);
    const resolver = this.resolverUrl;
    this.resolverUrl = null;
    resolver?.(url);
  }

  /** Lo último que no se pudo guardar, para poder reintentarlo sin perderlo. */
  private pendienteDeReintento: { pageId: string; title: string; content: string } | null = null;

  /** Mientras se vuelca una página en el editor, los cambios que emite no son de nadie. */
  private cargandoPagina = false;

  /**
   * Cambia cuando el contenido se guarda, para que el índice se vuelva a leer.
   *
   * Se ata al guardado y no a cada pulsación: recalcular el esquema en cada tecla redibuja la
   * barra lateral mientras se escribe un título, que parpadea justo cuando hace falta concentrarse.
   */
  readonly versionDelEsquema = signal(0);

  // UI state
  searchQuery = signal('');
  isSearchActive = signal(false);
  /**
   * La pestaña del panel de Documentos, <b>leída de la URL</b>.
   *
   * Era estado interno del componente, y por eso Docs necesitaba su propia barra lateral: nadie
   * de fuera podía cambiarla. Al pasarla a `?tab=` la maneja el panel de navegación compartido
   * como el resto de los módulos, y de paso una pestaña se puede compartir por enlace y el botón
   * de atrás funciona.
   */
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);
  private readonly seccionesDelPanel = inject(SeccionesDelPanelService);

  /** `effect` fuera del constructor necesita inyector explícito. */
  private readonly inyector = inject(Injector);

  readonly activeSidebarTab = computed<'all' | 'my' | 'shared' | 'private' | 'meeting-notes' | 'templates' | 'archived'>(() => {
    const tab = this.parametrosDeLaUrl()['tab'];
    return TABS_DE_DOCS.includes(tab as never) ? tab as never : 'all';
  });

  private readonly parametrosDeLaUrl = toSignal(
    inject(ActivatedRoute).queryParams,
    { initialValue: {} as Record<string, string> });
  isPrivateCollapsed = signal(false);
  isPinned = signal(true);
  isHovered = signal(false);
  
  // Dropdowns & Modals
  isNewDocDropdownOpen = signal(false);
  isImportModalOpen = signal(false);
  /** El cajón con todas las plantillas: se abre desde «Ver más» y desde el menú de «Nuevo». */
  isTemplatePickerOpen = signal(false);
  isSaveAsTemplateModalOpen = signal(false);
  activeRowMenuId = signal<string | null>(null);

  // Form states
  selectedDocForTemplate = signal<DocumentDto | null>(null);

  // Sorting & Filtering
  selectedSort = signal<'updated' | 'title' | 'type'>('updated');
  sortDirection = signal<'asc' | 'desc'>('desc');
  selectedTypeFilter = signal<number | null>(null);
  starredDocIds = signal<Set<string>>(new Set<string>());

  @ViewChild('bubbleMenu', { static: false }) bubbleMenuElement!: ElementRef;

  customTemplates = computed(() => {
    return this.documents().filter(d => d.type === 4);
  });

  /**
   * Cuántas veces se ha usado cada plantilla, tal como lo cuenta el servidor.
   *
   * Se pide una vez al entrar y se vuelve a pedir cada vez que se crea desde una plantilla, que
   * es lo único que lo cambia.
   */
  private readonly usosDePlantilla = signal<ReadonlyMap<string, number>>(new Map());

  /** Las del sistema y las del equipo, mezcladas y ordenadas por uso. */
  readonly plantillas = computed<PlantillaDisponible[]>(
    () => plantillasDisponibles(this.customTemplates(), this.usosDePlantilla()));

  /** Las cuatro de la galería. El resto vive detrás de «Ver más». */
  readonly plantillasDestacadas = computed(() => this.plantillas().slice(0, PLANTILLAS_A_LA_VISTA));

  readonly hayMasPlantillas = computed(() => this.plantillas().length > PLANTILLAS_A_LA_VISTA);

  private cargarUsosDePlantilla(): void {
    this.docsService.getUsosDePlantilla().subscribe({
      // Se comprueba la forma antes de recorrerla. Una respuesta que no sea la lista esperada
      // —un proxy que devuelve un objeto de error con 200, por ejemplo— reventaría aquí dentro y
      // se llevaría por delante la pantalla entera de Documentos por un contador de adorno.
      next: (usos) => this.usosDePlantilla.set(Array.isArray(usos)
        ? new Map(usos.map(u => [u.clave, u.veces]))
        : new Map()),
      // Sin contadores la galería sigue siendo utilizable: se ve el orden de declaración. No
      // merece parar la pantalla ni enseñar un error por esto.
      error: (err) => console.warn('No se pudieron leer los usos de las plantillas', err)
    });
  }

  filteredDocuments = computed(() => {
    let docs = this.documents();
    const query = this.searchQuery().toLowerCase().trim();
    const tab = this.activeSidebarTab();
    const typeFilter = this.selectedTypeFilter();

    // Filter by Tab
    if (tab === 'meeting-notes') {
      docs = docs.filter(d => d.type === 3);
    } else if (tab === 'private') {
      docs = docs.filter(d => d.type === 1);
    } else if (tab === 'templates') {
      docs = docs.filter(d => d.type === 4);
    }

    // Filter by Type dropdown
    if (typeFilter !== null) {
      docs = docs.filter(d => d.type === typeFilter);
    }
    
    // Filter by Search Query
    if (query) {
      docs = docs.filter(d => (d.title || '').toLowerCase().includes(query) || (d.description || '').toLowerCase().includes(query));
    }
    
    // Sorting
    const dir = this.sortDirection() === 'asc' ? 1 : -1;
    const sortKey = this.selectedSort();

    return [...docs].sort((a, b) => {
      if (sortKey === 'title') {
        return (a.title || '').localeCompare(b.title || '') * dir;
      }
      if (sortKey === 'type') {
        return (a.type - b.type) * dir;
      }
      // Default: updated date
      const dateA = new Date(a.updatedAtUtc || a.createdAtUtc || 0).getTime();
      const dateB = new Date(b.updatedAtUtc || b.createdAtUtc || 0).getTime();
      return (dateA - dateB) * dir;
    });
  });

  starredDocuments = computed(() => {
    const set = this.starredDocIds();
    return this.documents().filter(d => set.has(d.id));
  });

  editor = new Editor({
    extensions: [
      StarterKit,
      Image,
      Youtube,
      FileAttachment,

      // El menú `/` no sabe pedir una dirección: se le inyecta cómo, igual que a las menciones se
      // les inyecta el buscador. Antes lo hacía con `window.prompt`, que bloquea la pestaña.
      SlashCommand.configure({
        pedirUrl: (clase) => this.pedirUrl(clase),
        subirFichero: () => this.elegirYSubir()
      }),

      /**
       * Arrastrar un fichero al editor, o pegarlo desde el portapapeles.
       *
       * Es la forma en que se mete una captura en un documento, y no existía: la única vía era
       * pedir una dirección de una imagen que ya estuviera publicada en algún sitio.
       */
      FileHandler.configure({
        onDrop: (editorActual, ficheros, posicion) => {
          for (const fichero of ficheros) void this.subirEInsertar(fichero, posicion);
        },
        onPaste: (editorActual, ficheros) => {
          for (const fichero of ficheros) void this.subirEInsertar(fichero);
        }
      }),

      // Menciones `@persona` y `#tarea`. El buscador se inyecta aquí y no dentro de la extensión
      // porque la extensión no puede —ni debe— saber llamar a la API: sabe escribir el nodo con
      // el formato que el servidor lee, y nada más.
      Mencion.configure({ buscador: (disparador, consulta) => this.menciones.buscar(disparador, consulta) }),
      // ── Los bloques que faltaban ──────────────────────────────────────────────────────────
      //
      // Sin ellos el editor sólo daba formato al texto; con ellos se puede estructurar un
      // documento largo, que es lo que se pedía de Notion y de ClickUp.

      /** Desplegables: la forma de tener un documento largo que no abruma. */
      Details.configure({ persist: true, HTMLAttributes: { class: 'desplegable' } }),
      DetailsSummary,
      DetailsContent,

      /** El recuadro de «ojo con esto». Escrito aquí: no hay extensión oficial. */
      Aviso,

      /** Dos o tres columnas lado a lado. Tampoco hay extensión oficial. */
      Columnas,
      Columna,

      /**
       * La marca de los comentarios en línea.
       *
       * Sólo dice «aquí hay una conversación y se llama así». El hilo lo guarda el módulo
       * Comments y el anclaje —qué se citó, si está resuelto— lo guarda Docs.
       */
      Comentario,

      /**
       * Código coloreado, con el lenguaje elegible.
       *
       * `common` trae los lenguajes habituales en vez de los ~190 de `all`, que pesan más que el
       * resto del editor junto.
       */
      BloqueDeCodigo.configure({ lowlight: createLowlight(common) }),

      Highlight.configure({ multicolor: false }),

      /** Comillas, guiones y flechas al escribir. No cambia lo guardado: cambia lo que se teclea. */
      Typography,

      CharacterCount,

      /**
       * El asa para arrastrar bloques.
       *
       * Es lo que define a estos editores: cada bloque es un objeto que se puede agarrar. Hasta
       * ahora reordenar dos párrafos era cortar y pegar.
       */
      DragHandle.configure({
        render: () => {
          const asa = document.createElement('div');
          asa.className = 'asa-de-bloque';
          asa.setAttribute('aria-hidden', 'true');
          asa.textContent = '⠿';
          return asa;
        }
      }),

      Table.configure({ resizable: true }),
      TableRow,
      TableHeader,
      TableCell,
      TaskList,
      TaskItem.configure({ nested: true }),
      Link.configure({ openOnClick: false }),
      Placeholder.configure({
        placeholder: ({ node }) => {
          if (node.type.name === 'heading') {
            return $localize`Encabezado…`;
          }
          return $localize`Escribe / para los comandos, o empieza a escribir…`;
        },
      })
    ],
    editorProps: {
      attributes: {
        class: 'prose prose-sm sm:prose-base dark:prose-invert prose-zinc max-w-none focus:outline-none min-h-[500px]',
      },
    },
    onUpdate: ({ editor }) => {
      if (this.cargandoPagina) return;

      // El contador se lee del editor en cada cambio: la extensión lo calcula igual, y sin esto
      // `CharacterCount` sería otra extensión cargada que no hace nada, que es justo lo que hemos
      // estado quitando.
      this.palabras.set(editor.storage['characterCount'].words());

      const page = this.activePage();
      const doc = this.activeDocument();
      if (page && doc) {
        // «Pendiente» se pone aquí, no en el guardado: entre la última tecla y la petición pasa
        // un segundo entero, y durante ese segundo la cabecera decía «guardado» aunque hubiera
        // cambios sin mandar.
        this.estadoDeGuardado.set('pendiente');

        this.contentUpdate$.next({
          pageId: page.id,
          title: page.title,
          content: editor.getHTML()
        });

        this.versionDelEsquema.update(v => v + 1);
      }
    }
  });

  ngOnInit() {
    this.loadDocuments();
    this.cargarUsosDePlantilla();

    // Los favoritos y las páginas recientes se le entregan al panel compartido. Son datos de
    // Documentos, así que los pone Documentos; el panel sólo sabe pintar secciones.
    //
    // Va en un `effect` porque las dos listas cambian —al marcar una estrella, al abrir un
    // documento— y una entrega única dejaría el panel enseñando lo de hace un rato.
    effect(() => {
      this.seccionesDelPanel.registrar('docs', [
        {
          titulo: $localize`Favoritos`,
          siNoHayNada: $localize`Marca un documento con la estrella para verlo aquí.`,
          elementos: this.starredDocuments().map(doc => ({
            id: doc.id,
            etiqueta: doc.title,
            icono: 'lucideStar',
            alPulsar: () => this.selectDocument(doc)
          }))
        },
        {
          titulo: $localize`Páginas recientes`,
          elementos: this.documents().slice(0, 5).map(doc => ({
            id: 'reciente-' + doc.id,
            etiqueta: doc.title,
            icono: 'lucideBookOpen',
            alPulsar: () => this.selectDocument(doc)
          }))
        }
      ]);
    }, { injector: this.inyector });

    this.contentUpdate$.pipe(
      debounceTime(1000)
    ).subscribe(update => this.guardar(update));

    this.tituloDelDocumento$.pipe(
      debounceTime(700)
    ).subscribe(({ documentId, title }) => {
      const limpio = title.trim();
      // Un título vacío lo rechaza el servidor. Se deja de mandar en vez de enseñar un error por
      // cada tecla mientras alguien borra el título para escribir otro.
      if (!limpio) return;

      this.docsService.renameDocument(documentId, { title: limpio }).subscribe({
        next: () => this.guardadoA.set(new Date()),
        error: (err) => {
          this.estadoDeGuardado.set('error');
          this.toast.error($localize`No se pudo renombrar el documento`);
          console.error('No se pudo renombrar el documento', err);
        }
      });
    });
  }

  ngAfterViewInit() {
    if (this.bubbleMenuElement) {
      this.editor.extensionManager.extensions.push(
        BubbleMenu.configure({
          element: this.bubbleMenuElement.nativeElement
        })
      );
    }
  }

  ngOnDestroy() {
    this.editor.destroy();

    // Si no se limpian, al salir de Documentos el panel de Tareas seguiría enseñando documentos
    // favoritos: las secciones viven en un servicio global.
    this.seccionesDelPanel.limpiar('docs');
  }

  onGlobalClick(event: Event) {
    const target = event.target as HTMLElement;
    if (!target.closest('.dropdown-container')) {
      this.isNewDocDropdownOpen.set(false);
      this.activeRowMenuId.set(null);
    }
  }

  loadDocuments() {
    this.isLoading.set(true);
    this.docsService.getDocuments().subscribe({
      next: (docs) => {
        this.documents.set(docs);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading documents:', err);
        this.isLoading.set(false);
      }
    });
  }

  toggleNewDocDropdown(event?: Event) {
    if (event) event.stopPropagation();
    this.isNewDocDropdownOpen.update(v => !v);
  }

  toggleRowMenu(docId: string, event: Event) {
    event.stopPropagation();
    this.activeRowMenuId.update(current => current === docId ? null : docId);
  }

  createNewDoc(type = 1, event?: Event) {
    if (event) event.stopPropagation();
    this.isNewDocDropdownOpen.set(false);

    const title = type === 2 ? $localize`Untitled Wiki` : $localize`Untitled Document`;
    this.docsService.createDocument({
      title,
      description: '',
      type
    }).subscribe({
      next: (id: any) => {
        const cleanId = typeof id === 'string' ? id.replace(/['"]/g, '') : (id?.value || id?.id || id);
        this.docsService.getDocuments().subscribe({
          next: (docs) => {
            this.documents.set(docs);
            const newDoc = docs.find(d => d.id === cleanId) || docs[0];
            if (newDoc) this.selectDocument(newDoc);
          }
        });
      },
      error: (err) => {
        console.error('Error creating doc:', err);
        alert('Error creating document');
      }
    });
  }

  /**
   * Crea desde una plantilla de la galería o del cajón, sea del sistema o del equipo.
   *
   * De dónde sale se sabe por `esPropia`, no adivinando si la clave parece un identificador:
   * el título de una plantilla del equipo puede ser cualquier cosa, y la clave del sistema
   * también podría serlo el día que alguien añada una.
   */
  usarPlantilla(plantilla: PlantillaDisponible, event?: Event) {
    if (event) event.stopPropagation();

    this.crearDesdePlantilla(plantilla.esPropia
      ? { templateDocumentId: plantilla.clave }
      : { templateKey: plantilla.clave });
  }

  /**
   * Crea un documento desde una plantilla, sea predefinida o propia.
   *
   * Los dos casos eran copias del mismo flujo y sólo se diferencian en el parámetro que
   * se envía. Tenerlo una vez evita que uno se arregle y el otro no.
   */
  private crearDesdePlantilla(req: { templateKey?: string; templateDocumentId?: string }) {
    this.isNewDocDropdownOpen.set(false);
    this.isTemplatePickerOpen.set(false);
    this.isLoading.set(true);

    this.docsService.createFromTemplate(req).subscribe({
      next: (id) => {
        // El contador que acaba de subir es el que ordena la galería; sin releerlo, la plantilla
        // recién usada no se movería de sitio hasta la siguiente visita.
        this.cargarUsosDePlantilla();
        this.abrirDocumentoCreado(id);
      },
      error: (err) => {
        console.error('Error creating from template:', err);
        this.isLoading.set(false);
      }
    });
  }

  /**
   * Recarga el listado y abre el documento recién creado.
   *
   * Lo repetían por igual la importación y las dos vías de plantilla. El identificador
   * llega ya limpio: la normalización vive en DocsService, donde entra el dato.
   */
  private abrirDocumentoCreado(id: string) {
    this.docsService.getDocuments().subscribe({
      next: (docs) => {
        this.documents.set(docs);
        this.isLoading.set(false);
        const creado = docs.find(d => d.id === id);
        if (creado) this.selectDocument(creado);
      },
      error: () => this.isLoading.set(false)
    });
  }

  openSaveAsTemplateModal(doc: DocumentDto, event?: Event) {
    if (event) event.stopPropagation();
    this.activeRowMenuId.set(null);
    this.selectedDocForTemplate.set(doc);
    this.isSaveAsTemplateModalOpen.set(true);
  }

  /** El modal ya guardó; aquí sólo queda reflejarlo en el listado. */
  alGuardarPlantilla() {
    this.isSaveAsTemplateModalOpen.set(false);
    this.loadDocuments();
  }

  /** El modal ya importó; se abre el documento recién creado. */
  alImportar(id: string) {
    this.isImportModalOpen.set(false);
    this.abrirDocumentoCreado(id);
  }

  openImportModal(event?: Event) {
    if (event) event.stopPropagation();
    this.isNewDocDropdownOpen.set(false);
    this.isImportModalOpen.set(true);
  }

  toggleStar(docId: string, event?: Event) {
    if (event) event.stopPropagation();
    this.starredDocIds.update(set => {
      const newSet = new Set(set);
      if (newSet.has(docId)) newSet.delete(docId);
      else newSet.add(docId);
      return newSet;
    });
  }

  selectDocument(doc: DocumentDto) {
    this.activeDocument.set(doc);
    
    this.docsService.getPages(doc.id).subscribe(pages => {
      this.pagesByDoc.update(dict => ({ ...dict, [doc.id]: pages }));
      if (pages.length > 0) {
        this.selectPage(pages[0]);
      } else {
        this.createNewPage(doc.id, $localize`Sin título`);
      }
    });
  }

  /** Las páginas del documento abierto, que es lo que pinta el árbol. */
  readonly paginasDelDocumento = computed<PageDto[]>(() => {
    const doc = this.activeDocument();
    return doc ? this.pagesByDoc()[doc.id] ?? [] : [];
  });

  createNewPage(documentId: string, title = $localize`Sin título`, parentPageId?: string) {
    this.docsService.createPage(documentId, { title, parentPageId }).subscribe({
      next: (id) => {
        const cleanId = typeof id === 'string' ? id.replace(/['"]/g, '') : (id as any)?.value || id;
        this.recargarPaginas(documentId, cleanId);
      },
      error: (err) => {
        this.toast.error($localize`No se pudo crear la página`);
        console.error('No se pudo crear la página', err);
      }
    });
  }

  /** Desde el árbol: `null` crea una página de primer nivel, un id crea una subpágina. */
  crearPaginaDesdeElArbol(parentPageId: string | null) {
    const doc = this.activeDocument();
    if (doc) this.createNewPage(doc.id, $localize`Sin título`, parentPageId ?? undefined);
  }

  /**
   * Mueve una página y **espera al servidor antes de repintar**.
   *
   * Aquí no se adelanta el cambio en pantalla, al revés que en el tablero de tickets: el servidor
   * renumera a todas las hermanas, así que adivinar el resultado en el cliente sería reimplementar
   * esa renumeración y las dos versiones acabarían discrepando. Mover una página es una acción
   * puntual, no un arrastre continuo, y la espera no se percibe.
   */
  moverPagina(movimiento: MovimientoDePagina) {
    const doc = this.activeDocument();
    if (!doc) return;

    this.docsService.movePage(movimiento.pagina.id, {
      parentPageId: movimiento.padreId,
      order: movimiento.orden
    }).subscribe({
      next: () => this.recargarPaginas(doc.id),
      error: (err) => {
        this.toast.error($localize`No se pudo mover la página`, this.motivoDelError(err));
        console.error('No se pudo mover la página', err);
      }
    });
  }

  /**
   * Manda una página a la papelera.
   *
   * La última no se borra. Un documento sin páginas no se puede abrir —la pantalla del editor
   * exige documento **y** página— así que quedaría inalcanzable desde el listado sin haberse
   * borrado.
   */
  borrarPagina(pagina: PageDto) {
    const doc = this.activeDocument();
    if (!doc || this.paginasDelDocumento().length <= 1) return;

    this.docsService.deletePage(doc.id, pagina.id).subscribe({
      next: () => {
        if (this.activePage()?.id === pagina.id) this.activePage.set(null);
        this.recargarPaginas(doc.id);
      },
      error: (err) => {
        this.toast.error($localize`No se pudo borrar la página`);
        console.error('No se pudo borrar la página', err);
      }
    });
  }

  /** Vuelve a leer el árbol y deja abierta la página que se diga, o la que ya lo estaba. */
  private recargarPaginas(documentId: string, abrirId?: string) {
    this.docsService.getPages(documentId).subscribe({
      next: (pages) => {
        this.pagesByDoc.update(dict => ({ ...dict, [documentId]: pages }));

        const buscada = abrirId ? pages.find(p => p.id === abrirId) : null;
        if (buscada) { this.selectPage(buscada); return; }

        const abierta = this.activePage();
        if (!abierta || !pages.some(p => p.id === abierta.id)) {
          if (pages.length > 0) this.selectPage(pages[0]);
        }
      },
      error: (err) => console.error('No se pudieron leer las páginas', err)
    });
  }

  /** El texto que manda el servidor cuando rechaza un movimiento, si viene en algo legible. */
  private motivoDelError(err: unknown): string | undefined {
    const cuerpo = (err as { error?: unknown })?.error;
    return typeof cuerpo === 'string' && cuerpo.length < 200 ? cuerpo : undefined;
  }

  togglePageExpansion(event: Event, pageId: string) {
    event.stopPropagation();
    this.expandedPages.update(set => {
      const newSet = new Set(set);
      if (newSet.has(pageId)) newSet.delete(pageId);
      else newSet.add(pageId);
      return newSet;
    });
  }

  selectPage(page: PageDto) {
    // Abrir una página no es editarla. Sin esta bandera, cargar el contenido en el editor
    // dispara `onUpdate`, y la cabecera pasaba a «Guardado a las HH:MM» **por haber abierto el
    // documento**, además de reescribir en el servidor lo mismo que acababa de leer.
    this.cargandoPagina = true;
    this.activePage.set(page);
    // Vacío, no un texto de ejemplo. Antes se metía «Start typing or use / for commands…» **como
    // contenido**, así que se guardaba en la página y había que borrarlo a mano. El aviso lo pone
    // la extensión `Placeholder`, que no escribe nada en el documento.
    this.editor.commands.setContent(page.content || '');
    setTimeout(() => {
      this.editor.commands.focus();
      this.cargandoPagina = false;

      // Al abrir tampoco lo cuenta nadie, porque `onUpdate` no llega a ejecutarse.
      this.palabras.set(this.editor.storage['characterCount'].words());
    }, 50);

    this.anotacionActivaId.set(null);
    this.cargarAnotaciones(page.id);
  }

  closeEditorView() {
    this.activeDocument.set(null);
    this.activePage.set(null);
  }

  insertEmoji(emoji: string) {
    this.editor.chain().focus().insertContent(emoji).run();
  }

  /**
   * Guarda el documento en PDF.
   *
   * `html2pdf.js` estaba en las dependencias y **no se importaba en ningún sitio**, así que
   * `window.html2pdf` era siempre `undefined` y el botón caía al `window.print()` de reserva, que
   * imprime la aplicación entera con su barra lateral en vez del documento.
   *
   * Se carga en el momento y no arriba del fichero: son unos 700 kB que sólo hacen falta si
   * alguien pulsa el botón, y cargarlos siempre los mete en el paquete de Documentos.
   */
  async exportPdf() {
    const contenido = document.querySelector('.ProseMirror');
    if (!contenido) return;

    this.exportando.set(true);
    try {
      const { default: html2pdf } = await import('html2pdf.js');

      await html2pdf()
        .set({ margin: 10, filename: `${this.activePage()?.title || 'documento'}.pdf` })
        .from(contenido as HTMLElement)
        .save();
    } catch (err) {
      this.toast.error($localize`No se pudo generar el PDF`);
      console.error('No se pudo generar el PDF', err);
    } finally {
      this.exportando.set(false);
    }
  }

  /**
   * Descarga el documento en HTML.
   *
   * Antes hacía `window.open` de la URL de exportación. Una pestaña nueva no lleva la cabecera de
   * sesión y el endpoint la exige: **el botón devolvía 401 siempre**, y como se abría en otra
   * pestaña, ni siquiera se veía el error.
   */
  exportHtml() {
    const doc = this.activeDocument();
    if (!doc) return;

    this.exportando.set(true);
    this.docsService.exportarHtml(doc.id).subscribe({
      next: (respuesta) => {
        this.exportando.set(false);

        const cuerpo = respuesta.body;
        if (!cuerpo) {
          this.toast.error($localize`La descarga llegó vacía.`);
          return;
        }

        const url = URL.createObjectURL(cuerpo);
        try {
          const enlace = document.createElement('a');
          enlace.href = url;
          enlace.download = `${doc.title || 'documento'}.html`;
          enlace.click();
        } finally {
          // Sin esto, cada descarga deja el fichero entero retenido en memoria mientras la
          // pestaña siga abierta.
          URL.revokeObjectURL(url);
        }
      },
      error: (err) => {
        this.exportando.set(false);
        this.toast.error($localize`No se pudo exportar el documento`);
        console.error('No se pudo exportar el documento', err);
      }
    });
  }

  /**
   * Renombra la <b>página</b> activa. Va por el guardado automático, como el contenido.
   *
   * Antes este método lo llamaban los dos campos de título —el del documento y el de la miga de
   * pan—, así que escribir el título del documento renombraba la página por debajo mientras la
   * pantalla seguía enseñando el título viejo del documento.
   */
  renombrarPagina(newTitle: string) {
    const current = this.activePage();
    if (!current) return;

    this.activePage.set({ ...current, title: newTitle });
    this.pagesByDoc.update(dict => {
      const docId = current.documentId;
      const paginas = dict[docId];
      if (!paginas) return dict;
      return { ...dict, [docId]: paginas.map(p => p.id === current.id ? { ...p, title: newTitle } : p) };
    });

    this.estadoDeGuardado.set('pendiente');
    this.contentUpdate$.next({
      pageId: current.id,
      title: newTitle,
      content: this.editor.getHTML()
    });
  }

  /**
   * Renombra el <b>documento</b>.
   *
   * Necesitó endpoint nuevo: el módulo sólo publicaba borrar documento, borrar página y actualizar
   * página, así que este campo no podía funcionar de ninguna manera.
   */
  renombrarDocumento(newTitle: string) {
    const doc = this.activeDocument();
    if (!doc) return;

    this.activeDocument.set({ ...doc, title: newTitle });
    this.documents.update(docs => docs.map(d => d.id === doc.id ? { ...d, title: newTitle } : d));

    this.tituloDelDocumento$.next({ documentId: doc.id, title: newTitle });
  }

  /**
   * Manda el contenido al servidor y cuenta lo que pasa.
   *
   * El error no se traga: se enseña en la cabecera, se avisa una vez, y lo que no se pudo guardar
   * queda apartado para reintentarlo. Perder el texto de alguien porque caducó una sesión es el
   * peor fallo que puede tener un editor, y era el que tenía.
   */
  private guardar(update: { pageId: string; title: string; content: string }) {
    this.estadoDeGuardado.set('guardando');

    this.docsService.updatePage(update.pageId, {
      title: update.title,
      content: update.content
    }).subscribe({
      next: () => {
        this.pendienteDeReintento = null;
        this.guardadoA.set(new Date());
        this.estadoDeGuardado.set('guardado');
      },
      error: (err) => {
        // Se guarda lo que falló, no lo que hay ahora en el editor: si alguien cambia de página
        // tras el fallo, el reintento tiene que mandar el texto que no llegó, no el de la página
        // nueva.
        this.pendienteDeReintento = update;
        this.estadoDeGuardado.set('error');

        this.toast.error(
          $localize`No se pudo guardar`,
          $localize`Los cambios siguen en pantalla. Vuelve a intentarlo desde la cabecera.`);

        console.error('No se pudo guardar la página', err);
      }
    });
  }

  /** Reintenta lo último que no se pudo guardar. */
  reintentarGuardado() {
    if (this.pendienteDeReintento) this.guardar(this.pendienteDeReintento);
  }

  toggleSearch() {
    this.isSearchActive.update(v => !v);
    if (!this.isSearchActive()) {
      this.searchQuery.set('');
    }
  }

  /**
   * Cambia de pestaña navegando, no tocando una señal.
   *
   * Lo sigue usando el botón «volver a todos los documentos» de la cabecera del editor. La
   * navegación es la que mueve la pestaña; cerrar el documento abierto es lo único que queda
   * aquí, porque de eso la URL no dice nada.
   */
  setSidebarTab(tab: 'all' | 'my' | 'shared' | 'private' | 'meeting-notes' | 'archived') {
    this.activeDocument.set(null);
    this.activePage.set(null);

    void this.router.navigate([], {
      relativeTo: this.ruta,
      queryParams: { tab: tab === 'all' ? null : tab },
      queryParamsHandling: 'merge'
    });
  }

  togglePrivate() {
    this.isPrivateCollapsed.update(v => !v);
  }

  togglePin(event: Event) {
    event.stopPropagation();
    this.isPinned.update(v => !v);
  }

  deleteDocument(event: Event, id: string) {
    event.stopPropagation();
    this.activeRowMenuId.set(null);
    if (confirm('Are you sure you want to delete this document?')) {
      this.docsService.deleteDocument(id).subscribe({
        next: () => {
          this.documents.update(docs => docs.filter(d => d.id !== id));
          if (this.activeDocument()?.id === id) {
            this.activeDocument.set(null);
            this.activePage.set(null);
          }
        },
        error: (err: any) => {
          console.error('Error deleting doc:', err);
          alert('Error deleting document');
        }
      });
    }
  }

  deletePage(event: Event, docId: string, pageId: string) {
    event.stopPropagation();
    if (confirm('Are you sure you want to delete this page?')) {
      this.docsService.deletePage(docId, pageId).subscribe({
        next: () => {
          this.docsService.getPages(docId).subscribe(pages => {
            this.pagesByDoc.update(dict => ({ ...dict, [docId]: pages }));
            if (this.activePage()?.id === pageId) {
              this.activePage.set(null);
              this.editor.commands.clearContent();
            }
          });
        },
        error: (err: any) => console.error('Error deleting page:', err)
      });
    }
  }

  getTypeLabel(type: number): string {
    switch (type) {
      case 2: return 'Wiki';
      case 3: return 'Meeting Note';
      case 4: return 'Template';
      default: return 'List';
    }
  }

  getTypeBadgeClass(type: number): string {
    switch (type) {
      case 2: return 'bg-primary-subtle text-primary-subtle-fg dark:bg-primary-subtle/40 dark:text-primary-subtle-fg border-primary dark:border-primary/50';
      case 3: return 'bg-warning-subtle text-warning-subtle-fg dark:bg-warning-subtle/40 dark:text-warning-subtle-fg border-warning dark:border-warning/50';
      case 4: return 'bg-primary-subtle text-primary-subtle-fg dark:bg-primary-subtle/40 dark:text-primary-subtle-fg border-primary dark:border-primary/50';
      default: return 'bg-muted text-muted-foreground dark:bg-muted dark:text-muted-foreground border-border dark:border-border';
    }
  }
}