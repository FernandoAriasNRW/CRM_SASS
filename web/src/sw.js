/// <reference lib="webworker" />

/**
 * Service Worker for CRM SaaS Push Notifications
 * Handles push notification delivery and interaction events.
 */

addEventListener('install', (event: ExtendableEvent) => {
  console.log('[ServiceWorker] Installed');
  (self as unknown as ServiceWorkerGlobalScope).skipWaiting();
});

addEventListener('activate', (event: ExtendableEvent) => {
  console.log('[ServiceWorker] Activated');
  (self as unknown as ServiceWorkerGlobalScope).clients.claim();
});

/**
 * Handle push notification events from the server.
 */
addEventListener('push', (event: PushEvent) => {
  console.log('[ServiceWorker] Push received');

  if (!event.data) {
    console.log('[ServiceWorker] No push data');
    return;
  }

  let data: {
    title: string;
    body: string;
    icon?: string;
    badge?: string;
    tag?: string;
    url?: string;
    requireInteraction?: boolean;
  };

  try {
    data = event.data.json();
  } catch {
    // Fallback for plain text notifications
    data = {
      title: 'CRM SaaS',
      body: event.data.text(),
    };
  }

  const options: NotificationOptions = {
    body: data.body || '',
    icon: data.icon || '/assets/icons/icon-192x192.png',
    badge: data.badge || '/assets/icons/badge-72x72.png',
    tag: data.tag || 'crm-notification',
    data: {
      url: data.url || '/',
      timestamp: Date.now(),
    },
    requireInteraction: data.requireInteraction ?? false,
    vibrate: [200, 100, 200],
    actions: [
      { action: 'view', title: 'Ver' },
      { action: 'dismiss', title: 'Ignorar' }
    ],
  };

  event.waitUntil(
    (self as unknown as ServiceWorkerGlobalScope).registration.showNotification(
      data.title || 'CRM SaaS',
      options
    )
  );
});

/**
 * Handle notification click events.
 */
addEventListener('notificationclick', (event: NotificationEvent) => {
  console.log('[ServiceWorker] Notification click');

  const notification = event.notification;
  const action = event.action;
  const data = notification.data as { url?: string };

  // Handle dismiss action
  if (action === 'dismiss') {
    notification.close();
    return;
  }

  // Close the notification
  notification.close();

  // Get the URL to open
  const url = data?.url || '/';

  // Open the app and focus the tab
  event.waitUntil(
    (self as unknown as ServiceWorkerGlobalScope).clients.matchAll({ type: 'window', includeUncontrolled: true })
      .then((clientList) => {
        // Try to focus an existing window
        for (const client of clientList) {
          if ('focus' in client) {
            client.focus();
            if (client.url.includes('/')) {
              client.navigate(url);
              return;
            }
          }
        }
        // Open a new window if no existing one
        return (self as unknown as ServiceWorkerGlobalScope).clients.openWindow(url);
      })
  );
});

/**
 * Handle notification close events (for analytics/tracking).
 */
addEventListener('notificationclose', (event: NotificationEvent) => {
  console.log('[ServiceWorker] Notification closed');
  // Could send analytics event here
});

/**
 * Handle message events from the main app.
 */
addEventListener('message', (event: MessageEvent) => {
  console.log('[ServiceWorker] Message received:', event.data);

  if (event.data?.type === 'SKIP_WAITING') {
    (self as unknown as ServiceWorkerGlobalScope).skipWaiting();
  }

  if (event.data?.type === 'UPDATE_BADGE') {
    updateBadgeCount(event.data.count);
  }
});

/**
 * Update the badge count in the browser.
 */
async function updateBadgeCount(count: number): Promise<void> {
  try {
    if ('setAppBadge' in (self as unknown as ServiceWorkerGlobalScope).navigator) {
      await (self as unknown as ServiceWorkerGlobalScope).navigator.setAppBadge(count);
    }
  } catch (error) {
    console.error('[ServiceWorker] Error updating badge:', error);
  }
}