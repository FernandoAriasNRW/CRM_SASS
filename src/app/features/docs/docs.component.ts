import { Component, inject, signal, computed, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
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
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import Youtube from '@tiptap/extension-youtube';
import SlashCommand from './extensions/slash-command';
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

export interface PresetTemplate {
  key: string;
  title: string;
  description: string;
  bgGradient: string;
  borderColor: string;
  iconBg: string;
  icon: string;
  badgeText: string | null;
}

@Component({
  selector: 'app-docs',
  standalone: true,
  imports: [CommonModule, FormsModule, NgIconComponent, TiptapEditorDirective, EmojiPickerComponent],
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
    class: 'flex h-full w-full bg-white dark:bg-zinc-950 overflow-hidden relative',
    '(document:click)': 'onGlobalClick($event)'
  }
})
export class DocsComponent implements OnInit, OnDestroy, AfterViewInit {
  private docsService = inject(DocsService);

  documents = signal<DocumentDto[]>([]);
  pagesByDoc = signal<Record<string, PageDto[]>>({});
  
  activeDocument = signal<DocumentDto | null>(null);
  activePage = signal<PageDto | null>(null);
  isLoading = signal(false);
  expandedPages = signal<Set<string>>(new Set<string>());
  
  private contentUpdate$ = new Subject<{ pageId: string, title: string, content: string }>();

  // UI state
  searchQuery = signal('');
  isSearchActive = signal(false);
  activeSidebarTab = signal<'all' | 'my' | 'shared' | 'private' | 'meeting-notes' | 'archived'>('all');
  isPrivateCollapsed = signal(false);
  isPinned = signal(true);
  isHovered = signal(false);
  
  // Dropdowns & Modals
  isNewDocDropdownOpen = signal(false);
  isImportModalOpen = signal(false);
  isTemplatePickerOpen = signal(false);
  isSaveAsTemplateModalOpen = signal(false);
  activeRowMenuId = signal<string | null>(null);

  // Form states
  selectedDocForTemplate = signal<DocumentDto | null>(null);
  saveTemplateTitle = signal('');
  saveTemplateDescription = signal('');
  
  importTitle = signal('');
  importContent = signal('');
  importType = signal(1);

  // Sorting & Filtering
  selectedSort = signal<'updated' | 'title' | 'type'>('updated');
  sortDirection = signal<'asc' | 'desc'>('desc');
  selectedTypeFilter = signal<number | null>(null);
  starredDocIds = signal<Set<string>>(new Set<string>());

  presetTemplates: PresetTemplate[] = [
    {
      key: 'project-overview',
      title: 'Project Overview',
      description: 'Summarize goals, scope, and milestones',
      bgGradient: 'from-amber-500/10 via-orange-500/5 to-transparent dark:from-amber-500/20 dark:via-orange-500/10',
      borderColor: 'border-amber-200 dark:border-amber-800/50 hover:border-amber-400 dark:hover:border-amber-600',
      iconBg: 'bg-amber-500 text-white shadow-amber-500/30',
      icon: 'lucideFileText',
      badgeText: null
    },
    {
      key: 'meeting-notes',
      title: 'Meeting Notes',
      description: 'Capture an agenda, notes, and action items',
      bgGradient: 'from-yellow-500/10 via-amber-500/5 to-transparent dark:from-yellow-500/20 dark:via-amber-500/10',
      borderColor: 'border-yellow-200 dark:border-yellow-800/50 hover:border-yellow-400 dark:hover:border-yellow-600',
      iconBg: 'bg-yellow-500 text-white shadow-yellow-500/30',
      icon: 'lucideCalendar',
      badgeText: null
    },
    {
      key: 'wiki',
      title: 'Wiki',
      description: 'Organize information in one place',
      bgGradient: 'from-blue-500/10 via-indigo-500/5 to-transparent dark:from-blue-500/20 dark:via-indigo-500/10',
      borderColor: 'border-blue-200 dark:border-blue-800/50 hover:border-blue-400 dark:hover:border-blue-600',
      iconBg: 'bg-blue-600 text-white shadow-blue-600/30',
      icon: 'lucideBookOpen',
      badgeText: '✓'
    },
    {
      key: 'client-onboarding',
      title: 'Client Onboarding',
      description: 'Client summary, requirements, and handover',
      bgGradient: 'from-purple-500/10 via-pink-500/5 to-transparent dark:from-purple-500/20 dark:via-pink-500/10',
      borderColor: 'border-purple-200 dark:border-purple-800/50 hover:border-purple-400 dark:hover:border-purple-600',
      iconBg: 'bg-purple-600 text-white shadow-purple-600/30',
      icon: 'lucideBriefcase',
      badgeText: null
    }
  ];

  @ViewChild('bubbleMenu', { static: false }) bubbleMenuElement!: ElementRef;

