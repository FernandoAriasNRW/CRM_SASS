import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { Store } from '@ngrx/store';
import { projectCreated, type Project } from '../../state/projects/projects.state';
import { HierarchyService, Space, Folder } from './hierarchy.service';
import { ButtonComponent } from '../../shared/ui/button.component';
import { InputComponent } from '../../shared/ui/input.component';
import { LabelComponent } from '../../shared/ui/label.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';

@Component({
  selector: 'app-project-create-modal',
  standalone: true,
  imports: [FormsModule, ButtonComponent, InputComponent, LabelComponent, DrawerComponent],
  templateUrl: './project-create-modal.component.html',
})
export class ProjectCreateModalComponent {
  readonly closed = output<void>();

  name = '';
  description = '';
  estimatedEndDate = '';
  spaceId = '';
  folderId = '';
  loading = signal(false);
  error = signal('');

  spaces = signal<Space[]>([]);
  folders = signal<Folder[]>([]);

  private readonly api = inject(ApiService);
  private readonly store = inject(Store);
  private readonly hierarchyService = inject(HierarchyService);

  ngOnInit() {
    this.hierarchyService.getSpaces().subscribe({
      next: (spaces) => this.spaces.set(spaces),
      error: () => console.error('Failed to load spaces')
    });
  }

  onSpaceChange() {
    this.folderId = '';
    this.folders.set([]);
    if (this.spaceId) {
      this.hierarchyService.getFolders(this.spaceId).subscribe({
        next: (folders) => this.folders.set(folders),
        error: () => console.error('Failed to load folders')
      });
    }
  }

  submit(): void {
    if (!this.name.trim() || !this.estimatedEndDate || !this.spaceId) {
      this.error.set('Nombre, espacio y fecha estimada son requeridos');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.api.post<Project>('/projects', {
      name: this.name,
      description: this.description,
      estimatedEndDate: this.estimatedEndDate,
      spaceId: this.spaceId,
      folderId: this.folderId || null,
    }).subscribe({
      next: item => {
        this.store.dispatch(projectCreated({ item }));
        this.closed.emit();
      },
      error: () => {
        this.error.set('Error al crear el proyecto');
        this.loading.set(false);
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
