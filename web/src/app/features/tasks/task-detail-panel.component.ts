import { Component, inject, input, output, signal, OnInit, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../shared/services/toast.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { AvatarComponent } from '../../shared/ui/avatar.component';
import { SkeletonComponent } from '../../shared/ui/skeleton.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideX, lucideCheck, lucideCalendar, lucideClock, lucideUser,
  lucideTag, lucideFlag, lucideMessageSquare, lucidePaperclip,
  lucideSmile, lucideSend, lucideChevronDown, lucideAlertCircle,
  lucideArrowUp, lucideMinus, lucideArrowDown, lucideLoader2,
  lucideBan, lucideArrowRight, lucideRepeat
} from '@ng-icons/lucide';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import {
  PRIORITIES, DEFAULT_PRIORITY,
  FREQUENCIES,
  type TaskItem, type TaskDependencies, type TaskDependencyRef, type ChecklistItem, type Recurrence,
} from './task-create-modal.component';
import { TASK_TAGS, type Tag } from '../../shared/utils/tags';
import { UsersService, type TenantUser } from '../../core/users.service';
import { ClickableDirective } from '../../shared/directives/clickable.directive';
import { CustomFieldsFormComponent } from '../../shared/ui/custom-fields-form.component';
import { CommentsComponent } from '../../shared/ui/comments.component';
import { MentionedInComponent } from '../../shared/ui/mentioned-in.component';
import { TASK_STATUSES, taskStatusBadge, taskStatusLabel } from './task-vocabulary';

/** Los dos estados entre los que alterna el check de una subtarea. Los define el backend. */
const COMPLETED_STATUS = 'Done';
const INITIAL_STATUS = 'To Do';

@Component({
  selector: 'app-task-detail-panel',
  standalone: true,
  imports: [MentionedInComponent, ClickableDirective, FormsModule, DatePipe, BadgeComponent, AvatarComponent, NgIconComponent, SkeletonComponent, DrawerComponent, CustomFieldsFormComponent, CommentsComponent],
  viewProviders: [provideIcons({
    lucideX, lucideCheck, lucideCalendar, lucideClock, lucideUser,
    lucideTag, lucideFlag, lucideMessageSquare, lucidePaperclip,
    lucideSmile, lucideSend, lucideChevronDown, lucideAlertCircle,
    lucideArrowUp, lucideMinus, lucideArrowDown, lucideLoader2,
    lucideBan, lucideArrowRight, lucideRepeat
  })],
  templateUrl: './task-detail-panel.component.html',
})
export class TaskDetailPanelComponent implements OnInit {
  readonly task = input.required<TaskItem>();
  readonly closed = output<void>();
  readonly updated = output<TaskItem>();

  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly users = inject(UsersService);

  /**
   * Responsables de la tarea. El orden que llega de la API no significa nada, así que quién es
   * el principal se sabe comparando con `principal`, no por la posición.
   */
  assignees = signal<string[]>([]);
  principal = signal('');
  chosenUser = '';

  // Estado editable local
  title = '';
  description = '';
  status = '';
  priority: string = DEFAULT_PRIORITY;
  dueDate = '';
  estimatedHours = 0;
  selectedTags = signal<string[]>([]);
  showTagPicker = signal(false);
  saving = signal(false);
  subtasks = signal<TaskItem[]>([]);
  loadingSubtasks = signal(false);
  creatingSubtask = signal(false);
  newSubtaskTitle = '';
  readonly frequencies = FREQUENCIES;
  recurrence = signal<Recurrence | null>(null);
  chosenFrequency = '';
  chosenInterval = 1;
  checklist = signal<ChecklistItem[]>([]);
  loadingChecklist = signal(false);
  newChecklistItemText = '';
  dependencies = signal<TaskDependencies>({ blockedBy: [], blocks: [] });
  loadingDependencies = signal(false);
  blockerCandidates = signal<TaskItem[]>([]);
  chosenBlocker = '';
  activeTab = signal<'comments' | 'activity'>('comments');

  readonly priorities = PRIORITIES;
  readonly statuses = TASK_STATUSES;
  readonly availableTags = TASK_TAGS;

  readonly currentPriority = computed(() =>
    PRIORITIES.find(p => p.key === this.priority) ?? PRIORITIES[2]
  );

