import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideZap, lucidePlus, lucideTrash2, lucideEdit3, lucideRefreshCw,
  lucideLoader2, lucideCircleAlert, lucideX,
} from '@ng-icons/lucide';
import {
  AutomationsService, VALUELESS_OPERATOR, automationLabel,
  type RuleAction, type RuleCondition, type AutomationRule,
  type AutomationVocabulary,
} from '../../../core/automations.service';
import { mensajeDeError } from '../../../shared/utils/mensaje-de-error';

/** Lo que el dominio acepta. Repetirlo evita un viaje al servidor para decir lo obvio. */
const MAX_NAME_LENGTH = 100;
const MAX_CONDITIONS = 10;
const MAX_ACTIONS = 5;

/**
 * Administración de las automatizaciones.
 *
 * **El formulario se construye con el vocabulario que sirve el servidor**, no con listas
 * escritas aquí: una copia se desincroniza el día que se añada un disparador, y entonces esta
 * pantalla dejaría configurar algo que el servidor no entiende.
 *
 * La lista enseña cuántas veces se ha ejecutado cada regla. Es lo primero que se mira cuando
 * alguien dice «esta automatización no funciona»: separa «no salta» de «salta y hace otra cosa».
 */
@Component({
  selector: 'app-admin-automations',
  standalone: true,
  imports: [FormsModule, NgIconComponent],
  viewProviders: [provideIcons({
    lucideZap, lucidePlus, lucideTrash2, lucideEdit3, lucideRefreshCw,
    lucideLoader2, lucideCircleAlert, lucideX,
  })],
  templateUrl: './admin-automations.component.html',
})
export class AdminAutomationsComponent implements OnInit {
  private readonly service = inject(AutomationsService);

  readonly maxNameLength = MAX_NAME_LENGTH;
  readonly valuelessOperator = VALUELESS_OPERATOR;

  /** Para enseñar cada código del vocabulario en castellano. */
  readonly automationLabel = automationLabel;

  readonly vocabulary = signal<AutomationVocabulary>({
    triggers: [], fields: [], operators: [], actions: [], fieldsByTrigger: {},
  });

