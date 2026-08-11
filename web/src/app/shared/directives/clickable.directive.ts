import { Directive } from '@angular/core';

/**
 * Hace accesible por teclado un elemento no interactivo que lleva `(click)`.
 *
 * Un `<div (click)>` funciona con ratón y no existe para quien navega con teclado: no
 * recibe foco y no responde a Enter ni a Espacio. Lo correcto sería usar `<button>`, pero
 * en muchos de estos casos el elemento es una fila, una tarjeta arrastrable o una
 * etiqueta, donde el botón nativo trae consigo estilos y semántica que estorban.
 *
 * Esta directiva aporta lo que falta —rol, foco y activación por teclado— sin cambiar el
 * elemento. Reenvía la pulsación como un `click` real, de modo que el `(click)` que ya
 * está escrito en la plantilla es el único manejador: no hay que duplicar la lógica ni
 * mantener dos caminos que puedan divergir.
 *
 * Preferir `<button>` cuando el elemento sea de verdad un botón.
 */
@Directive({
  selector: '[uiClickable]',
  standalone: true,
  host: {
    'role': 'button',
    'tabindex': '0',
    '(keydown.enter)': 'activate($event)',
    '(keydown.space)': 'activate($event)',
  },
})
export class ClickableDirective {
  // El tipo es Event y no KeyboardEvent porque así es como Angular declara el argumento
  // de un binding de host; estrecharlo rompe la comprobación de plantillas.
  protected activate(event: Event): void {
    // Espacio desplaza la página por defecto; Enter puede enviar un formulario.
    event.preventDefault();

    // Un click sintético dispara el (click) de la plantilla. No hay recursión: click no
    // vuelve a producir keydown.
    (event.currentTarget as HTMLElement).click();
  }
}