  statusBadge(s: string): BadgeVariant { return taskStatusBadge(s); }

  readonly statusLabel = taskStatusLabel;

  ngOnInit(): void {
    const t = this.task();
    this.title = t.title;
    this.description = t.description ?? '';
    this.status = t.status;
    this.priority = t.priority ?? DEFAULT_PRIORITY;
    this.dueDate = t.dueDate ?? '';
    this.estimatedHours = t.estimatedHours ?? 0;
    // Parsear etiquetas guardadas como string separado por comas
    if ((t as any).tags) {
      this.selectedTags.set(String((t as any).tags).split(',').map((s: string) => s.trim()).filter(Boolean));
    }
    this.assignees.set(t.assignees ?? (t.assigneeId ? [t.assigneeId] : []));
    this.principal.set(t.assigneeId ?? '');
    if (!this.isSubtask()) this.loadSubtasks();
    this.loadDependencies();
    this.loadChecklist();
    this.recurrence.set(t.recurrence ?? null);
    this.chosenFrequency = t.recurrence?.frequency ?? '';
    this.chosenInterval = t.recurrence?.interval ?? 1;
    if (!this.users.users().length) this.users.loadTenantUsers().subscribe();
  }

  /**
   * Pone o cambia la repetición.
   *
   * No se manda fecha de arranque: el servidor toma la fecha límite de la tarea, que es la que
   * el usuario ya eligió. Preguntarla otra vez sería preguntar dos veces lo mismo.
   */
  saveRecurrence(): void {
    if (!this.chosenFrequency) return;

    const interval = Math.max(1, Math.floor(this.chosenInterval || 1));

    this.api.put(`/tasks/${this.task().id}/recurrence`, {
      frequency: this.chosenFrequency,
      interval: interval,
    }).subscribe({
      next: () => {
        // Se relee para mostrar la próxima ocurrencia que calculó el servidor, en lugar de
        // adivinarla aquí y arriesgarse a pintar una fecha distinta de la guardada.
        this.reloadRecurrence();
        this.toast.success($localize`Repetición guardada`, this.recurrenceText(this.chosenFrequency, interval));
      },
      error: response => this.toast.error(
        $localize`No se pudo guardar la repetición`, this.serverMessage(response)),
    });
  }

  clearRecurrence(): void {
    this.api.delete(`/tasks/${this.task().id}/recurrence`).subscribe({
      next: () => {
        this.recurrence.set(null);
        this.chosenFrequency = '';
        this.chosenInterval = 1;
        this.updated.emit({ ...this.task(), recurrence: null });
      },
      error: () => this.toast.error($localize`Error`, $localize`No se pudo quitar la repetición`),
    });
  }

  private reloadRecurrence(): void {
    this.api.get<TaskItem>(`/tasks/${this.task().id}`).subscribe({
      next: task => {
        this.recurrence.set(task.recurrence ?? null);
        this.updated.emit({ ...this.task(), recurrence: task.recurrence ?? null });
      },
    });
  }

  recurrenceText(frequency: string, interval: number): string {
    const label = FREQUENCIES.find(f => f.key === frequency)?.label ?? frequency;
    return interval > 1 ? `${label} × ${interval}` : label;
  }

  readonly doneItems = computed(() => this.checklist().filter(p => p.isDone).length);

  loadChecklist(): void {
    this.loadingChecklist.set(true);
    this.api.get<ChecklistItem[]>(`/tasks/${this.task().id}/checklist`).subscribe({
      next: items => {
        this.checklist.set(items ?? []);
        this.loadingChecklist.set(false);
      },
      error: () => {
        this.loadingChecklist.set(false);
        this.toast.error($localize`Error`, $localize`No se pudo cargar la checklist`);
      },
    });
  }

  addChecklistItem(): void {
    const text = this.newChecklistItemText.trim();
    if (!text) return;

    this.api.post<ChecklistItem>(`/tasks/${this.task().id}/checklist`, { text }).subscribe({
      next: item => {
        this.checklist.update(current => [...current, item]);
        this.newChecklistItemText = '';
        this.toastChecklist();
      },
      error: response => this.toast.error(
        $localize`No se pudo añadir el punto`, this.serverMessage(response)),
    });
  }