  customTemplates = computed(() => {
    return this.documents().filter(d => d.type === 4);
  });

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
      }
    }
  });

  ngOnInit() {
    this.loadDocuments();

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

  createNewDoc(type: number = 1, event?: Event) {
    if (event) event.stopPropagation();
    this.isNewDocDropdownOpen.set(false);

    const title = type === 2 ? 'Untitled Wiki' : 'Untitled Document';
    this.docsService.createDocument({
      title,
      description: '',
      type
    }).subscribe({
      next: (id: any) => {
        let cleanId = typeof id === 'string' ? id.replace(/['"]/g, '') : (id?.value || id?.id || id);
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

  createFromPreset(presetKey: string, event?: Event) {
    if (event) event.stopPropagation();
    this.isNewDocDropdownOpen.set(false);
    this.isLoading.set(true);

    this.docsService.createFromTemplate({ templateKey: presetKey }).subscribe({
      next: (id: any) => {
        let cleanId = typeof id === 'string' ? id.replace(/['"]/g, '') : (id?.value || id?.id || id);
        this.docsService.getDocuments().subscribe(docs => {
          this.documents.set(docs);
          this.isLoading.set(false);
          const newDoc = docs.find(d => d.id === cleanId);
          if (newDoc) this.selectDocument(newDoc);
        });
      },
      error: (err) => {
        console.error('Error creating from template:', err);
        this.isLoading.set(false);
      }
    });
  }

  createFromCustomTemplate(templateDocId: string, event?: Event) {
    if (event) event.stopPropagation();
    this.isNewDocDropdownOpen.set(false);
    this.isTemplatePickerOpen.set(false);
    this.isLoading.set(true);

    this.docsService.createFromTemplate({ templateDocumentId: templateDocId }).subscribe({
      next: (id: any) => {
        let cleanId = typeof id === 'string' ? id.replace(/['"]/g, '') : (id?.value || id?.id || id);
        this.docsService.getDocuments().subscribe(docs => {
          this.documents.set(docs);
          this.isLoading.set(false);
          const newDoc = docs.find(d => d.id === cleanId);
          if (newDoc) this.selectDocument(newDoc);
        });
      },
      error: (err) => {
        console.error('Error creating from custom template:', err);
        this.isLoading.set(false);
      }
    });
  }

  openSaveAsTemplateModal(doc: DocumentDto, event?: Event) {
    if (event) event.stopPropagation();
    this.activeRowMenuId.set(null);
    this.selectedDocForTemplate.set(doc);
    this.saveTemplateTitle.set(`${doc.title} Template`);
    this.saveTemplateDescription.set(doc.description || '');
    this.isSaveAsTemplateModalOpen.set(true);
  }

  submitSaveAsTemplate() {
    const doc = this.selectedDocForTemplate();
    if (!doc) return;

    this.docsService.saveAsTemplate(doc.id, {
      customTitle: this.saveTemplateTitle(),
      description: this.saveTemplateDescription()
    }).subscribe({
      next: () => {
        this.isSaveAsTemplateModalOpen.set(false);
        this.loadDocuments();
      },
      error: (err) => {
        console.error('Error saving template:', err);
        alert('Error saving template');
      }
    });
  }

  openImportModal(event?: Event) {
    if (event) event.stopPropagation();
    this.isNewDocDropdownOpen.set(false);
    this.importTitle.set('');
    this.importContent.set('');
    this.importType.set(1);
    this.isImportModalOpen.set(true);
  }

  onImportFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      const fileNameWithoutExt = file.name.replace(/\.[^/.]+$/, "");
      this.importTitle.set(fileNameWithoutExt);

      const reader = new FileReader();
      reader.onload = (e) => {
        const text = e.target?.result as string;
        this.importContent.set(text || '');
      };
      reader.readAsText(file);
    }
  }

  submitImport() {
    if (!this.importTitle().trim() || !this.importContent().trim()) {
      alert('Please provide a title and content to import.');
      return;
    }

    this.docsService.importDocument({
      title: this.importTitle(),
      content: this.importContent(),
      type: this.importType()
    }).subscribe({
      next: (id: any) => {
        this.isImportModalOpen.set(false);
        let cleanId = typeof id === 'string' ? id.replace(/['"]/g, '') : (id?.value || id?.id || id);
        this.docsService.getDocuments().subscribe(docs => {
          this.documents.set(docs);
          const importedDoc = docs.find(d => d.id === cleanId);
          if (importedDoc) this.selectDocument(importedDoc);
        });
      },
      error: (err) => {
        console.error('Error importing document:', err);
        alert('Error importing document');
      }
    });
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

  createNewPage(documentId: string, title: string = 'Untitled Page', parentPageId?: string) {
    this.docsService.createPage(documentId, { title, parentPageId }).subscribe(id => {
      let cleanId = typeof id === 'string' ? id.replace(/['"]/g, '') : (id as any)?.value || id;
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

  setSidebarTab(tab: 'all' | 'my' | 'shared' | 'private' | 'meeting-notes' | 'archived') {
    this.activeSidebarTab.set(tab);
    this.activeDocument.set(null);
    this.activePage.set(null);
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
      case 2: return 'bg-pink-100 text-pink-700 dark:bg-pink-950/40 dark:text-pink-300 border-pink-200 dark:border-pink-800/50';
      case 3: return 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300 border-amber-200 dark:border-amber-800/50';
      case 4: return 'bg-purple-100 text-purple-700 dark:bg-purple-950/40 dark:text-purple-300 border-purple-200 dark:border-purple-800/50';
      default: return 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300 border-zinc-200 dark:border-zinc-700';
    }
  }
}