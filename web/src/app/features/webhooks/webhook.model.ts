/**
 * Webhook subscription model for frontend.
 */
export interface WebhookSubscription {
  id: string;
  tenantId: string;
  name: string;
  url: string;
  secret: string;
  isActive: boolean;
  eventTypes: string;
  createdAtUtc: string;
  lastTriggeredAtUtc: string | null;
  successCount: number;
  failureCount: number;
}

/**
 * Payload for creating a new webhook subscription.
 */
export interface CreateWebhookRequest {
  tenantId: string;
  name: string;
  url: string;
  eventTypes: string;
  maxRetries?: number;
  timeoutSeconds?: number;
  customHeaders?: string;
}

/**
 * Payload for updating a webhook subscription.
 */
export interface UpdateWebhookRequest {
  name?: string;
  url?: string;
  eventTypes?: string;
  isActive?: boolean;
  maxRetries?: number;
  timeoutSeconds?: number;
  customHeaders?: string;
}

/**
 * Available webhook event types.
 */
export interface WebhookEventType {
  type: string;
  description: string;
}

/**
 * Webhook delivery status for tracking.
 */
export interface WebhookDelivery {
  id: string;
  subscriptionId: string;
  eventType: string;
  entityId: string | null;
  payload: string;
  attemptNumber: number;
  attemptedAtUtc: string;
  httpStatusCode: number | null;
  responseBody: string | null;
  errorMessage: string | null;
}

/**
 * Webhook statistics summary.
 */
export interface WebhookStats {
  totalSubscriptions: number;
  activeSubscriptions: number;
  totalDeliveries: number;
  successfulDeliveries: number;
  failedDeliveries: number;
  recentActivity: WebhookSubscription[];
}

/**
 * Form data for webhook subscription form.
 */
export interface WebhookFormData {
  name: string;
  url: string;
  eventTypes: string[];
  maxRetries: number;
  timeoutSeconds: number;
}

/**
 * Predefined event types available in the system.
 */
export const WEBHOOK_EVENT_TYPES: WebhookEventType[] = [
  // WorkItems
  { type: 'TaskCreated', description: 'Cuando se crea una tarea' },
  { type: 'TaskMoved', description: 'Cuando cambia el estado de una tarea' },
  { type: 'TaskDeleted', description: 'Cuando se elimina una tarea' },
  { type: 'TaskAssigned', description: 'Cuando se asigna una tarea' },
  { type: 'TaskCommentAdded', description: 'Cuando se agrega un comentario' },

  // Projects
  { type: 'ProjectCreated', description: 'Cuando se crea un proyecto' },
  { type: 'ProjectUpdated', description: 'Cuando se actualiza un proyecto' },
  { type: 'ProjectDeleted', description: 'Cuando se elimina un proyecto' },

  // Tickets
  { type: 'TicketCreated', description: 'Cuando se crea un ticket' },
  { type: 'TicketAssigned', description: 'Cuando se asigna un ticket' },
  { type: 'TicketStatusChanged', description: 'Cuando cambia el estado del ticket' },

  // Calendar
  { type: 'CalendarEventCreated', description: 'Cuando se crea un evento' },
  { type: 'CalendarEventUpdated', description: 'Cuando se actualiza un evento' },

  // Identity
  { type: 'UserCreated', description: 'Cuando se crea un usuario' },
  { type: 'UserInvited', description: 'Cuando se invita un usuario' },
];