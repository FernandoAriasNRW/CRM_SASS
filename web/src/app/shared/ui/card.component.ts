import { Component, HostBinding, input } from '@angular/core';
import { cn } from '../utils/cn';

@Component({
  selector: 'ui-card',
  standalone: true,
  template: '<ng-content />',
})
export class CardComponent {
  @HostBinding('class') classes = cn('rounded-lg border bg-card text-card-foreground shadow-sm block');
}

@Component({
  selector: 'ui-card-header',
  standalone: true,
  template: '<ng-content />',
})
export class CardHeaderComponent {
  @HostBinding('class') classes = cn('flex flex-col space-y-1.5 p-6 block');
}

/**
 * Título de tarjeta.
 *
 * Emite un encabezado real. Antes renderizaba sólo un `<ng-content />` dentro de un
 * elemento sin semántica, de modo que una pantalla cuyo único título fuera una tarjeta
 * —el login, por ejemplo— no tenía ni un solo `<h1>`-`<h6>`: un lector de pantalla no
 * percibía estructura alguna y no había forma de navegar por encabezados.
 *
 * El nivel es configurable porque depende de dónde esté la tarjeta: nivel 1 si es el
 * título de la página, 3 si es una tarjeta más dentro de una rejilla. Fijarlo a un valor
 * único produciría jerarquías incorrectas, que para un lector de pantalla es tan confuso
 * como no tener ninguna.
 *
 * Se usa `role="heading"` con `aria-level` en lugar de emitir un `<h1>`-`<h6>` real.
 * Lo natural sería preferir el elemento nativo, pero Angular proyecta `<ng-content />`
 * una sola vez: con una rama por nivel, el encabezado se renderizaba vacío y el texto
 * quedaba fuera. La pareja role/aria-level expone exactamente la misma semántica a las
 * tecnologías de asistencia y evita esa limitación.
 */
@Component({
  selector: 'ui-card-title',
  standalone: true,
  template: '<ng-content />',
  host: {
    'role': 'heading',
    '[attr.aria-level]': 'level()',
    '[class]': 'classes',
  },
})
export class CardTitleComponent {
  /** Nivel del encabezado (1-6). Por defecto 3, el habitual para una tarjeta. */
  readonly level = input<1 | 2 | 3 | 4 | 5 | 6>(3);

  protected readonly classes = cn('text-2xl font-semibold leading-none tracking-tight block');
}

@Component({
  selector: 'ui-card-description',
  standalone: true,
  template: '<ng-content />',
})
export class CardDescriptionComponent {
  @HostBinding('class') classes = cn('text-sm text-muted-foreground block');
}

@Component({
  selector: 'ui-card-content',
  standalone: true,
  template: '<ng-content />',
})
export class CardContentComponent {
  @HostBinding('class') classes = cn('p-6 pt-0 block');
}

@Component({
  selector: 'ui-card-footer',
  standalone: true,
  template: '<ng-content />',
})
export class CardFooterComponent {
  @HostBinding('class') classes = cn('flex items-center p-6 pt-0 block');
}
