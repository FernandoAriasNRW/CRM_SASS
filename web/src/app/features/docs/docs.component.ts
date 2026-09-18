import { Component, effect, inject, signal, computed, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewInit, Injector } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
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
import { AnnotationDto, DocsService, DocumentDto, PageDto } from './docs.service';
import { SeccionesDelPanelService } from '../../shared/ui/panel-de-navegacion/secciones-del-panel.service';

/** Las pestañas que el panel de Documentos sabe abrir. Cualquier otra cosa en `?tab=` cae en «all». */
const DOCS_TABS = ['all', 'my', 'shared', 'private', 'meeting-notes', 'templates', 'archived'] as const;
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import Youtube from '@tiptap/extension-youtube';
import SlashCommand from './extensions/slash-command';
import { Mention } from './extensions/mention';
import { MentionsService } from './mentions.service';
import { DocumentOutlineComponent } from './document-outline.component';
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
import { Callout } from './extensions/callout';
import { CodeBlock } from './extensions/code-block';
import FileHandler from '@tiptap/extension-file-handler';
import { CommentMark } from './extensions/comment-mark';
import { Column, Columns } from './extensions/columns';
import { DocumentCommentsComponent } from './document-comments.component';
import { EmojiPickerComponent } from './extensions/emoji-picker.component';
import { firstValueFrom } from 'rxjs';
import { ClickableDirective } from '../../shared/directives/clickable.directive';
import { SaveTemplateModalComponent } from './modals/save-template-modal.component';
import { ImportDocumentModalComponent } from './modals/import-document-modal.component';
import { TemplatesDrawerComponent } from './templates-drawer.component';
import { PageTreeComponent, PageMove } from './page-tree.component';
import { UrlKind, PromptUrlModalComponent } from './modals/prompt-url-modal.component';
import { ToastService } from '../../shared/services/toast.service';
import { DocumentSaveService } from './document-save.service';
import { DocumentExportService } from './document-export.service';
import { VISIBLE_TEMPLATES, AvailableTemplate, availableTemplates } from './templates';

