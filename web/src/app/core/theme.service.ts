import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';

/** Las tres opciones. «Sistema» sigue lo que tenga configurado el ordenador. */
export type Theme = 'claro' | 'oscuro' | 'sistema';

export const THEMES: { value: Theme; name: string }[] = [
  { value: 'claro', name: $localize`Claro` },
  { value: 'oscuro', name: $localize`Oscuro` },
  { value: 'sistema', name: $localize`El del sistema` }
];

const STORAGE_KEY = 'crm.tema';

/**
 * El tema claro u oscuro.
 *
 * <b>Los estilos oscuros estaban escritos y nadie los encendía.</b> Toda la aplicación tiene
 * variantes `dark:` —cientos— y la única forma de verlas era la paleta de comandos, que hacía
 * `classList.toggle('dark')` sin guardar nada: al recargar se perdía. Es decir, medio diseño
 * existía y no se podía usar.
 *
 * <b>«Sistema» es la opción por defecto</b>, y no «claro»: quien tiene el ordenador en oscuro
 * espera que las aplicaciones lo respeten sin tener que decírselo a cada una.
 *
 * Se guarda en `localStorage` y no en el servidor a propósito: es una preferencia del aparato, no
 * de la persona. El mismo usuario puede querer oscuro en el portátil de noche y claro en el
 * monitor de la oficina, y una preferencia de servidor le impondría la misma en los dos.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly doc = inject(DOCUMENT);

  private readonly chosen = signal<Theme>(this.readSaved());

  /** Lo que la persona eligió: claro, oscuro o seguir al sistema. */
  readonly theme = this.chosen.asReadonly();

  /** Si el sistema pide oscuro. Se sigue en vivo, para que cambiar de tema en el sistema se note. */
  private readonly systemPrefersDark = signal(this.systemQuery()?.matches ?? false);

  /** Si ahora mismo se está pintando en oscuro. */
  readonly isDark = computed(() =>
    this.chosen() === 'oscuro' || (this.chosen() === 'sistema' && this.systemPrefersDark()));

  constructor() {
    // El sistema puede cambiar mientras la aplicación está abierta —el modo nocturno automático
    // de Windows y macOS lo hace—, y con «sistema» elegido eso tiene que verse sin recargar.
    this.systemQuery()?.addEventListener('change', e => this.systemPrefersDark.set(e.matches));

    effect(() => this.apply(this.isDark()));
  }

  choose(theme: Theme): void {
    this.chosen.set(theme);

    try {
      this.doc.defaultView?.localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      // Navegación privada o almacenamiento bloqueado: el tema sigue funcionando en esta
      // sesión, sólo que no se recuerda. Peor sería no dejar cambiarlo.
    }
  }

  private apply(dark: boolean): void {
    this.doc.documentElement.classList.toggle('dark', dark);
  }

  private readSaved(): Theme {
    try {
      const saved = this.doc.defaultView?.localStorage.getItem(STORAGE_KEY);
      return saved === 'claro' || saved === 'oscuro' ? saved : 'sistema';
    } catch {
      return 'sistema';
    }
  }

  private systemQuery(): MediaQueryList | null {
    return this.doc.defaultView?.matchMedia?.('(prefers-color-scheme: dark)') ?? null;
  }
}
