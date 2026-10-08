import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucidePlus, lucideWebhook, lucideTrash2, lucideEdit3, lucideRefreshCw,
  lucideCheck, lucideX, lucideExternalLink, lucideActivity, lucideSearch,
  lucideLoader2, lucideServer, lucideEye, lucideEyeOff, lucideSend, lucideKey, lucideCopy, lucideClock
} from '@ng-icons/lucide';
import { WebhookService } from './webhook.service';
import type { WebhookDelivery, WebhookSubscription } from './webhook.model';
import { WebhookFormModalComponent } from './webhook-form-modal.component';
import { ClickableDirective } from '../../shared/directives/clickable.directive';

/**
 * Los webhooks de la organización: a dónde se mandan los eventos, cuáles, y si están llegando.
 *
 * Cada suscripción enseña cuántos envíos salieron bien, cuántos fallaron y cuántos esperan un
 * reintento, y su registro de envíos dice por qué falló cada uno. Antes los fallos no dejaban
 * rastro: un destino caído durante un mes no se notaba desde aquí.
 */
@Component({
  selector: 'app-webhooks',
  standalone: true,
  imports: [ClickableDirective, DatePipe, NgIconComponent, WebhookFormModalComponent, FormsModule],
  viewProviders: [
    provideIcons({
      lucidePlus, lucideWebhook, lucideTrash2, lucideEdit3, lucideRefreshCw,
      lucideCheck, lucideX, lucideExternalLink, lucideActivity, lucideSearch,
      lucideLoader2, lucideServer, lucideEye, lucideEyeOff, lucideSend, lucideKey, lucideCopy, lucideClock
    })
  ],
  templateUrl: './webhooks.component.html',
})
export class WebhooksComponent implements OnInit {
  private readonly webhookService = inject(WebhookService);

  subscriptions = signal<WebhookSubscription[]>([]);
  loading = signal(false);
  searchQuery = signal('');
  showForm = signal(false);
  editingSubscription = signal<WebhookSubscription | null>(null);
  confirmDeleteId = signal<string | null>(null);

  /** El secreto que se enseña: al crear, al regenerarlo o al pedirlo. */
  secretToShow = signal<{ name: string; secret: string; isNew: boolean } | null>(null);
  busyId = signal<string | null>(null);

  readonly activateLabel = $localize`Activar`;
  readonly deactivateLabel = $localize`Desactivar`;

  /** La suscripción cuyo registro de envíos está abierto, y sus envíos. */
  deliveriesFor = signal<string | null>(null);
  deliveries = signal<WebhookDelivery[]>([]);
  loadingDeliveries = signal(false);

  filteredSubscriptions = computed(() => {
    const query = this.searchQuery().toLowerCase();
    if (!query) return this.subscriptions();
    return this.subscriptions().filter(s =>
      s.name.toLowerCase().includes(query)
      || s.url.toLowerCase().includes(query)
      || s.eventTypes.some(e => e.toLowerCase().includes(query)));
  });

  stats = computed(() => {
    const subs = this.subscriptions();
    return {
      total: subs.length,
      active: subs.filter(s => s.isActive).length,
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
      next: data => {
        this.subscriptions.set(Array.isArray(data) ? data : []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
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

  /** Al crear, el secreto se enseña en el momento: es cuando hay que guardarlo al otro lado. */
  onFormSaved(result: { subscription: WebhookSubscription; secret?: string }): void {
    this.closeFormModal();
    if (result.secret) {
      this.secretToShow.set({ name: result.subscription.name, secret: result.secret, isNew: true });
    }
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
      next: () => this.subscriptions.update(subs => subs.filter(s => s.id !== id)),
    });
  }

  toggleActive(subscription: WebhookSubscription): void {
    this.busyId.set(subscription.id);
    this.webhookService.updateSubscription(subscription.id, {
      name: subscription.name,
      url: subscription.url,
      eventTypes: subscription.eventTypes,
      isActive: !subscription.isActive,
    }).subscribe({
      next: updated => {
        this.subscriptions.update(subs => subs.map(s => s.id === updated.id ? updated : s));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  showSecret(subscription: WebhookSubscription): void {
    this.busyId.set(subscription.id);
    this.webhookService.getSecret(subscription.id).subscribe({
      next: result => {
        this.secretToShow.set({ name: subscription.name, secret: result.secret, isNew: false });
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  regenerateSecret(subscription: WebhookSubscription): void {
    this.busyId.set(subscription.id);
    this.webhookService.regenerateSecret(subscription.id).subscribe({
      next: result => {
        this.secretToShow.set({ name: subscription.name, secret: result.secret, isNew: true });
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  sendTest(subscription: WebhookSubscription): void {
    this.busyId.set(subscription.id);
    this.webhookService.sendTest(subscription.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.openDeliveries(subscription, true);
      },
      error: () => this.busyId.set(null),
    });
  }

  /** Abre o cierra el registro de envíos de una suscripción. */
  openDeliveries(subscription: WebhookSubscription, keepOpen = false): void {
    if (this.deliveriesFor() === subscription.id && !keepOpen) {
      this.deliveriesFor.set(null);
      return;
    }

    this.deliveriesFor.set(subscription.id);
    this.loadingDeliveries.set(true);
    this.webhookService.getDeliveries(subscription.id).subscribe({
      next: list => {
        this.deliveries.set(Array.isArray(list) ? list : []);
        this.loadingDeliveries.set(false);
      },
      error: () => {
        this.deliveries.set([]);
        this.loadingDeliveries.set(false);
      },
    });
  }

  deliveryStatusLabel(delivery: WebhookDelivery): string {
    switch (delivery.status) {
      case 'Succeeded': return $localize`Entregado`;
      case 'Failed': return $localize`Fallido`;
      default: return delivery.attempts > 0 ? $localize`Reintentando` : $localize`Pendiente`;
    }
  }

  closeSecretModal(): void {
    this.secretToShow.set(null);
  }

  copyToClipboard(text: string): void {
    void navigator.clipboard?.writeText(text);
  }
}
