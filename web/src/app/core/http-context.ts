import { HttpContext, HttpContextToken } from '@angular/common/http';

/**
 * Marca una petición como «yo explico mi propio error».
 *
 * El interceptor de errores levanta un aviso por cada fallo, y eso está bien para lo que nadie
 * más va a explicar. Pero cuando el componente pone el mensaje junto al campo que lo provocó, o
 * en un aviso con el nombre de la tarea que se revirtió, el del interceptor es **el mismo texto
 * repetido**: dos avisos para un solo fallo, y el segundo no añade nada.
 *
 * Se resuelve con el contexto de la propia petición y no con una bandera en el servicio porque
 * es información de esa llamada concreta, no del servicio entero: la misma pantalla puede querer
 * explicar un error y dejar que el interceptor explique otro.
 */
export const SIN_AVISO_AUTOMATICO = new HttpContextToken<boolean>(() => false);

/** El contexto que se le pasa a una petición que se encarga de contar su propio error. */
export function sinAvisoAutomatico(): HttpContext {
  return new HttpContext().set(SIN_AVISO_AUTOMATICO, true);
}
