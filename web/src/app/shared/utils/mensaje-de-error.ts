/**
 * Saca de una respuesta fallida la frase que explica por qué el servidor la rechazó.
 *
 * Esta API rechaza de tres formas distintas y hay que contemplar las tres, porque mirar sólo una
 * convierte el resto en un mensaje genérico y se pierde la explicación del dominio, que es
 * justo lo único que sirve de algo:
 *
 * - `BadRequest(result.Error)` manda **una cadena suelta** —así rechazan campos personalizados,
 *   tareas y casi todos los módulos—.
 * - El manejador global de excepciones manda un **ProblemDetails**, y el motivo va en `detail`.
 *   Si es de validación, `detail` es genérico («Uno o más campos…») y el motivo de verdad va en
 *   `errors`, por campo: se devuelve el primero.
 * - Algunos endpoints antiguos mandan un objeto con `message`.
 *
 * Lo que **nunca** se devuelve es `error.message` de Angular: es la cadena «Http failure response
 * for http://…: 400 Bad Request», que enseña la dirección interna de la API y no dice nada que
 * quien la lee pueda usar.
 */
export function mensajeDeError(respuesta: unknown, porDefecto: string): string {
  const cuerpo = (respuesta as { error?: unknown })?.error;

  if (typeof cuerpo === 'string' && cuerpo.trim()) return cuerpo;

  const objeto = cuerpo as { detail?: string; message?: string; errors?: Record<string, unknown> } | undefined;

  const primero = Object.values(objeto?.errors ?? {})
    .flatMap(mensajes => (Array.isArray(mensajes) ? mensajes : []))
    .find((m): m is string => typeof m === 'string' && m.trim().length > 0);
  if (primero) return primero;

  if (objeto?.detail?.trim()) return objeto.detail;
  if (objeto?.message?.trim()) return objeto.message;

  return porDefecto;
}