  /**
   * Marca o desmarca un punto. Se pinta antes de que responda el servidor y se revierte si lo
   * rechaza, igual que en subtareas, prioridad y tableros.
   */
  toggleChecklistItem(item: ChecklistItem): void {
    const updated = !item.isDone;
    this.checklist.update(current => current.map(p => p.id === item.id ? { ...p, isDone: updated } : p));
    this.toastChecklist();

    this.api.patch(`/tasks/${this.task().id}/checklist/${item.id}`, { isDone: updated }).subscribe({
      error: () => {
        this.checklist.update(current => current.map(p => p.id === item.id ? { ...p, isDone: !updated } : p));
        this.toastChecklist();
        this.toast.error($localize`Error`, $localize`No se pudo actualizar el punto`);
      },
    });
  }

  removeChecklistItem(item: ChecklistItem): void {
    this.api.delete(`/tasks/${this.task().id}/checklist/${item.id}`).subscribe({
      next: () => {
        this.checklist.update(current => current.filter(p => p.id !== item.id));
        this.toastChecklist();
      },
      error: () => this.toast.error($localize`Error`, $localize`No se pudo quitar el punto`),
    });
  }

  private toastChecklist(): void {
    this.updated.emit({
      ...this.task(),
      checklistTotal: this.checklist().length,
      checklistDone: this.doneItems(),
    });
  }

  /** Nombre de una persona, o su identificador recortado si aún no está cargada. */
  nameOf(userId: string): string {
    return this.users.getUser(userId)?.name ?? `${userId.slice(0, 8)}…`;
  }

  avatarOf(userId: string): string {
    return this.users.getUser(userId)?.avatarUrl ?? '';
  }

  esPrincipal(userId: string): boolean { return userId === this.principal(); }

  /** Quien todavía no es responsable. Sólo esas personas se pueden añadir. */
  readonly assigneeCandidates = computed<TenantUser[]>(() => {
    const alreadyThere = new Set(this.assignees());
    return this.users.users().filter(u => !alreadyThere.has(u.id));
  });

  addAssignee(): void {
    const who = this.chosenUser;
    if (!who) return;

    this.api.post(`/tasks/${this.task().id}/assignees`, { userId: who }).subscribe({
      next: () => {
        this.assignees.update(current => [...current, who]);
        this.chosenUser = '';
        this.toastAssignees();
      },
      error: response => this.toast.error(
        $localize`No se pudo añadir el responsable`, this.serverMessage(response)),
    });
  }

  /**
   * Quita a una persona de los responsables.
   *
   * Si era la principal, el servidor promueve a la siguiente. Se recarga la tarea en lugar de
   * adivinar a quién promovió: inventarlo aquí sería arriesgarse a pintar un principal que no
   * es el que quedó guardado.
   */
  removeAssignee(userId: string): void {
    this.api.delete(`/tasks/${this.task().id}/assignees/${userId}`).subscribe({
      next: () => {
        this.assignees.update(current => current.filter(u => u !== userId));

        // Si se quitó al principal, el servidor promovió a otro. Se relee en lugar de adivinar
        // a quién: inventarlo aquí sería pintar un principal que no es el que quedó guardado.
        if (userId === this.principal()) this.reloadAssignees();

        this.toastAssignees();
      },
      error: response => this.toast.error(
        $localize`No se pudo quitar el responsable`, this.serverMessage(response)),
    });
  }

  /** Vuelve a leer la tarea para saber a quién promovió el servidor. */
  private reloadAssignees(): void {
    this.api.get<TaskItem>(`/tasks/${this.task().id}`).subscribe({
      next: task => {
        this.assignees.set(task.assignees ?? []);
        this.principal.set(task.assigneeId ?? '');
        this.toastAssignees();
      },
    });
  }

  private toastAssignees(): void {
    this.updated.emit({
      ...this.task(),
      assignees: this.assignees(),
      assigneeId: this.principal(),
    });
  }

