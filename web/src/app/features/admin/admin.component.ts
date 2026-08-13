import { Component, inject, OnInit, signal } from '@angular/core';

import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideUsers, lucideUserCheck, lucideShieldCheck, lucideWebhook,
  lucideSettings, lucideBuilding2, lucideListPlus
} from '@ng-icons/lucide';
import { AdminUsersComponent } from './users/admin-users.component';
import { AdminTeamsComponent } from './teams/admin-teams.component';
import { AdminPermissionsComponent } from './permissions/admin-permissions.component';
import { AdminCustomFieldsComponent } from './custom-fields/admin-custom-fields.component';
import { WebhooksComponent } from '../webhooks/webhooks.component';

type PestanaDeAdmin = 'users' | 'teams' | 'permissions' | 'custom-fields' | 'webhooks';

const PESTANAS: PestanaDeAdmin[] = ['users', 'teams', 'permissions', 'custom-fields', 'webhooks'];

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [
    RouterModule,
    NgIconComponent,
    AdminUsersComponent,
    AdminTeamsComponent,
    AdminPermissionsComponent,
    AdminCustomFieldsComponent,
    WebhooksComponent
],
  viewProviders: [
    provideIcons({
      lucideUsers, lucideUserCheck, lucideShieldCheck, lucideWebhook,
      lucideSettings, lucideBuilding2, lucideListPlus
    })
  ],
  templateUrl: './admin.component.html',
})
export class AdminComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  activeTab = signal<PestanaDeAdmin>('users');

  ngOnInit(): void {
    const tabParam = this.route.snapshot.queryParams['tab'];
    if (tabParam && PESTANAS.includes(tabParam)) {
      this.activeTab.set(tabParam as PestanaDeAdmin);
    }
  }

  setTab(tab: PestanaDeAdmin): void {
    this.activeTab.set(tab);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge'
    });
  }
}
