import { Component, inject, OnInit, signal } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/api.service';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideShieldCheck, lucideLock, lucideCheck, lucideX,
  lucideFolderKanban, lucideCheckSquare, lucideFileText,
  lucideWebhook, lucideUsers, lucideBarChart3, lucideSettings, lucideLoader2
} from '@ng-icons/lucide';
import { GranularPermissionsModalComponent } from './granular-permissions-modal.component';
import { ClickableDirective } from '../../../shared/directives/clickable.directive';

export interface RoleMatrixRow {
  key: string;
  name: string;
  description: string;
  icon: string;
  adminLevel: 'Full';
  memberLevel: 'Full' | 'Edit' | 'View' | 'None';
  guestLevel: 'Full' | 'Edit' | 'View' | 'None';
}

@Component({
  selector: 'app-admin-permissions',
  standalone: true,
  imports: [ClickableDirective, FormsModule, NgIconComponent, GranularPermissionsModalComponent],
  viewProviders: [
    provideIcons({
      lucideShieldCheck, lucideLock, lucideCheck, lucideX,
      lucideFolderKanban, lucideCheckSquare, lucideFileText,
      lucideWebhook, lucideUsers, lucideBarChart3, lucideSettings, lucideLoader2
    })
  ],
  templateUrl: './admin-permissions.component.html',
})
export class AdminPermissionsComponent implements OnInit {
  private readonly api = inject(ApiService);

  loading = signal(false);
  saving = signal(false);
  successMsg = signal('');
  errorMsg = signal('');

  showRoleModal = signal(false);
  selectedRoleForModal = signal<'Member' | 'Guest' | null>(null);

  matrix = signal<RoleMatrixRow[]>([
    {
      key: 'Projects',
      name: 'Proyectos & Espacios',
      description: 'Crear, editar, eliminar y configurar proyectos del workspace',
      icon: 'lucideFolderKanban',
      adminLevel: 'Full',
      memberLevel: 'Full',
      guestLevel: 'View'
    },
    {
      key: 'Tasks',
      name: 'Tareas & Elementos',
      description: 'Creación, edición de estado, asignaciones y comentarios',
      icon: 'lucideCheckSquare',
      adminLevel: 'Full',
      memberLevel: 'Full',
      guestLevel: 'Edit'
    },
    {
      key: 'Docs',
      name: 'Documentación & Wikis',
      description: 'Creación, edición y compartido de documentos TippTap',
      icon: 'lucideFileText',
      adminLevel: 'Full',
      memberLevel: 'Full',
      guestLevel: 'View'
    },
    {
      key: 'Webhooks',
      name: 'Webhooks & Integraciones API',
      description: 'Configuración de webhooks, llaves HMAC y eventos de integración',
      icon: 'lucideWebhook',
      adminLevel: 'Full',
      memberLevel: 'View',
      guestLevel: 'None'
    },
    {
      key: 'Teams',
      name: 'Gestión de Equipos & Miembros',
      description: 'Creación de equipos y asignación de usuarios',
      icon: 'lucideUsers',
      adminLevel: 'Full',
      memberLevel: 'View',
      guestLevel: 'None'
    },
    {
      key: 'Reports',
      name: 'Reportes & Analíticas',
      description: 'Acceso a tableros de métricas y exportaciones',
      icon: 'lucideBarChart3',
      adminLevel: 'Full',
      memberLevel: 'View',
      guestLevel: 'None'
    },
    {
      key: 'Settings',
      name: 'Configuración Global del Workspace',
      description: 'Ajustes del tenant, seguridad y facturación',
      icon: 'lucideSettings',
      adminLevel: 'Full',
      memberLevel: 'None',
      guestLevel: 'None'
    }
  ]);

  ngOnInit(): void {
    this.loadRolePermissions();
  }

  loadRolePermissions(): void {
    this.loading.set(true);
    this.api.get<any[]>('/permissions?targetType=Role').subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.matrix.update(rows => rows.map(r => {
            const memberPerm = data.find(d => d.roleName === 'Member' && d.entityType === r.key);
            const guestPerm = data.find(d => d.roleName === 'Guest' && d.entityType === r.key);
            return {
              ...r,
              memberLevel: memberPerm ? memberPerm.permissionLevel : r.memberLevel,
              guestLevel: guestPerm ? guestPerm.permissionLevel : r.guestLevel
            };
          }));
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  updateLevel(resourceKey: string, role: 'Member' | 'Guest', level: 'Full' | 'Edit' | 'View' | 'None'): void {
    this.matrix.update(rows => rows.map(r => {
      if (r.key === resourceKey) {
        return {
          ...r,
          [role === 'Member' ? 'memberLevel' : 'guestLevel']: level
        };
      }
      return r;
    }));
  }

  saveMatrix(): void {
    this.saving.set(true);
    this.successMsg.set('');
    this.errorMsg.set('');

    const memberPayload = {
      targetType: 'Role',
      roleName: 'Member',
      permissions: this.matrix().map(r => ({
        entityType: r.key,
        entityId: '00000000-0000-0000-0000-000000000000',
        permissionLevel: r.memberLevel
      }))
    };

    const guestPayload = {
      targetType: 'Role',
      roleName: 'Guest',
      permissions: this.matrix().map(r => ({
        entityType: r.key,
        entityId: '00000000-0000-0000-0000-000000000000',
        permissionLevel: r.guestLevel
      }))
    };

    this.api.post('/permissions', memberPayload).subscribe({
      next: () => {
        this.api.post('/permissions', guestPayload).subscribe({
          next: () => {
            this.saving.set(false);
            this.successMsg.set('Matriz de permisos de roles actualizada correctamente');
          },
          error: (err) => {
            this.saving.set(false);
            this.errorMsg.set(err?.error || 'Error al guardar permisos de invitados');
          }
        });
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMsg.set(err?.error || 'Error al guardar permisos de miembros');
      }
    });
  }

  openRoleModal(role: 'Member' | 'Guest'): void {
    this.selectedRoleForModal.set(role);
    this.showRoleModal.set(true);
  }

  closeRoleModal(): void {
    this.showRoleModal.set(false);
    this.selectedRoleForModal.set(null);
  }
}
