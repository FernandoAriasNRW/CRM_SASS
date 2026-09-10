import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';

/** Las tres opciones. «Sistema» sigue lo que tenga configurado el ordenador. */
export type Tema = 'claro' | 'oscuro' | 'sistema';

export const TEMAS: { valor: Tema; nombre: string }[] = [
  { valor: 'claro', nombre: $localize`Claro` },
  { valor: 'oscuro', nombre: $localize`Oscuro` },
  { valor: 'sistema', nombre: $localize`El del sistema` }
];

const CLAVE = 'crm.tema';

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
export class TemaService {
  private readonly documento = inject(DOCUMENT);

  private readonly elegido = signal<Tema>(this.leerGuardado());

  /** Lo que la persona eligió: claro, oscuro o seguir al sistema. */
  readonly tema = this.elegido.asReadonly();

  /** Si el sistema pide oscuro. Se sigue en vivo, para que cambiar de tema en el sistema se note. */
  private readonly sistemaEnOscuro = signal(this.consultaDelSistema()?.matches ?? false);

  /** Si ahora mismo se está pintando en oscuro. */
  readonly enOscuro = computed(() =>
    this.elegido() === 'oscuro' || (this.elegido() === 'sistema' && this.sistemaEnOscuro()));

  constructor() {
    // El sistema puede cambiar mientras la aplicación está abierta —el modo nocturno automático
    // de Windows y macOS lo hace—, y con «sistema» elegido eso tiene que verse sin recargar.
    this.consultaDelSistema()?.addEventListener('change', e => this.sistemaEnOscuro.set(e.matches));

    effect(() => this.aplicar(this.enOscuro()));
  }

  elegir(tema: Tema): void {
    this.elegido.set(tema);

    try {
      this.documento.defaultView?.localStorage.setItem(CLAVE, tema);
    } catch {
      // Navegación privada o almacenamiento bloqueado: el tema sigue funcionando en esta
      // sesión, sólo que no se recuerda. Peor sería no dejar cambiarlo.
    }
  }

  private aplicar(oscuro: boolean): void {
    this.documento.documentElement.classList.toggle('dark', oscuro);
  }

  private leerGuardado(): Tema {
    try {
      const guardado = this.documento.defaultView?.localStorage.getItem(CLAVE);
      return guardado === 'claro' || guardado === 'oscuro' ? guardado : 'sistema';
    } catch {
      return 'sistema';
    }
  }

  private consultaDelSistema(): MediaQueryList | null {
    return this.documento.defaultView?.matchMedia?.('(prefers-color-scheme: dark)') ?? null;
  }
}