@Component({
  selector: 'app-docs',
  standalone: true,
  imports: [DocumentOutlineComponent, 
    SaveTemplateModalComponent, ImportDocumentModalComponent, TemplatesDrawerComponent,
    PageTreeComponent, PromptUrlModalComponent, DocumentCommentsComponent,
    ClickableDirective, CommonModule, FormsModule, NgIconComponent, TiptapEditorDirective, EmojiPickerComponent],
  providers: [
    DocumentSaveService,
    DocumentExportService,
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
  private mentions = inject(MentionsService);

  documents = signal<DocumentDto[]>([]);
  pagesByDoc = signal<Record<string, PageDto[]>>({});
  
  activeDocument = signal<DocumentDto | null>(null);
  activePage = signal<PageDto | null>(null);
  isLoading = signal(false);
  

  private readonly toast = inject(ToastService);

  /**
   * El guardado automático y la exportación, cada uno en su servicio. Ver
   * `DocumentSaveService` y `DocumentExportService`: el componente sólo orquesta.
   *
   * Se reexponen sus señales con el mismo nombre para que la plantilla no tenga que saber de
   * dónde salen.
   */
  private readonly saving = inject(DocumentSaveService);
  private readonly exports = inject(DocumentExportService);
  readonly saveState = this.saving.state;
  readonly savedAt = this.saving.savedAt;
  readonly exporting = this.exports.exporting;

  /** Cuántas palabras lleva la página abierta. */
  readonly words = signal(0);

  /** Si hay una subida en marcha, para poder avisarlo en la cabecera. */
  readonly uploading = signal(false);

  /** Los comentarios en línea de la página abierta. */
  readonly annotations = signal<AnnotationDto[]>([]);

  /** Sobre cuál está el cursor ahora mismo, para destacarla en el panel. */
  readonly activeAnnotationId = signal<string | null>(null);

  /** Si el panel lateral enseña el índice o los comentarios. */
  readonly sidePanel = signal<'outline' | 'comments'>('outline');

  readonly openComments = computed(() => this.annotations().filter(a => !a.resolvedAtUtc).length);

  /** Qué está pidiendo el modal de dirección, o `null` si no hay ninguno abierto. */
  readonly requestedUrl = signal<UrlKind | null>(null);

  /** Cómo se le contesta al comando que está esperando la dirección. */
  private resolveUrl: ((url: string | null) => void) | null = null;

  /**
   * Abre el modal y espera a que se conteste.
   *
   * Devuelve una promesa porque el comando del editor la espera dentro de su `ejecutar`, que es lo
   * que permite que el rango donde se escribió la barra siga siendo válido al insertar.
   */
  private promptUrl(kind: UrlKind): Promise<string | null> {
    // Si ya había uno abierto se cierra contestando que no: dejar la promesa anterior colgada
    // mantendría vivo un comando que ya nadie va a completar.
    this.resolveUrl?.(null);

    this.requestedUrl.set(kind);
    return new Promise<string | null>(resolve => { this.resolveUrl = resolve; });
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
  async linkSelection() {
    const { from, to } = this.editor.state.selection;
    if (from === to) return;

    const url = await this.promptUrl('link');
    if (!url) return;

    this.editor.chain().focus().setTextSelection({ from, to }).setLink({ href: url }).run();
  }

  /**
   * Abre el selector de ficheros del sistema y sube lo que se elija.
   *
   * El `input` se crea y se tira: uno permanente en la plantilla conserva el fichero anterior, así
   * que elegir dos veces el mismo fichero seguido no dispara el `change` la segunda vez.
   */
  private pickAndUpload(): Promise<{ url: string; name: string; isImage: boolean } | null> {
    return new Promise(resolve => {
      const field = document.createElement('input');
      field.type = 'file';

      field.addEventListener('change', async () => {
        const file = field.files?.[0];
        if (!file) { resolve(null); return; }

        resolve(await this.upload(file));
      });

      // Si se cierra el diálogo sin elegir nada no llega ningún evento en algunos navegadores, así
      // que se resuelve al volver el foco a la ventana. Sin esto la promesa queda colgada y el
      // comando del menú nunca termina.
      window.addEventListener('focus', () => setTimeout(() => resolve(null), 500), { once: true });

      field.click();
    });
  }

  /** Sube el fichero e inserta lo que corresponda donde diga la posición. */
  private async uploadAndInsert(file: File, position?: number) {
    const uploaded = await this.upload(file);
    if (!uploaded) return;

    const content = uploaded.isImage
      ? { type: 'image', attrs: { src: uploaded.url, alt: uploaded.name } }
      : { type: 'fileAttachment', attrs: { href: uploaded.url, title: uploaded.name } };

    const chain = this.editor.chain().focus();
    if (position !== undefined) chain.insertContentAt(position, content);
    else chain.insertContent(content);
    chain.run();
  }

  /** La subida en sí, con su aviso si falla. */
  private async upload(file: File): Promise<{ url: string; name: string; isImage: boolean } | null> {
    this.uploading.set(true);

    try {
      const { url } = await firstValueFrom(this.docsService.uploadFile(file));
      return { url, name: file.name, isImage: file.type.startsWith('image/') };
    } catch (err) {
      this.toast.error(
        $localize`No se pudo subir el fichero`,
        $localize`«${file.name}» no llegó al servidor.`);
      console.error('No se pudo subir el fichero', err);
      return null;
    } finally {
      this.uploading.set(false);
    }
  }

  /**
   * Comenta el texto seleccionado.
   *
   * La anotación se crea en el servidor **antes** de marcar el texto, y no al revés: el
   * identificador con el que se marca es el suyo. Marcando primero con uno inventado, un fallo al
   * crear dejaría el documento con una marca que no apunta a ninguna conversación.
   */
  async commentSelection() {
    const page = this.activePage();
    if (!page) return;

    const { from, to } = this.editor.state.selection;
    if (from === to) return;

    const quoted = this.editor.state.doc.textBetween(from, to, ' ').trim();
    if (!quoted) return;

    try {
      const anotacionId = await firstValueFrom(this.docsService.createAnnotation(page.id, quoted));

      this.editor.chain().focus()
        .setTextSelection({ from, to })
        .setComment(anotacionId)
        .run();

      this.sidePanel.set('comments');
      this.activeAnnotationId.set(anotacionId);
      this.loadAnnotations(page.id);
    } catch (err) {
      this.toast.error($localize`No se pudo crear el comentario`);
      console.error('No se pudo crear la anotación', err);
    }
  }

  /** Lleva el cursor hasta el texto señalado por una anotación. */
  goToAnnotation(annotation: AnnotationDto) {
    this.activeAnnotationId.set(annotation.id);

    let found: { from: number; to: number } | null = null;

    this.editor.state.doc.descendants((node, pos) => {
      if (found || !node.isText) return;

      const hasMark = node.marks.some(
        m => m.type.name === 'comentario' && m.attrs['anotacionId'] === annotation.id);

      if (hasMark) found = { from: pos, to: pos + node.nodeSize };
    });

    // Puede no estar: si alguien borró el texto comentado, la marca se fue con él. La anotación
    // sigue en el panel con su cita, que es justamente para esto.
    if (!found) {
      this.toast.info($localize`El texto comentado ya no está en la página`);
      return;
    }

    const range = found as { from: number; to: number };
    this.editor.chain().focus().setTextSelection(range).scrollIntoView().run();
  }

  /** Marca una anotación como resuelta, o la vuelve a abrir. */
  resolveAnnotation(annotation: AnnotationDto) {
    const page = this.activePage();
    if (!page) return;

    this.docsService.resolveAnnotation(annotation.id, !annotation.resolvedAtUtc).subscribe({
      next: () => this.loadAnnotations(page.id),
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
  deleteAnnotation(annotation: AnnotationDto) {
    const page = this.activePage();
    if (!page) return;

    this.docsService.deleteAnnotation(annotation.id).subscribe({
      next: () => {
        this.editor.chain().focus().unsetComment(annotation.id).run();
        this.loadAnnotations(page.id);
      },
      error: (err) => {
        this.toast.error($localize`No se pudo quitar el comentario`);
        console.error('No se pudo borrar la anotación', err);
      }
    });
  }

  private loadAnnotations(pageId: string) {
    this.docsService.getPageAnnotations(pageId).subscribe({
      next: (annotations) => this.annotations.set(Array.isArray(annotations) ? annotations : []),
      // Sin anotaciones el editor sigue siendo utilizable; no merece parar la pantalla.
      error: (err) => console.warn('No se pudieron leer los comentarios del documento', err)
    });
  }

  /** Contesta al comando que esperaba y cierra el modal. */
  answerUrl(url: string | null) {
    this.requestedUrl.set(null);
    const resolve = this.resolveUrl;
    this.resolveUrl = null;
    resolve?.(url);
  }

  /** Mientras se vuelca una página en el editor, los cambios que emite no son de nadie. */
  private loadingPage = false;

  /**
   * Cambia cuando el contenido se guarda, para que el índice se vuelva a leer.
   *
   * Se ata al guardado y no a cada pulsación: recalcular el esquema en cada tecla redibuja la
   * barra lateral mientras se escribe un título, que parpadea justo cuando hace falta concentrarse.
   */
  readonly outlineVersion = signal(0);

  // UI state
  searchQuery = signal('');
  /**
   * La pestaña del panel de Documentos, <b>leída de la URL</b>.
   *
   * Era estado interno del componente, y por eso Docs necesitaba su propia barra lateral: nadie
   * de fuera podía cambiarla. Al pasarla a `?tab=` la maneja el panel de navegación compartido
   * como el resto de los módulos, y de paso una pestaña se puede compartir por enlace y el botón
   * de atrás funciona.
   */
  private readonly seccionesDelPanel = inject(SeccionesDelPanelService);

  /** `effect` fuera del constructor necesita inyector explícito. */
  private readonly injector = inject(Injector);

  readonly activeSidebarTab = computed<'all' | 'my' | 'shared' | 'private' | 'meeting-notes' | 'templates' | 'archived'>(() => {
    const tab = this.queryParams()['tab'];
    return DOCS_TABS.includes(tab as never) ? tab as never : 'all';
  });

  private readonly queryParams = toSignal(
    inject(ActivatedRoute).queryParams,
    { initialValue: {} as Record<string, string> });
  
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
  private readonly templateUsages = signal<ReadonlyMap<string, number>>(new Map());

  /** Las del sistema y las del equipo, mezcladas y ordenadas por uso. */
  readonly templates = computed<AvailableTemplate[]>(
    () => availableTemplates(this.customTemplates(), this.templateUsages()));

  /** Las cuatro de la galería. El resto vive detrás de «Ver más». */
  readonly featuredTemplates = computed(() => this.templates().slice(0, VISIBLE_TEMPLATES));

  readonly hasMoreTemplates = computed(() => this.templates().length > VISIBLE_TEMPLATES);

  private loadTemplateUsages(): void {
    this.docsService.getTemplateUsages().subscribe({
      // Se comprueba la forma antes de recorrerla. Una respuesta que no sea la lista esperada
      // —un proxy que devuelve un objeto de error con 200, por ejemplo— reventaría aquí dentro y
      // se llevaría por delante la pantalla entera de Documentos por un contador de adorno.
      next: (usages) => this.templateUsages.set(Array.isArray(usages)
        ? new Map(usages.map(u => [u.key, u.count]))
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
        promptUrl: (kind) => this.promptUrl(kind),
        uploadFile: () => this.pickAndUpload()
      }),

      /**
       * Arrastrar un fichero al editor, o pegarlo desde el portapapeles.
       *
       * Es la forma en que se mete una captura en un documento, y no existía: la única vía era
       * pedir una dirección de una imagen que ya estuviera publicada en algún sitio.
       */
      FileHandler.configure({
        onDrop: (currentEditor, files, position) => {
          for (const file of files) void this.uploadAndInsert(file, position);
        },
        onPaste: (currentEditor, files) => {
          for (const file of files) void this.uploadAndInsert(file);
        }
      }),

      // Menciones `@persona` y `#tarea`. El buscador se inyecta aquí y no dentro de la extensión
      // porque la extensión no puede —ni debe— saber llamar a la API: sabe escribir el nodo con
      // el formato que el servidor lee, y nada más.
      Mention.configure({ search: (trigger, query) => this.mentions.search(trigger, query) }),
      // ── Los bloques que faltaban ──────────────────────────────────────────────────────────
      //
      // Sin ellos el editor sólo daba formato al texto; con ellos se puede estructurar un
      // documento largo, que es lo que se pedía de Notion y de ClickUp.

      /** Desplegables: la forma de tener un documento largo que no abruma. */
      Details.configure({ persist: true, HTMLAttributes: { class: 'desplegable' } }),
      DetailsSummary,
      DetailsContent,

      /** El recuadro de «ojo con esto». Escrito aquí: no hay extensión oficial. */
      Callout,

      /** Dos o tres columnas lado a lado. Tampoco hay extensión oficial. */
      Columns,
      Column,

      /**
       * La marca de los comentarios en línea.
       *
       * Sólo dice «aquí hay una conversación y se llama así». El hilo lo guarda el módulo
       * Comments y el anclaje —qué se citó, si está resuelto— lo guarda Docs.
       */
      CommentMark,

      /**
       * Código coloreado, con el lenguaje elegible.
       *
       * `common` trae los lenguajes habituales en vez de los ~190 de `all`, que pesan más que el
       * resto del editor junto.
       */
      CodeBlock.configure({ lowlight: createLowlight(common) }),

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
          const handle = document.createElement('div');
          handle.className = 'asa-de-bloque';
          handle.setAttribute('aria-hidden', 'true');
          handle.textContent = '⠿';
          return handle;
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
      if (this.loadingPage) return;

      // El contador se lee del editor en cada cambio: la extensión lo calcula igual, y sin esto
      // `CharacterCount` sería otra extensión cargada que no hace nada, que es justo lo que hemos
      // estado quitando.
      this.words.set(editor.storage['characterCount'].words());

      const page = this.activePage();
      const doc = this.activeDocument();
      if (page && doc) {
        this.saving.queuePage({
          pageId: page.id,
          title: page.title,
          content: editor.getHTML()
        });

        this.outlineVersion.update(v => v + 1);
      }
    }
  });

  ngOnInit() {
    this.loadDocuments();
    this.loadTemplateUsages();

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
    }, { injector: this.injector });

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
  useTemplate(template: AvailableTemplate, event?: Event) {
    if (event) event.stopPropagation();

    this.createFromTemplate(template.isCustom
      ? { templateDocumentId: template.key }
      : { templateKey: template.key });
  }

  /**
   * Crea un documento desde una plantilla, sea predefinida o propia.
   *
   * Los dos casos eran copias del mismo flujo y sólo se diferencian en el parámetro que
   * se envía. Tenerlo una vez evita que uno se arregle y el otro no.
   */
  private createFromTemplate(req: { templateKey?: string; templateDocumentId?: string }) {
    this.isNewDocDropdownOpen.set(false);
    this.isTemplatePickerOpen.set(false);
    this.isLoading.set(true);

    this.docsService.createFromTemplate(req).subscribe({
      next: (id) => {
        // El contador que acaba de subir es el que ordena la galería; sin releerlo, la plantilla
        // recién usada no se movería de sitio hasta la siguiente visita.
        this.loadTemplateUsages();
        this.openCreatedDocument(id);
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
  private openCreatedDocument(id: string) {
    this.docsService.getDocuments().subscribe({
      next: (docs) => {
        this.documents.set(docs);
        this.isLoading.set(false);
        const created = docs.find(d => d.id === id);
        if (created) this.selectDocument(created);
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
  onTemplateSaved() {
    this.isSaveAsTemplateModalOpen.set(false);
    this.loadDocuments();
  }

  /** El modal ya importó; se abre el documento recién creado. */
  onImported(id: string) {
    this.isImportModalOpen.set(false);
    this.openCreatedDocument(id);
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
  readonly documentPages = computed<PageDto[]>(() => {
    const doc = this.activeDocument();
    return doc ? this.pagesByDoc()[doc.id] ?? [] : [];
  });

  createNewPage(documentId: string, title = $localize`Sin título`, parentPageId?: string) {
    this.docsService.createPage(documentId, { title, parentPageId }).subscribe({
      next: (id) => {
        const cleanId = typeof id === 'string' ? id.replace(/['"]/g, '') : (id as any)?.value || id;
        this.reloadPages(documentId, cleanId);
      },
      error: (err) => {
        this.toast.error($localize`No se pudo crear la página`);
        console.error('No se pudo crear la página', err);
      }
    });
  }

  /** Desde el árbol: `null` crea una página de primer nivel, un id crea una subpágina. */
  createPageFromTree(parentPageId: string | null) {
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
  movePage(move: PageMove) {
    const doc = this.activeDocument();
    if (!doc) return;

    this.docsService.movePage(move.page.id, {
      parentPageId: move.parentId,
      order: move.order
    }).subscribe({
      next: () => this.reloadPages(doc.id),
      error: (err) => {
        this.toast.error($localize`No se pudo mover la página`, this.errorReason(err));
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
  trashPage(page: PageDto) {
    const doc = this.activeDocument();
    if (!doc || this.documentPages().length <= 1) return;

    this.docsService.deletePage(doc.id, page.id).subscribe({
      next: () => {
        if (this.activePage()?.id === page.id) this.activePage.set(null);
        this.reloadPages(doc.id);
      },
      error: (err) => {
        this.toast.error($localize`No se pudo borrar la página`);
        console.error('No se pudo borrar la página', err);
      }
    });
  }

  /** Vuelve a leer el árbol y deja abierta la página que se diga, o la que ya lo estaba. */
  private reloadPages(documentId: string, openId?: string) {
    this.docsService.getPages(documentId).subscribe({
      next: (pages) => {
        this.pagesByDoc.update(dict => ({ ...dict, [documentId]: pages }));

        const requested = openId ? pages.find(p => p.id === openId) : null;
        if (requested) { this.selectPage(requested); return; }

        const open = this.activePage();
        if (!open || !pages.some(p => p.id === open.id)) {
          if (pages.length > 0) this.selectPage(pages[0]);
        }
      },
      error: (err) => console.error('No se pudieron leer las páginas', err)
    });
  }

  /** El texto que manda el servidor cuando rechaza un movimiento, si viene en algo legible. */
  private errorReason(err: unknown): string | undefined {
    const body = (err as { error?: unknown })?.error;
    return typeof body === 'string' && body.length < 200 ? body : undefined;
  }

  selectPage(page: PageDto) {
    // Abrir una página no es editarla. Sin esta bandera, cargar el contenido en el editor
    // dispara `onUpdate`, y la cabecera pasaba a «Guardado a las HH:MM» **por haber abierto el
    // documento**, además de reescribir en el servidor lo mismo que acababa de leer.
    this.loadingPage = true;
    this.activePage.set(page);
    // Vacío, no un texto de ejemplo. Antes se metía «Start typing or use / for commands…» **como
    // contenido**, así que se guardaba en la página y había que borrarlo a mano. El aviso lo pone
    // la extensión `Placeholder`, que no escribe nada en el documento.
    this.editor.commands.setContent(page.content || '');
    setTimeout(() => {
      this.editor.commands.focus();
      this.loadingPage = false;

      // Al abrir tampoco lo cuenta nadie, porque `onUpdate` no llega a ejecutarse.
      this.words.set(this.editor.storage['characterCount'].words());
    }, 50);

    this.activeAnnotationId.set(null);
    this.loadAnnotations(page.id);
  }

  closeEditorView() {
    this.activeDocument.set(null);
    this.activePage.set(null);
  }

  insertEmoji(emoji: string) {
    this.editor.chain().focus().insertContent(emoji).run();
  }

  /** Guarda en PDF lo que se ve en el editor. Ver `DocumentExportService.exportPdf`. */
  exportPdf() {
    const content = document.querySelector('.ProseMirror');
    if (content) void this.exports.exportPdf(content as HTMLElement, this.activePage()?.title);
  }

  /** Descarga el documento abierto en HTML. Ver `DocumentExportService.exportHtml`. */
  exportHtml() {
    const doc = this.activeDocument();
    if (doc) this.exports.exportHtml(doc);
  }

  /**
   * Renombra la <b>página</b> activa. Va por el guardado automático, como el contenido.
   *
   * Antes este método lo llamaban los dos campos de título —el del documento y el de la miga de
   * pan—, así que escribir el título del documento renombraba la página por debajo mientras la
   * pantalla seguía enseñando el título viejo del documento.
   */
  renamePage(newTitle: string) {
    const current = this.activePage();
    if (!current) return;

    this.activePage.set({ ...current, title: newTitle });
    this.pagesByDoc.update(dict => {
      const docId = current.documentId;
      const pages = dict[docId];
      if (!pages) return dict;
      return { ...dict, [docId]: pages.map(p => p.id === current.id ? { ...p, title: newTitle } : p) };
    });

    this.saving.queuePage({
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
  renameDocument(newTitle: string) {
    const doc = this.activeDocument();
    if (!doc) return;

    this.activeDocument.set({ ...doc, title: newTitle });
    this.documents.update(docs => docs.map(d => d.id === doc.id ? { ...d, title: newTitle } : d));

    this.saving.queueDocumentTitle(doc.id, newTitle);
  }

  /** Reintenta lo último que no se pudo guardar. */
  retrySave() {
    this.saving.retry();
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