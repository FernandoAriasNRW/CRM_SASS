/**
 * Los ejemplos que se enseñan para integrar la entrada de tickets.
 *
 * Funciones puras y aparte del componente para poder probarlas: un ejemplo que no funciona al
 * pegarlo es peor que ninguno, porque quien integra da por hecho que el fallo es suyo.
 */

/** La cabecera en la que viaja la clave. Tiene que coincidir con `TicketingEndpoints.CabeceraDeClave`. */
export const CABECERA_DE_CLAVE = 'X-Api-Key';

/** Lo que se pone en los ejemplos cuando no hay una clave recién creada a la vista. */
export const CLAVE_DE_EJEMPLO = 'tke_TU_CLAVE';

/** Los campos que la entrada exige. Tienen que coincidir con `CrearTicketExternoHandler`. */
export const CAMPOS_OBLIGATORIOS = [
  'title', 'description', 'requesterName', 'requesterEmail', 'requesterPhone', 'requesterCompany',
] as const;

/** Los que acepta si llegan. */
export const CAMPOS_OPCIONALES = [
  'attachments', 'classification', 'tags', 'teamId', 'status', 'priority',
] as const;

/** Cómo quedaría una cadena dentro de un literal de JavaScript entre comillas dobles. */
function comillas(texto: string): string {
  return JSON.stringify(texto);
}

/**
 * Un formulario HTML que se puede pegar en la web del cliente tal cual, con adjuntos.
 *
 * Envía el propio formulario como multipart con `fetch`, sin recargar la página: así los adjuntos
 * viajan sin código extra. No se pone `Content-Type` a mano porque el navegador tiene que añadir
 * el separador del multipart. La clave queda escrita en la página, así que cualquiera puede leerla:
 * está bien, porque sólo sirve para abrir tickets en esta organización, pero por eso conviene una
 * clave por sitio y revocarla si se abusa de ella.
 */
export function ejemploDeFormulario(url: string, clave: string): string {
  return [
    '<form id="soporte">',
    '  <input name="requesterName" placeholder="Nombre" required>',
    '  <input name="requesterEmail" type="email" placeholder="Email" required>',
    '  <input name="requesterPhone" type="tel" placeholder="Teléfono" required>',
    '  <input name="requesterCompany" placeholder="Empresa" required>',
    '  <input name="title" placeholder="Asunto" required minlength="5">',
    '  <textarea name="description" placeholder="Mensaje" required></textarea>',
    '  <input name="attachments" type="file" accept="image/*,video/*" multiple>',
    '  <button>Enviar</button>',
    '</form>',
    '<script>',
    '  document.getElementById("soporte").addEventListener("submit", async (evento) => {',
    '    evento.preventDefault();',
    '    const respuesta = await fetch(' + comillas(url) + ', {',
    '      method: "POST",',
    '      headers: { ' + comillas(CABECERA_DE_CLAVE) + ': ' + comillas(clave) + ' },',
    '      body: new FormData(evento.target)',
    '    });',
    '    alert(respuesta.ok ? "Hemos recibido tu solicitud" : "No se pudo enviar");',
    '  });',
    '</script>',
  ].join('\n');
}

/** La misma llamada desde un backend, en JSON y sin adjuntos. */
export function ejemploDeCurl(url: string, clave: string): string {
  const cuerpo = JSON.stringify({
    title: 'No puedo descargar la factura',
    description: 'Al pulsar en descargar no pasa nada',
    requesterName: 'Marta',
    requesterEmail: 'marta@cliente.com',
    requesterPhone: '+34 600 000 000',
    requesterCompany: 'Cliente S.L.',
    priority: 'High',
    classification: 'Facturación',
    tags: ['billing'],
  });

  return [
    'curl -X POST ' + url + ' \\',
    '  -H "Content-Type: application/json" \\',
    '  -H "' + CABECERA_DE_CLAVE + ': ' + clave + '" \\',
    "  -d '" + cuerpo + "'",
  ].join('\n');
}

/** Desde un backend con adjuntos: multipart, un -F por fichero. */
export function ejemploDeCurlConAdjuntos(url: string, clave: string): string {
  return [
    'curl -X POST ' + url + ' \\',
    '  -H "' + CABECERA_DE_CLAVE + ': ' + clave + '" \\',
    '  -F "title=No puedo descargar la factura" \\',
    '  -F "description=Al pulsar en descargar no pasa nada" \\',
    '  -F "requesterName=Marta" \\',
    '  -F "requesterEmail=marta@cliente.com" \\',
    '  -F "requesterPhone=+34 600 000 000" \\',
    '  -F "requesterCompany=Cliente S.L." \\',
    '  -F "attachments=@captura.png" \\',
    '  -F "attachments=@grabacion.mp4"',
  ].join('\n');
}
