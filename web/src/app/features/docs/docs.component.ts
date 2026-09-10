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
  lucideArrowUpDown, lucideTag, lucideX, lucideFileUp, lucideBriefcase, lucideCheck
} from '@ng-icons/lucide';
import { DocsService, DocumentDto, PageDto } from './docs.service';
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
import { EmojiPickerComponent } from './extensions/emoji-picker.component';
import { Subject, debounceTime } from 'rxjs';
import { ClickableDirective } from '../../shared/directives/clickable.directive';
import { GuardarPlantillaModalComponent } from './modals/guardar-plantilla-modal.component';
import { ImportarDocumentoModalComponent } from './modals/importar-documento-modal.component';
import { PlantillasDrawerComponent } from './plantillas-drawer.component';
import { PLANTILLAS_A_LA_VISTA, PlantillaDisponible, plantillasDisponibles } from './plantillas';

@Component({
  selector: 'app-docs',
  standalone: true,
  imports: [EsquemaDelDocumentoComponent, 
    GuardarPlantillaModalComponent, ImportarDocumentoModalComponent, PlantillasDrawerComponent, ClickableDirective, CommonModule, FormsModule, NgIconComponent, TiptapEditorDirective, EmojiPickerComponent],
  providers: [
    provideIcons({
      lucideFileText, lucidePlus, lucideFolder, lucideMoreVertical,
      lucideChevronRight, lucideChevronDown, lucideSearch,
      lucideSettings, lucideShare2, lucideClock, lucidePin, lucidePinOff,
      lucideBold, lucideItalic, lucideStrikethrough, lucideLink, lucideTrash,
      lucideUpload, lucideWand2, lucideLayoutTemplate, lucideCopy, lucideBookOpen,
      lucideUsers, lucideCalendar, lucideCheckCircle2, lucideStar, lucideFilter,
      lucideArrowUpDown, lucideTag, lucideX, lucideFileUp, lucideBriefcase, lucideCheck
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
      SlashCommand,

      // Menciones `@persona` y `#tarea`. El buscador se inyecta aquí y no dentro de la extensión
      // porque la extensión no puede —ni debe— saber llamar a la API: sabe escribir el nodo con
      // el formato que el servidor lee, y nada más.
      Mencion.configure({ buscador: (disparador, consulta) => this.menciones.buscar(disparador, consulta) }),
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
            return 'Heading...';
          }
          return 'Type / for commands, or start writing...';
        },
      })
    ],
    editorProps: {
      attributes: {
        class: 'prose prose-sm sm:prose-base dark:prose-invert prose-zinc max-w-none focus:outline-none min-h-[500px]',
      },
    },
    onUpdate: ({ editor }) => {
      const page = this.activePage();
      const doc = this.activeDocument();
      if (page && doc) {
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
    ).subscribe(update => {
      this.docsService.updatePage(update.pageId, {
        title: update.title,
        content: update.content
      }).subscribe();
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
        this.createNewPage(doc.id, 'Root Page');
      }
    });
  }

  createNewPage(documentId: string, title = 'Untitled Page', parentPageId?: string) {
    this.docsService.createPage(documentId, { title, parentPageId }).subscribe(id => {
      const cleanId = typeof id === 'string' ? id.replace(/['"]/g, '') : (id as any)?.value || id;
      this.docsService.getPages(documentId).subscribe(pages => {
        this.pagesByDoc.update(dict => ({ ...dict, [documentId]: pages }));
        const newPage = pages.find(p => p.id === cleanId);
        if (newPage) this.selectPage(newPage);
      });
    });
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
    this.activePage.set(page);
    this.editor.commands.setContent(page.content || '<p>Start typing or use / for commands...</p>');
    setTimeout(() => {
      this.editor.commands.focus();
    }, 50);
  }

  closeEditorView() {
    this.activeDocument.set(null);
    this.activePage.set(null);
  }

  insertEmoji(emoji: string) {
    this.editor.chain().focus().insertContent(emoji).run();
  }

  exportPdf() {
    const html2pdf = (window as any).html2pdf;
    if (html2pdf) {
      const element = document.querySelector('.ProseMirror');
      if (element) {
        html2pdf().from(element).save(`${this.activePage()?.title || 'export'}.pdf`);
      }
    } else {
      window.print();
    }
  }

  exportHtml() {
    const docId = this.activeDocument()?.id;
    if (docId) {
      window.open(`/api/v1/docs/${docId}/export`, '_blank');
    }
  }

  updateDocumentTitle(newTitle: string) {
    const current = this.activePage();
    if (current) {
      this.activePage.set({ ...current, title: newTitle });
      this.contentUpdate$.next({
        pageId: current.id,
        title: newTitle,
        content: this.editor.getHTML()
      });
    }
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