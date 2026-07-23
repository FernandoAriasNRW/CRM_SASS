import { Directive, Input, TemplateRef, ViewContainerRef, inject, effect } from '@angular/core';
import { AuthSignalStore } from '../../core/auth-signal.store';

@Directive({
  selector: '[appHasPermission]',
  standalone: true
})
export class HasPermissionDirective {
  private authStore = inject(AuthSignalStore);
  private templateRef = inject(TemplateRef);
  private vcr = inject(ViewContainerRef);

  private requiredPermission: string = '';
  private entityType?: string;
  private entityId?: string;
  
  private hasView = false;

  @Input() set appHasPermission(permission: string) {
    this.requiredPermission = permission;
    this.updateView();
  }

  @Input() set appHasPermissionEntityType(type: string) {
    this.entityType = type;
    this.updateView();
  }

  @Input() set appHasPermissionEntityId(id: string) {
    this.entityId = id;
    this.updateView();
  }

  constructor() {
    effect(() => {
      // Re-evaluate when user info changes
      this.authStore.userInfo();
      this.updateView();
    });
  }

  private updateView() {
    const isAdmin = this.authStore.isAdmin();
    
    // Simplification for the frontend demo: Admins always have access. 
    // In a real app, we would check a matrix of entity permissions loaded into the store.
    let hasAccess = isAdmin;

    if (!isAdmin && this.requiredPermission === 'Read') {
        // Example: everyone can read by default in this demo if they are logged in
        hasAccess = this.authStore.isAuthenticated();
    }

    if (hasAccess && !this.hasView) {
      this.vcr.createEmbeddedView(this.templateRef);
      this.hasView = true;
    } else if (!hasAccess && this.hasView) {
      this.vcr.clear();
      this.hasView = false;
    }
  }
}
