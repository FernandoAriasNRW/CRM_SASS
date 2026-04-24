import { createAction, createReducer, createSelector, on, props } from '@ngrx/store';

export interface Notification {
  id: string;
  userId: string;
  title: string;
  body: string;
  createdAtUtc: string;
  isRead: boolean;
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
  on(notificationReceived,   (s, { item })  => ({ ...s, items: [item, ...s.items] })),
  on(notificationMarkedRead, (s, { id })    => ({
    ...s,
    items: s.items.map(n => n.id === id ? { ...n, isRead: true } : n),
  })),
);

const selectFeature = (state: any) => state.notifications as NotificationsState;
export const selectNotifications = createSelector(selectFeature, s => s.items);
export const selectUnreadCount   = createSelector(selectFeature, s => s.items.filter(n => !n.isRead).length);