  readonly rules = signal<AutomationRule[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');

  /** `null` si el formulario está cerrado, `''` si es una regla nueva, o el id que se edita. */
  readonly editing = signal<string | null>(null);
  readonly deleting = signal<string | null>(null);

  /**
   * Qué condiciones se quitaron al cambiar de disparador, para decirlo en vez de hacerlo en
   * silencio. Vacío si no se quitó ninguna.
   */
  readonly droppedNotice = signal('');

  name = '';
  trigger = '';
  conditions: RuleCondition[] = [];
  actions: RuleAction[] = [];

  readonly isNew = computed(() => this.editing() === '');

  ngOnInit(): void {
    this.service.vocabulary().subscribe({
      next: v => this.vocabulary.set(v),
      error: response => this.error.set(
        mensajeDeError(response, $localize`No se pudo cargar el vocabulario de automatizaciones`)),
    });

    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');

    this.service.rules().subscribe({
      next: rules => {
        this.rules.set(rules ?? []);
        this.loading.set(false);
      },
      error: response => {
        this.error.set(mensajeDeError(response, $localize`No se pudieron cargar las automatizaciones`));
        this.loading.set(false);
      },
    });
  }

  startNew(): void {
    this.editing.set('');
    this.name = '';
    this.trigger = this.vocabulary().triggers[0] ?? '';
    this.conditions = [];
    // Una regla sin acciones no hace nada, así que el formulario empieza con una.
    this.actions = [this.blankAction()];
    this.droppedNotice.set('');
    this.error.set('');
  }

  edit(rule: AutomationRule): void {
    this.editing.set(rule.id);
    this.name = rule.name;
    this.trigger = rule.trigger;
    this.conditions = rule.conditions.map(c => ({ ...c }));
    this.actions = rule.actions.map(a => ({ ...a }));
    this.droppedNotice.set('');
    this.error.set('');
  }

  closeForm(): void {
    this.editing.set(null);
    this.droppedNotice.set('');
    this.error.set('');
  }

  /** Los campos que trae un disparador, según el servidor. */
  fieldsFor(trigger: string): string[] {
    return this.vocabulary().fieldsByTrigger?.[trigger] ?? [];
  }

  /** Los que se pueden usar con el disparador elegido ahora mismo en el formulario. */
  get availableFields(): string[] {
    return this.fieldsFor(this.trigger);
  }

  /**
   * Si el disparador elegido trae el campo de la condición. Una regla guardada antes de que se
   * comprobara puede tener condiciones que no; se enseñan marcadas en vez de esconderlas.
   */
  isCarried(condition: RuleCondition): boolean {
    return this.availableFields.includes(condition.field);
  }

  /** Lo mismo sobre una regla de la lista, con su propio disparador. */
  hasUnreachableCondition(rule: AutomationRule): boolean {
    const fields = this.fieldsFor(rule.trigger);
    return rule.conditions.some(c => !fields.includes(c.field));
  }

  /**
   * Cambiar de disparador quita las condiciones sobre campos que el nuevo no trae: no se
   * cumplirían nunca y el servidor no las aceptaría. Se avisa de cuáles, porque desaparecer en
   * silencio sería la misma sorpresa que se está arreglando.
   */
  changeTrigger(trigger: string): void {
    this.trigger = trigger;

    const fields = this.fieldsFor(trigger);
    const dropped = this.conditions.filter(c => !fields.includes(c.field));

    this.conditions = this.conditions.filter(c => fields.includes(c.field));

    const labels = [...new Set(dropped.map(c => automationLabel(c.field)))].join(', ');
    this.droppedNotice.set(dropped.length
      ? $localize`Se quitaron las condiciones sobre ${labels}, porque este disparador no trae ese dato.`
      : '');
  }

  private blankAction(): RuleAction {
    return { type: this.vocabulary().actions[0] ?? '', value: '' };
  }

  addCondition(): void {
    const field = this.availableFields[0];
    if (this.conditions.length >= MAX_CONDITIONS || !field) return;

    this.conditions = [...this.conditions, {
      field,
      operator: this.vocabulary().operators[0] ?? '',
      value: '',
    }];
  }

  removeCondition(index: number): void {
    this.conditions = this.conditions.filter((_, i) => i !== index);
  }

  addAction(): void {
    if (this.actions.length >= MAX_ACTIONS) return;
    this.actions = [...this.actions, this.blankAction()];
  }

  removeAction(index: number): void {
    this.actions = this.actions.filter((_, i) => i !== index);
  }

  needsValue(condition: RuleCondition): boolean {
    return condition.operator !== VALUELESS_OPERATOR;
  }

  /**
   * El motivo por el que no se puede guardar todavía, o cadena vacía si sí se puede.
   *
   * Es un getter y no un `computed`: lee campos atados con `ngModel`, que no son señales, y un
   * `computed` sobre eso se quedaría con el primer valor para siempre.
   */
  get blocker(): string {
    const name = this.name.trim();

    if (!name) return $localize`La automatización necesita un nombre`;
    if (name.length > MAX_NAME_LENGTH) {
      return $localize`El nombre no puede pasar de ${MAX_NAME_LENGTH} caracteres`;
    }

    if (!this.trigger) return $localize`Hay que elegir cuándo se dispara`;

    // Una regla sin acciones se ejecutaría entera para no hacer nada.
    if (!this.actions.length) return $localize`La automatización necesita al menos una acción`;
    if (this.actions.some(a => !a.type || !a.value.trim())) {
      return $localize`Cada acción necesita un valor`;
    }

    // Una regla guardada antes de que se comprobara puede traerlas: el servidor la rechazaría.
    if (this.conditions.some(c => !this.isCarried(c))) {
      return $localize`Hay condiciones sobre datos que este disparador no trae: cámbialas o quítalas`;
    }

    if (this.conditions.some(c => this.needsValue(c) && !(c.value ?? '').trim())) {
      return $localize`Cada condición necesita un valor con el que comparar`;
    }

    return '';
  }

  save(): void {
    if (this.blocker || this.saving()) return;

    const id = this.editing();
    if (id === null) return;

    const rule = {
      name: this.name.trim(),
      trigger: this.trigger,
      conditions: this.conditions.map(c => ({
        field: c.field,
        operator: c.operator,
        // «Está vacío» no compara contra nada: mandar un valor sería ruido que el servidor tira.
        value: this.needsValue(c) ? (c.value ?? '').trim() : null,
      })),
      actions: this.actions.map(a => ({ type: a.type, value: a.value.trim() })),
    };

    this.saving.set(true);
    this.error.set('');

    const request: Observable<unknown> = id === ''
      ? this.service.create(rule)
      : this.service.update(id, rule);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.closeForm();
        this.load();
      },
      error: response => {
        this.saving.set(false);
        this.error.set(mensajeDeError(response, $localize`No se pudo guardar la automatización`));
      },
    });
  }

  /**
   * Apagar o encender una regla.
   *
   * Se pinta el cambio antes de tener respuesta y se revierte si el servidor lo rechaza: dejar
   * en pantalla una automatización apagada que sigue ejecutándose es la peor mentira posible en
   * esta pantalla.
   */
  toggleActive(rule: AutomationRule): void {
    const previous = rule.isActive;
    this.applyToList(rule.id, !previous);

    this.service.setActive(rule.id, !previous).subscribe({
      error: response => {
        this.applyToList(rule.id, previous);
        this.error.set(mensajeDeError(response, $localize`No se pudo cambiar el estado de la automatización`));
      },
    });
  }

  private applyToList(id: string, isActive: boolean): void {
    this.rules.update(rules => rules.map(r => r.id === id ? { ...r, isActive } : r));
  }

  remove(rule: AutomationRule): void {
    this.saving.set(true);

    this.service.remove(rule.id).subscribe({
      next: () => {
        this.saving.set(false);
        this.deleting.set(null);
        this.load();
      },
      error: response => {
        this.saving.set(false);
        this.deleting.set(null);
        this.error.set(mensajeDeError(response, $localize`No se pudo borrar la automatización`));
      },
    });
  }

  /** Un resumen legible de la regla, para no obligar a abrirla para saber qué hace. */
  summaryOf(rule: AutomationRule): string {
    const actions = rule.actions.map(a => `${automationLabel(a.type)}: ${a.value}`).join(', ');

    if (!rule.conditions.length) return actions;

    const conditions = rule.conditions
      .map(c => {
        const head = `${automationLabel(c.field)} ${automationLabel(c.operator)}`;
        return c.operator === VALUELESS_OPERATOR ? head : `${head} ${c.value}`;
      })
      .join(' · ');

    return `${conditions} → ${actions}`;
  }
}
