import { Component, signal } from '@angular/core';
import { ButtonComponent } from '../../shared/ui/button.component';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import {
  CardComponent, CardHeaderComponent, CardTitleComponent,
  CardDescriptionComponent, CardContentComponent,
} from '../../shared/ui/card.component';
import { SkeletonComponent, SkeletonListComponent } from '../../shared/ui/skeleton.component';
import { EmptyStateComponent, EmptyInlineComponent } from '../../shared/ui/empty-state.component';
import { ClickableDirective } from '../../shared/directives/clickable.directive';

/** Un color semántico con sus cuatro tokens. */
interface Family {
  name: string;
  solid: string;
  solidText: string;
  muted: string;
  mutedText: string;
  usage: string;
}

/**
 * Guía de diseño viva.
 *
 * No es documentación escrita aparte: renderiza los componentes reales, así que no puede
 * quedarse desfasada respecto al código. Esa diferencia importa aquí más de lo normal,
 * porque el proyecto ya tuvo el problema contrario: `empty-state` llevaba tiempo con dos
 * errores —un paréntesis sin cerrar y un tipo equivocado— que nadie vio porque ninguna
 * vista lo importaba y por tanto nunca se compilaba.
 *
 * Al usar cada componente, esta página los mantiene compilados y visibles.
 */
@Component({
  selector: 'app-design-system',
  standalone: true,
  imports: [
    ButtonComponent, BadgeComponent, CardComponent, CardHeaderComponent,
    CardTitleComponent, CardDescriptionComponent, CardContentComponent,
    SkeletonComponent, SkeletonListComponent, EmptyStateComponent,
    EmptyInlineComponent, ClickableDirective,
  ],
  template: `
    <div class="mx-auto max-w-5xl space-y-12 p-8">
      <header class="space-y-2">
        <h1 class="text-3xl font-semibold tracking-tight">Sistema de diseño</h1>
        <p class="text-muted-foreground">
          Los componentes de esta página son los mismos que usa la aplicación, no copias.
          Si uno se rompe, se ve aquí.
        </p>
      </header>

      <section class="space-y-4" aria-labelledby="s-color">
        <h2 id="s-color" class="text-xl font-semibold">Color</h2>
        <p class="text-sm text-muted-foreground">
          Cada color semántico tiene cuatro tokens. El par <em>tenue</em> lleva su propio
          texto porque un fondo translúcido del mismo tono nunca alcanza el contraste que
          exige WCAG AA: escribir <code>bg-primary/10 text-primary</code> parece razonable
          y falla siempre.
        </p>

        <div class="grid gap-4 sm:grid-cols-2">
          @for (f of families; track f.name) {
            <div class="rounded-lg border border-border p-4 space-y-3">
              <div class="flex items-center justify-between">
                <h3 class="font-medium">{{ f.name }}</h3>
                <span class="text-xs text-muted-foreground">{{ f.usage }}</span>
              </div>
              <div [class]="'rounded-md px-3 py-2 text-sm ' + f.solid + ' ' + f.solidText">
                Relleno sólido — botones, barras
              </div>
              <div [class]="'rounded-md px-3 py-2 text-sm ' + f.muted + ' ' + f.mutedText">
                Fondo tenue — etiquetas, avisos
              </div>
            </div>
          }
        </div>
      </section>

      <section class="space-y-4" aria-labelledby="s-botones">
        <h2 id="s-botones" class="text-xl font-semibold">Botones</h2>
        <div class="flex flex-wrap gap-3">
          @for (v of buttonVariants; track v) {
            <button uiButton [variant]="v">{{ v }}</button>
          }
        </div>
        <div class="flex flex-wrap items-center gap-3">
          @for (t of buttonSizes; track t) {
            <button uiButton variant="outline" [size]="t"
                    [attr.aria-label]="t === 'icon' ? 'Ejemplo de botón de icono' : null">
              {{ t === 'icon' ? '★' : t }}
            </button>
          }
        </div>
      </section>

      <section class="space-y-4" aria-labelledby="s-badges">
        <h2 id="s-badges" class="text-xl font-semibold">Etiquetas</h2>
        <div class="flex flex-wrap gap-2">
          @for (v of badgeVariants; track v) {
            <ui-badge [variant]="v">{{ v }}</ui-badge>
          }
        </div>
      </section>

      <section class="space-y-4" aria-labelledby="s-tarjetas">
        <h2 id="s-tarjetas" class="text-xl font-semibold">Tarjetas</h2>
        <ui-card class="max-w-sm">
          <ui-card-header>
            <ui-card-title [level]="3">Título de tarjeta</ui-card-title>
            <ui-card-description>Con nivel de encabezado configurable</ui-card-description>
          </ui-card-header>
          <ui-card-content>
            <p class="text-sm text-muted-foreground">
              El título emite un encabezado real. Ajusta el nivel al lugar que ocupa la
              tarjeta: fijarlo produciría jerarquías incorrectas.
            </p>
          </ui-card-content>
        </ui-card>
      </section>

      <section class="space-y-4" aria-labelledby="s-carga">
        <h2 id="s-carga" class="text-xl font-semibold">Carga y vacío</h2>
        <div class="grid gap-6 sm:grid-cols-2">
          <div class="space-y-2">
            <h3 class="text-sm font-medium">Esqueletos</h3>
            <app-skeleton variant="text" [lines]="3" />
            <app-skeleton-list [count]="2" />
          </div>
          <div class="space-y-2">
            <h3 class="text-sm font-medium">Estados vacíos</h3>
            <app-empty-inline message="Sin elementos" />
            <app-empty-state type="search" title="Nada coincide"
                             description="Prueba con otros filtros" [showAction]="false" />
          </div>
        </div>
      </section>

      <section class="space-y-4" aria-labelledby="s-teclado">
        <h2 id="s-teclado" class="text-xl font-semibold">Interacción por teclado</h2>
        <p class="text-sm text-muted-foreground">
          <code>appClickable</code> hace accesible un elemento no interactivo que lleva
          <code>(click)</code>. Prefiere <code>&lt;button&gt;</code> cuando el elemento sea
          de verdad un botón; esto es para filas, tarjetas y etiquetas.
        </p>
        <div appClickable (click)="clicks.set(clicks() + 1)"
             class="cursor-pointer rounded-lg border border-border p-4 text-sm
                    hover:bg-accent focus:outline-none focus:ring-2 focus:ring-ring">
          Actívame con el ratón, con Enter o con Espacio — {{ clicks() }}
        </div>
      </section>
    </div>
  `,
})
export class DesignSystemComponent {
  protected readonly clicks = signal(0);

