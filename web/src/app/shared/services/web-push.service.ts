import { Injectable, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { AuthSignalStore } from '../../core/auth-signal.store';

export interface NotificationPreferences {
  emailEnabled: boolean;
  pushEnabled: boolean;
  taskAssigned: boolean;
  taskCompleted: boolean;
  taskDueSoon: boolean;
  ticketCreated: boolean;
  ticketUpdated: boolean;
  projectUpdated: boolean;
  mentionEnabled: boolean;
  quietHoursEnabled: boolean;
  quietHoursStart: string;
  quietHoursEnd: string;
}

export interface PushSubscriptionData {
  endpoint: string;
  keys: {
    p256dh: string;
    auth: string;
  };
}

/**
 * Web Push Notification Service
 * Handles browser push notification subscription and management.
 */
@Injectable({ providedIn: 'root' })
export class WebPushService {
  private readonly api = inject(ApiService);
  private readonly authStore = inject(AuthSignalStore);

  private readonly _isSupported = signal(false);
  private readonly _permission = signal<NotificationPermission>('default');
  private readonly _isSubscribed = signal(false);
  private subscription: globalThis.PushSubscription | null = null;

  readonly isSupported = this._isSupported.asReadonly();
  readonly permission = this._permission.asReadonly();
  readonly isSubscribed = this._isSubscribed.asReadonly();

  constructor() {
    this.checkSupport();
  }

  private checkSupport(): void {
    if (typeof window !== 'undefined' && 'Notification' in window && 'serviceWorker' in navigator) {
      this._isSupported.set(true);
      this._permission.set(Notification.permission);
      this.checkSubscriptionStatus();
    }
  }

  private async checkSubscriptionStatus(): Promise<void> {
    if (!this._isSupported()) return;

    try {
      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.getSubscription();
      this._isSubscribed.set(!!subscription);
      this.subscription = subscription;
    } catch (error) {
      console.error('Error checking push subscription:', error);
    }
  }

  /**
   * Requests permission for push notifications.
   */
  async requestPermission(): Promise<NotificationPermission> {
    if (!this._isSupported()) {
      console.warn('Push notifications not supported');
      return 'denied';
    }

    try {
      const permission = await Notification.requestPermission();
      this._permission.set(permission);
      return permission;
    } catch (error) {
      console.error('Error requesting notification permission:', error);
      return 'denied';
    }
  }

  /**
   * Subscribes to push notifications.
   */
  async subscribe(): Promise<PushSubscriptionData | null> {
    if (!this._isSupported() || this._permission() !== 'granted') {
      console.warn('Cannot subscribe: notifications not permitted');
      return null;
    }

    try {
      // Get VAPID public key from backend
      const { publicKey } = await this.api.get<{ publicKey: string }>('/notifications/push/vapid-key').toPromise() ?? { publicKey: '' };

      const registration = await navigator.serviceWorker.ready;

      const subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: this.urlBase64ToUint8Array(publicKey)
      });

      this.subscription = subscription;
      this._isSubscribed.set(true);

      // Convert to simple object for backend
      const subscriptionData: PushSubscriptionData = {
        endpoint: subscription.endpoint,
        keys: {
          p256dh: this.arrayBufferToBase64(subscription.getKey('p256dh')!),
          auth: this.arrayBufferToBase64(subscription.getKey('auth')!)
        }
      };

      // Send subscription to backend
      await this.api.post('/notifications/push/subscribe', subscriptionData).toPromise();

      return subscriptionData;
    } catch (error) {
      console.error('Error subscribing to push notifications:', error);
      return null;
    }
  }

  private arrayBufferToBase64(buffer: ArrayBuffer): string {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
  }

  /**
   * Unsubscribes from push notifications.
   */
  async unsubscribe(): Promise<boolean> {
    try {
      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.getSubscription();

      if (subscription) {
        await subscription.unsubscribe();

        // Notify backend
        await this.api.post('/notifications/push/unsubscribe', { endpoint: subscription.endpoint }).toPromise();
      }

      this.subscription = null;
      this._isSubscribed.set(false);
      return true;
    } catch (error) {
      console.error('Error unsubscribing from push notifications:', error);
      return false;
    }
  }

  /**
   * Updates the browser tab title with unread count badge.
   */
  updateBadgeCount(count: number): void {
    if (typeof document !== 'undefined') {
      const title = count > 0 ? `(${count}) CRM SaaS` : 'CRM SaaS';
      document.title = title;

      // Update favicon with badge if supported
      if ('setAppBadge' in navigator) {
        navigator.setAppBadge(count).catch(() => {});
      }
    }
  }

  /**
   * Clears the badge count.
   */
  clearBadge(): void {
    if (typeof document !== 'undefined') {
      document.title = 'CRM SaaS';

      if ('clearAppBadge' in navigator) {
        navigator.clearAppBadge().catch(() => {});
      }
    }
  }

  /**
   * Shows a test notification to verify setup.
   */
  async sendTestNotification(): Promise<void> {
    if (!this.subscription) {
      console.warn('Not subscribed to push notifications');
      return;
    }

    await this.api.post('/notifications/push/test', { endpoint: this.subscription.endpoint }).toPromise();
  }

  private urlBase64ToUint8Array(base64String: string): BufferSource {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);

    for (let i = 0; i < rawData.length; ++i) {
      outputArray[i] = rawData.charCodeAt(i);
    }

    return outputArray.buffer as ArrayBuffer;
  }

  /**
   * Tests if push notifications work by showing a local notification.
   */
  async testLocalNotification(title: string, body: string): Promise<void> {
    if (this._permission() === 'granted') {
      new Notification(title, {
        body,
        icon: '/assets/icons/icon-192x192.png',
        badge: '/assets/icons/badge-72x72.png',
        tag: 'test-notification',
        requireInteraction: false,
      });
    }
  }
}