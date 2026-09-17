import {
  API_KEY_HEADER, REQUIRED_FIELDS, curlExample, curlWithAttachmentsExample, formExample,
} from './intake-examples';

describe('ejemplos de la entrada de tickets', () => {
  const url = 'https://api.ejemplo.com/api/v1/ticket-intake';

  it('el formulario llama a la URL real con la clave en su cabecera', () => {
    const html = formExample(url, 'tke_abc"123');

    expect(html).toContain(`fetch("${url}"`);
    expect(html).toContain(`"${API_KEY_HEADER}": "tke_abc\\"123"`);
  });

  it('el formulario pide los seis obligatorios y admite varios adjuntos de imagen o vídeo', () => {
    const html = formExample(url, 'tke_x');

    for (const field of REQUIRED_FIELDS) {
      expect(html).toMatch(new RegExp(`name="${field}"[^>]*required`));
    }
    expect(html).toContain('name="attachments" type="file" accept="image/*,video/*" multiple');
  });

  it('el formulario deja al navegador poner el Content-Type del multipart', () => {
    const html = formExample(url, 'tke_x');

    expect(html).toContain('body: new FormData(evento.target)');
    expect(html).not.toContain('Content-Type');
  });

  it('el ejemplo de backend manda un JSON con todos los obligatorios', () => {
    const curl = curlExample(url, 'tke_x');
    const body = JSON.parse(curl.slice(curl.indexOf("-d '") + 4, curl.lastIndexOf("'")));

    expect(curl).toContain(`curl -X POST ${url}`);
    expect(curl).toContain(`${API_KEY_HEADER}: tke_x`);
    for (const field of REQUIRED_FIELDS) {
      expect(body[field]).toBeTruthy();
    }
  });

  it('el ejemplo con adjuntos manda los obligatorios y un -F por fichero', () => {
    const curl = curlWithAttachmentsExample(url, 'tke_x');

    for (const field of REQUIRED_FIELDS) {
      expect(curl).toContain(`-F "${field}=`);
    }
    expect(curl.match(/-F "attachments=@/g)?.length).toBe(2);
  });
});