  protected readonly buttonVariants = [
    'default', 'secondary', 'outline', 'ghost', 'destructive', 'link',
  ] as const;

  protected readonly buttonSizes = ['sm', 'default', 'lg', 'icon'] as const;

  protected readonly badgeVariants: BadgeVariant[] = [
    'default', 'secondary', 'outline', 'destructive', 'success', 'warning',
  ];

  protected readonly families: Family[] = [
    { name: 'primary', usage: 'acción principal', solid: 'bg-primary', solidText: 'text-primary-foreground', muted: 'bg-primary-subtle', mutedText: 'text-primary-subtle-fg' },
    { name: 'destructive', usage: 'error, borrado', solid: 'bg-destructive', solidText: 'text-destructive-foreground', muted: 'bg-destructive-subtle', mutedText: 'text-destructive-subtle-fg' },
    { name: 'success', usage: 'confirmación', solid: 'bg-success', solidText: 'text-success-foreground', muted: 'bg-success-subtle', mutedText: 'text-success-subtle-fg' },
    { name: 'warning', usage: 'aviso', solid: 'bg-warning', solidText: 'text-warning-foreground', muted: 'bg-warning-subtle', mutedText: 'text-warning-subtle-fg' },
    { name: 'info', usage: 'informativo', solid: 'bg-info', solidText: 'text-info-foreground', muted: 'bg-info-subtle', mutedText: 'text-info-subtle-fg' },
  ];
}
