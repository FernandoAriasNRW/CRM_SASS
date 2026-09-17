import {
  CABECERA_DE_CLAVE, CAMPOS_OBLIGATORIOS, ejemploDeCurl, ejemploDeCurlConAdjuntos, ejemploDeFormulario,
} from './ejemplos-de-entrada';

describe('ejemplos de la entrada de tickets', () => {
  const url = 'https://api.ejemplo.com/api/v1/entrada/tickets';

  it('el formulario llama a la URL real con la clave en su cabecera', () => {
    const html = ejemploDeFormulario(url, 'tke_abc"123');

    expect(html).toContain(`fetch("${url}"`);
    expect(html).toContain(`"${CABECERA_DE_CLAVE}": "tke_abc\\"123"`);
  });

  it('el formulario pide los seis obligatorios y admite varios adjuntos de imagen o vídeo', () => {
    const html = ejemploDeFormulario(url, 'tke_x');

    for (const campo of CAMPOS_OBLIGATORIOS) {
      expect(html).toMatch(new RegExp(`name="${campo}"[^>]*required`));
    }
    expect(html).toContain('name="attachments" type="file" accept="image/*,video/*" multiple');
  });

  it('el formulario deja al navegador poner el Content-Type del multipart', () => {
    const html = ejemploDeFormulario(url, 'tke_x');

    expect(html).toContain('body: new FormData(evento.target)');
    expect(html).not.toContain('Content-Type');
  });

  it('el ejemplo de backend manda un JSON con todos los obligatorios', () => {
    const curl = ejemploDeCurl(url, 'tke_x');
    const cuerpo = JSON.parse(curl.slice(curl.indexOf("-d '") + 4, curl.lastIndexOf("'")));

    expect(curl).toContain(`curl -X POST ${url}`);
    expect(curl).toContain(`${CABECERA_DE_CLAVE}: tke_x`);
    for (const campo of CAMPOS_OBLIGATORIOS) {
      expect(cuerpo[campo]).toBeTruthy();
    }
  });

  it('el ejemplo con adjuntos manda los obligatorios y un -F por fichero', () => {
    const curl = ejemploDeCurlConAdjuntos(url, 'tke_x');

    for (const campo of CAMPOS_OBLIGATORIOS) {
      expect(curl).toContain(`-F "${campo}=`);
    }
    expect(curl.match(/-F "attachments=@/g)?.length).toBe(2);
  });
});