  loadDependencies(): void {
    this.loadingDependencies.set(true);
    this.api.get<TaskDependencies>(`/tasks/${this.task().id}/dependencies`).subscribe({
      next: data => {
        this.dependencies.set({
          blockedBy: data.blockedBy ?? [],
          blocks: data.blocks ?? [],
        });
        this.loadingDependencies.set(false);
      },
      error: () => {
        this.loadingDependencies.set(false);
        this.toast.error($localize`Error`, $localize`No se pudieron cargar las dependencias`);
      },
    });
  }

  /**
   * Carga las candidatas a bloquear: las tareas del mismo proyecto, que es la única
   * combinación que el servidor acepta. Se piden al abrir el selector y no antes, porque en la
   * mayoría de las visitas al panel nadie toca las dependencias.
   */
  loadCandidates(): void {
    if (this.blockerCandidates().length) return;

    this.api.get<{ items: TaskItem[] }>('/tasks', { projectId: this.task().projectId, pageSize: 200, includeSubtasks: true })
      .subscribe({
        next: page => {
          const alreadyBlocking = new Set(this.dependencies().blockedBy.map(t => t.id));
          this.blockerCandidates.set(
            (page.items ?? []).filter(t => t.id !== this.task().id && !alreadyBlocking.has(t.id))
          );
        },
        error: () => this.toast.error($localize`Error`, $localize`No se pudieron cargar las tareas del proyecto`),
      });
  }

  addBlocker(): void {
    const chosen = this.chosenBlocker;
    if (!chosen) return;

    this.api.post(`/tasks/${this.task().id}/dependencies`, { dependsOnTaskId: chosen }).subscribe({
      next: () => {
        this.chosenBlocker = '';
        this.blockerCandidates.set([]);
        this.loadDependencies();
        this.toastBlockers(1);
      },
      error: response => {
        // El servidor explica por qué: ciclo, ya existe, otro proyecto. Se muestra su mensaje
        // en lugar de uno genérico, porque cada caso se corrige de forma distinta.
        this.toast.error($localize`No se pudo añadir la dependencia`, this.serverMessage(response));
      },
    });
  }

  removeBlocker(blocker: TaskDependencyRef): void {
    this.api.delete(`/tasks/${this.task().id}/dependencies/${blocker.id}`).subscribe({
      next: () => {
        this.dependencies.update(d => ({ ...d, blockedBy: d.blockedBy.filter(t => t.id !== blocker.id) }));
        this.blockerCandidates.set([]);
        this.toastBlockers(-1);
      },
      error: () => this.toast.error($localize`Error`, $localize`No se pudo quitar la dependencia`),
    });
  }

  private serverMessage(response: unknown): string {
    const body = (response as { error?: unknown })?.error;
    return typeof body === 'string' && body.trim()
      ? body
      : $localize`Inténtalo de nuevo`;
  }

  /** Mantiene al día el distintivo de bloqueada de la tarjeta sin recargar la lista. */
  private toastBlockers(delta: number): void {
    this.updated.emit({
      ...this.task(),
      blockedByCount: Math.max(0, (this.task().blockedByCount ?? 0) + delta),
    });
  }

  /** Si esta tarea cuelga de otra. El anidamiento admite un solo nivel. */
  isSubtask(): boolean { return !!this.task().parentTaskId; }

  readonly completedSubtasks = computed(
    () => this.subtasks().filter(s => this.isCompleted(s)).length
  );

  isCompleted(sub: TaskItem): boolean { return sub.status === COMPLETED_STATUS; }

  loadSubtasks(): void {
    this.loadingSubtasks.set(true);
    this.api.get<{ items: TaskItem[] }>(`/tasks/${this.task().id}/subtasks`).subscribe({
      next: page => {
        this.subtasks.set(page.items ?? []);
        this.loadingSubtasks.set(false);
      },
      error: () => {
        this.loadingSubtasks.set(false);
        this.toast.error($localize`Error`, $localize`No se pudieron cargar las subtareas`);
      },
    });
  }

