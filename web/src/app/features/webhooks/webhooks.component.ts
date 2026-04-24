import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucidePlus, lucideWebhook, lucideTrash2, lucideEdit3, lucideRefreshCw,
  lucideCheck, lucideX, lucideExternalLink, lucideActivity, lucideSearch,
  lucideLoader2, lucideServer, lucideEye, lucideEyeOff
} from '@ng-icons/lucide';
import { WebhookService } from './webhook.service';
import { WebhookSubscription, WEBHOOK_EVENT_TYPES } from './webhook.model';
import { WebhookFormModalComponent } from './webhook-form-modal.component';
import { AuthSignalStore } from '../../core/auth-signal.store';

@Component({
  selector: 'app-webhooks',
  standalone: true,
  imports: [
    NgIconComponent,
    WebhookFormModalComponent,
    FormsModule
  ],
  viewProviders: [
    provideIcons({
      lucidePlus, lucideWebhook, lucideTrash2, lucideEdit3, lucideRefreshCw,
      lucideCheck, lucideX, lucideExternalLink, lucideActivity, lucideSearch,
      lucideLoader2, lucideServer, lucideEye, lucideEyeOff
    })
  ],
  templateUrl: './webhooks.component.html',
})
export class WebhooksComponent implements OnInit {
  private readonly webhookService = inject(WebhookService);
  private readonly authStore = inject(AuthSignalStore);

  // State
  subscriptions = signal<WebhookSubscription[]>([]);
  loading = signal(false);
  searchQuery = signal('');
  showForm = signal(false);
  editingSubscription = signal<WebhookSubscription | null>(null);
  confirmDeleteId = signal<string | null>(null);
  showSecretModal = signal(false);
  secretToShow = signal<{ name: string; secret: string } | null>(null);
  regeneratingId = signal<string | null>(null);
  togglingId = signal<string | null>(null);

  // Computed
  filteredSubscriptions = computed(() => {
    const query = this.searchQuery().toLowerCase();
    if (!query) return this.subscriptions();
    return this.subscriptions().filter(
      (s) =>
        s.name.toLowerCase().includes(query) ||
        s.url.toLowerCase().includes(query) ||
        s.eventTypes.toLowerCase().includes(query)
    );
  });

  stats = computed(() => {
    const subs = this.subscriptions();
    return {
      total: subs.length,
      active: subs.filter((s) => s.isActive).length,
      successCount: subs.reduce((acc, s) => acc + s.successCount, 0),
      failureCount: subs.reduce((acc, s) => acc + s.failureCount, 0),
    };
  });

  ngOnInit(): void {
    this.loadSubscriptions();
  }

  loadSubscriptions(): void {
    this.loading.set(true);
    this.webhookService.getSubscriptions().subscribe({
      next: (data) => {
        this.subscriptions.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  openCreateModal(): void {
    this.editingSubscription.set(null);
    this.showForm.set(true);
  }

  openEditModal(subscription: WebhookSubscription): void {
    this.editingSubscription.set(subscription);
    this.showForm.set(true);
  }

  closeFormModal(): void {
    this.showForm.set(false);
    this.editingSubscription.set(null);
  }

  onFormSaved(subscription: WebhookSubscription): void {
    this.closeFormModal();
    this.loadSubscriptions();
  }

  confirmDelete(subscription: WebhookSubscription): void {
    this.confirmDeleteId.set(subscription.id);
  }

  cancelDelete(): void {
    this.confirmDeleteId.set(null);
  }

  deleteSubscription(id: string): void {
    this.confirmDeleteId.set(null);
    this.webhookService.deleteSubscription(id).subscribe({
      next: () => {
        this.subscriptions.update((subs) => subs.filter((s) => s.id !== id));
      },
    });
  }

  toggleActive(subscription: WebhookSubscription): void {
    this.togglingId.set(subscription.id);
    this.webhookService.toggleActive(subscription.id, !subscription.isActive).subscribe({
      next: (updated) => {
        this.subscriptions.update((subs) =>
          subs.map((s) => (s.id === subscription.id ? updated : s))
        );
        this.togglingId.set(null);
      },
      error: () => {
        this.togglingId.set(null);
      },
    });
  }

  regenerateSecret(subscription: WebhookSubscription): void {
    this.regeneratingId.set(subscription.id);
    this.webhookService.regenerateSecret(subscription.id).subscribe({
      next: (result) => {
        this.secretToShow.set({ name: subscription.name, secret: result.secret });
        this.showSecretModal.set(true);
        this.regeneratingId.set(null);
        this.loadSubscriptions();
      },
      error: () => {
        this.regeneratingId.set(null);
      },
    });
  }

  closeSecretModal(): void {
    this.showSecretModal.set(false);
    this.secretToShow.set(null);
  }

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text);
  }

  formatDate(dateStr: string | null): string {
    if (!dateStr) return 'Nunca';
    const date = new Date(dateStr);
    return date.toLocaleDateString('es-ES', {
      day: '2-digit',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  getEventTypeLabels(eventTypesStr: string): string[] {
    return this.webhookService.parseEventTypes(eventTypesStr);
  }

  getEventTypeDescription(type: string): string {
    const found = WEBHOOK_EVENT_TYPES.find((e) => e.type === type);
    return found?.description ?? type;
  }

  getTenantId(): string {
    const userInfo = this.authStore.userInfo();
    return userInfo?.tenantId ?? '';
  }
}