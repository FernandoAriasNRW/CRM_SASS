import { Injectable, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { Space, Folder } from '../features/projects/hierarchy.service';
import { Project } from '../state/projects/projects.state';

export interface SelectionItem {
  type: 'space' | 'folder' | 'project';
  id: string;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class HierarchySignalStore {
  private readonly api = inject(ApiService);

  readonly spaces = signal<Space[]>([]);
  
  // State for nested items
  readonly foldersBySpace = signal<Record<string, Folder[]>>({});
  readonly projectsByFolder = signal<Record<string, Project[]>>({});
  readonly projectsBySpace = signal<Record<string, Project[]>>({});

  // UI State
  readonly expandedSpaces = signal<Record<string, boolean>>({});
  readonly expandedFolders = signal<Record<string, boolean>>({});
  readonly selectedItem = signal<SelectionItem | null>(null);

  loadHierarchy() {
    this.api.get<Space[]>('/spaces').subscribe(s => this.spaces.set(s));
  }

  toggleSpace(spaceId: string) {
    const current = this.expandedSpaces();
    const isExpanded = !current[spaceId];
    this.expandedSpaces.set({ ...current, [spaceId]: isExpanded });

    if (isExpanded && !this.foldersBySpace()[spaceId]) {
      this.api.get<Folder[]>('/folders', { spaceId }).subscribe(f => {
        this.foldersBySpace.update(prev => ({ ...prev, [spaceId]: f }));
      });
      // Load projects directly under the space
      this.api.get<{items: Project[]}>('/projects', { spaceId, pageSize: 1000 }).subscribe(res => {
        this.projectsBySpace.update(prev => ({ ...prev, [spaceId]: res.items }));
      });
    }
  }

  toggleFolder(folderId: string) {
    const current = this.expandedFolders();
    const isExpanded = !current[folderId];
    this.expandedFolders.set({ ...current, [folderId]: isExpanded });

    if (isExpanded && !this.projectsByFolder()[folderId]) {
      this.api.get<{items: Project[]}>('/projects', { folderId, pageSize: 1000 }).subscribe(res => {
        this.projectsByFolder.update(prev => ({ ...prev, [folderId]: res.items }));
      });
    }
  }

  selectItem(item: SelectionItem) {
    this.selectedItem.set(item);
  }

  clearSelection() {
    this.selectedItem.set(null);
  }
}
