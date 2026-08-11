import { Component, inject, OnInit, signal, input, output } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/api.service';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideX, lucideShieldCheck, lucideCheck, lucideLock,
  lucideFolderKanban, lucideCheckSquare, lucideFileText,
  lucideWebhook, lucideUsers, lucideBarChart3, lucideSettings, lucideLoader2
} from '@ng-icons/lucide';

export interface ResourcePermissionItem {
  key: string;
  name: string;
  description: string;
  icon: string;
  level: 'Full' | 'Edit' | 'View' | 'None';
}

export interface GranularPermissionDto {
  id?: string;
  targetType: string;
  userId?: string | null;
  teamId?: string | null;
  roleName?: string | null;
  entityType: string;
  entityId: string;
  permissionLevel: string;
}

@Component({
  selector: 'app-granular-permissions-modal',
  standalone: true,
  imports: [FormsModule, NgIconComponent],
  viewProviders: [
    provideIcons({
      lucideX, lucideShieldCheck, lucideCheck, lucideLock,
      lucideFolderKanban, lucideCheckSquare, lucideFileText,
      lucideWebhook, lucideUsers, lucideBarChart3, lucideSettings, lucideLoader2
    })
  ],
  templateUrl: './granular-permissions-modal.component.html',
})
export class GranularPermissionsModalComponent implements OnInit {
  private readonly api = inject(ApiService);

  targetType = input.required<'User' | 'Team' | 'Role'>();
  targetId = input<string | null>(null);
  targetName = input.required<string>();
  roleName = input<string | null>(null);

  closeModal = output<void>();
  saved = output<void>();

  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  resources = signal<ResourcePermissionItem[]>([
    {
      key: 'Projects',
      name: 'Proyectos & Espacios',
      description: 'Crear, editar, borrar y administrar proyectos y espacios del workspace',
      icon: 'lucideFolderKanban',
      level: 'Edit'
    },
    {
      key: 'Tasks',
      name: 'Tareas & Elementos',
      description: 'Gestión de tareas, asignaciones, estados y tableros',
      icon: 'lucideCheckSquare',
      level: 'Edit'
    },
    {
      key: 'Docs',
      name: 'Documentación & Wikis',
      description: 'Crear, editar, compartir y eliminar documentos y plantillas',
      icon: 'lucideFileText',
      level: 'Edit'
    },
    {
      key: 'Webhooks',
      name: 'Webhooks & Integraciones',
      description: 'Configurar suscripciones de webhook, secretos HMAC y eventos globales',
      icon: 'lucideWebhook',
      level: 'View'
    },
    {
      key: 'Teams',
      name: 'Equipos & Miembros',
      description: 'Gestión de grupos de trabajo y asignación de usuarios',
      icon: 'lucideUsers',
      level: 'View'
    },
    {
      key: 'Reports',
      name: 'Reportes & Analíticas',
      description: 'Visualización de tableros de rendimiento, exportación y métricas',
      icon: 'lucideBarChart3',
      level: 'View'
    },
    {
      key: 'Settings',
      name: 'Configuración del Workspace',
      description: 'Administración global de tenant, facturación y preferencias',
      icon: 'lucideSettings',
      level: 'None'
    }
  ]);

  ngOnInit(): void {
    this.loadPermissions();
  }

  loadPermissions(): void {
    this.loading.set(true);
    let params = `targetType=${this.targetType()}`;
    if (this.targetId()) params += `&targetId=${this.targetId()}`;
    if (this.roleName()) params += `&roleName=${this.roleName()}`;

    this.api.get<GranularPermissionDto[]>(`/permissions?${params}`).subscribe({
      next: (existing) => {
        if (existing && existing.length > 0) {
          const map = new Map(existing.map(p => [p.entityType, p.permissionLevel as any]));
          this.resources.update(list => list.map(r => ({
            ...r,
            level: map.get(r.key) || r.level
          })));
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  setPermissionLevel(resourceKey: string, level: 'Full' | 'Edit' | 'View' | 'None'): void {
    this.resources.update(list =>
      list.map(r => r.key === resourceKey ? { ...r, level } : r)
    );
  }

  savePermissions(): void {
    this.saving.set(true);
    this.error.set('');
    this.successMsg.set('');

    const payload = {
      targetType: this.targetType(),
      userId: this.targetType() === 'User' ? this.targetId() : null,
      teamId: this.targetType() === 'Team' ? this.targetId() : null,
      roleName: this.targetType() === 'Role' ? this.roleName() || this.targetName() : null,
      permissions: this.resources().map(r => ({
        entityType: r.key,
        entityId: '00000000-0000-0000-0000-000000000000',
        permissionLevel: r.level
      }))
    };

    this.api.post('/permissions', payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.successMsg.set('Permisos granulares actualizados correctamente');
        setTimeout(() => {
          this.saved.emit();
          this.closeModal.emit();
        }, 800);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error || 'Error al guardar permisos');
      }
    });
  }

  onClose(): void {
    this.closeModal.emit();
  }
}
