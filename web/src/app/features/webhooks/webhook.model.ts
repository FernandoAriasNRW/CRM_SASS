/**
 * Una suscripción tal como la devuelve `GET /webhooks`. No lleva el secreto: se pide aparte
 * (`GET /webhooks/{id}/secret`), para que no viaje en cada listado.
 */
export interface WebhookSubscription {
  id: string;
  name: string;
  url: string;
  /** Los eventos elegidos, uno a uno. Nunca «todos» por defecto. */
  eventTypes: string[];
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  successCount: number;
  failureCount: number;
  pendingCount: number;
  lastDeliveryAtUtc: string | null;
}

/** Lo que devuelve crear una suscripción o cambiar su secreto. */
export interface WebhookWithSecret {
  subscription: WebhookSubscription;
  secret: string;
}

/** El cuerpo de crear o cambiar una suscripción. */
export interface WebhookRequest {
  name: string;
  url: string;
  eventTypes: string[];
  isActive: boolean;
}

/** Un evento del catálogo del servidor (`GET /webhooks/events`). */
export interface WebhookEventType {
  name: string;
  category: string;
}

/** Un envío y sus intentos. */
export interface WebhookDelivery {
  id: string;
  eventName: string;
  status: 'Pending' | 'Succeeded' | 'Failed';
  attempts: number;
  createdAtUtc: string;
  completedAtUtc: string | null;
  nextAttemptAtUtc: string | null;
  lastStatusCode: number | null;
  lastError: string | null;
}

/**
 * Cómo se llama en pantalla cada grupo del catálogo. La clave es la del servidor
 * (`WebhookEventCatalog.Categories`); un grupo nuevo sin nombre aquí se enseña con su clave.
 */
export const WEBHOOK_CATEGORY_LABELS: Record<string, string> = {
  tasks: $localize`Tareas`,
  tickets: $localize`Tickets`,
  projects: $localize`Proyectos`,
  users: $localize`Usuarios`,
  teams: $localize`Equipos`,
  reports: $localize`Informes`,
  documents: $localize`Documentos`,
  notifications: $localize`Notificaciones`,
  calendar: $localize`Calendario`,
  chat: $localize`Chat`,
};
