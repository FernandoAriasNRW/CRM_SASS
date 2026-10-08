import { createAction, createReducer, createSelector, on, props } from '@ngrx/store';

/** Un aviso tal como lo pinta la pantalla. */
export interface Notification {
  id: string;
  userId: string;
  title: string;
  body: string;
  /** De qué trata: uno del catálogo del servidor (`task.assigned`, `mention`…). Vacío en los antiguos. */
  kind: string | null;
  /** A qué cosa se refiere, para llevar a ella al pulsarlo. */
  entityType: string | null;
  entityId: string | null;
  createdAtUtc: string;
  isRead: boolean;
}

/** Lo que manda el servidor, en la lista y en tiempo real: el mismo `NotificationDto`. */
export interface NotificationDto {
  id: string;
  recipientUserId: string;
  subject: string;
  body: string;
  status: string;
  kind?: string | null;
  entityType?: string | null;
  entityId?: string | null;
  createdAt: string;
  readAt?: string | null;
}

/**
 * Del aviso del servidor al de la pantalla, en un solo sitio. La lista y el aviso en tiempo real
 * llegan con la misma forma; antes cada uno se leía a su manera y el de tiempo real esperaba
 * campos —`title`, `type`— que el servidor no mandaba.
 */
export function fromApi(n: NotificationDto): Notification {
  return {
    id: n.id,
    userId: n.recipientUserId,
    title: n.subject || $localize`Notificación`,
    body: n.body || '',
    kind: n.kind ?? null,
    entityType: n.entityType ?? null,
    entityId: n.entityId ?? null,
    createdAtUtc: n.createdAt || new Date().toISOString(),
    isRead: n.status === 'Read' || !!n.readAt,
  };
}

export interface NotificationsState {
  items: Notification[];
  loaded: boolean;
}

const initial: NotificationsState = { items: [], loaded: false };

export const notificationsLoaded   = createAction('[Notifications] Loaded', props<{ items: Notification[] }>());
export const notificationReceived  = createAction('[Notifications] Received', props<{ item: Notification }>());
export const notificationMarkedRead = createAction('[Notifications] Marked Read', props<{ id: string }>());

export const notificationsReducer = createReducer(
  initial,
  on(notificationsLoaded,    (s, { items }) => ({ items, loaded: true })),
  // Un aviso que ya está en la lista no se repite: llega por tiempo real y otra vez al recargar.
  on(notificationReceived,   (s, { item })  => ({ ...s, items: [item, ...s.items.filter(n => n.id !== item.id)] })),
  on(notificationMarkedRead, (s, { id })    => ({
    ...s,
    items: s.items.map(n => n.id === id ? { ...n, isRead: true } : n),
  })),
);

const selectFeature = (state: any) => state.notifications as NotificationsState;
export const selectNotifications = createSelector(selectFeature, s => s.items);
export const selectUnreadCount   = createSelector(selectFeature, s => s.items.filter(n => !n.isRead).length);
