import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideUsers, lucideUserCheck, lucideShieldCheck, lucideWebhook,
  lucideSettings, lucideBuilding2
} from '@ng-icons/lucide';
import { AdminUsersComponent } from './users/admin-users.component';
import { AdminTeamsComponent } from './teams/admin-teams.component';
import { AdminPermissionsComponent } from './permissions/admin-permissions.component';
import { WebhooksComponent } from '../webhooks/webhooks.component';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NgIconComponent,
    AdminUsersComponent,
    AdminTeamsComponent,
    AdminPermissionsComponent,
    WebhooksComponent
  ],
  viewProviders: [
    provideIcons({
      lucideUsers, lucideUserCheck, lucideShieldCheck, lucideWebhook,
      lucideSettings, lucideBuilding2
    })
  ],
  templateUrl: './admin.component.html',
})
export class AdminComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  activeTab = signal<'users' | 'teams' | 'permissions' | 'webhooks'>('users');

  ngOnInit(): void {
    const tabParam = this.route.snapshot.queryParams['tab'];
    if (tabParam && ['users', 'teams', 'permissions', 'webhooks'].includes(tabParam)) {
      this.activeTab.set(tabParam as any);
    }
  }

  setTab(tab: 'users' | 'teams' | 'permissions' | 'webhooks'): void {
    this.activeTab.set(tab);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge'
    });
  }
}