  createSubtask(): void {
    const title = this.newSubtaskTitle.trim();
    if (!title || this.creatingSubtask()) return;

    const parent = this.task();
    this.creatingSubtask.set(true);

    // Hereda proyecto y responsable del padre: el servidor exige que la subtarea sea del mismo
    // proyecto, y pedirlo otra vez en un alta rápida sobra.
    this.api.post<TaskItem>('/tasks', {
      title: title,
      description: '',
      projectId: parent.projectId,
      assigneeId: parent.assigneeId,
      estimatedHours: 1,
      dueDate: parent.dueDate,
      parentTaskId: parent.id,
    }).subscribe({
      next: created => {
        this.subtasks.update(current => [...current, created]);
        this.newSubtaskTitle = '';
        this.creatingSubtask.set(false);
        this.toastProgress();
      },
      error: () => {
        this.creatingSubtask.set(false);
        this.toast.error($localize`Error`, $localize`No se pudo crear la subtarea`);
      },
    });
  }

  /**
   * Marca o desmarca una subtarea como completada.
   *
   * Se pinta el cambio antes de que responda el servidor, y se revierte si lo rechaza: es la
   * misma decisión que en los tableros y en la prioridad.
   */
  toggleSubtask(sub: TaskItem): void {
    const previous = sub.status;
    const updated = this.isCompleted(sub) ? INITIAL_STATUS : COMPLETED_STATUS;

    this.subtasks.update(current => current.map(s => s.id === sub.id ? { ...s, status: updated } : s));

    this.api.patch(`/tasks/${sub.id}`, { status: updated }).subscribe({
      next: () => this.toastProgress(),
      error: () => {
        this.subtasks.update(current => current.map(s => s.id === sub.id ? { ...s, status: previous } : s));
        this.toast.error($localize`Error`, $localize`No se pudo actualizar la subtarea`);
      },
    });
  }

  /** Refresca el progreso que muestra la tarjeta del tablero sin recargar la lista entera. */
  private toastProgress(): void {
    this.updated.emit({
      ...this.task(),
      subtaskCount: this.subtasks().length,
      completedSubtaskCount: this.completedSubtasks(),
    });
  }


  saveField(field: string, value: unknown): void {
    this.saving.set(true);
    const payload: Record<string, unknown> = {};
    payload[field] = value;
    this.api.patch(`/tasks/${this.task().id}`, payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.updated.emit({ ...this.task(), title: this.title, description: this.description, status: this.status, priority: this.priority, dueDate: this.dueDate, estimatedHours: this.estimatedHours });
        if (field === 'title') {
          this.toast.success('Guardado', 'Título actualizado');
        }
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error('Error', 'No se pudo guardar el cambio');
      },
    });
  }

  toggleTag(key: string): void {
    this.selectedTags.update(tags =>
      tags.includes(key) ? tags.filter(t => t !== key) : [...tags, key]
    );
    this.saveField('tags', this.selectedTags().join(','));
  }

  isTagSelected(key: string): boolean {
    return this.selectedTags().includes(key);
  }

  getTag(key: string): Tag | undefined {
    return TASK_TAGS.find(t => t.key === key);
  }

  /**
   * Cambia la prioridad y la guarda.
   *
   * Si el servidor la rechaza, se revierte a la anterior. Es la misma decisión que en los
   * tableros: dejar en pantalla un valor que no se guardó es peor que no aceptar el cambio,
   * porque el usuario se va creyendo que la tarea quedó priorizada.
   */
  changePriority(newPriority: string): void {
    const previous = this.priority;
    if (newPriority === previous) return;

    this.priority = newPriority;
    this.saving.set(true);

    this.api.patch(`/tasks/${this.task().id}`, { priority: newPriority }).subscribe({
      next: () => {
        this.saving.set(false);
        this.updated.emit({ ...this.task(), priority: newPriority });
        this.toast.success($localize`Prioridad actualizada`, this.currentPriority().label);
      },
      error: () => {
        this.priority = previous;
        this.saving.set(false);
        this.toast.error($localize`Error`, $localize`No se pudo cambiar la prioridad`);
      },
    });
  }

  changeStatus(newStatus: string): void {
    this.status = newStatus;
    this.api.post(`/tasks/${this.task().id}/move`, { newStatus }).subscribe({
      next: () => {
        this.updated.emit({ ...this.task(), status: newStatus });
        this.toast.success($localize`Estado actualizado`, `La tarea ahora está en ${newStatus}`);
      },
      error: () => {
        this.toast.error('Error', 'No se pudo cambiar el estado');
      },
    });
  }



  close(): void { this.closed.emit(); }
}
