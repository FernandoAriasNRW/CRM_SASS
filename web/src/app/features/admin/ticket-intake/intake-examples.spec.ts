import {
  API_KEY_HEADER, REQUIRED_FIELDS, curlExample, curlWithAttachmentsExample, formExample,
} from './intake-examples';

describe('ticket intake examples', () => {
  const url = 'https://api.ejemplo.com/api/v1/ticket-intake';

  it('the form calls the real URL with the key in its header', () => {
    const html = formExample(url, 'tke_abc"123');

    expect(html).toContain(`fetch("${url}"`);
    expect(html).toContain(`"${API_KEY_HEADER}": "tke_abc\\"123"`);
  });

  it('the form asks for the six required fields and accepts several image or video attachments', () => {
    const html = formExample(url, 'tke_x');

    for (const field of REQUIRED_FIELDS) {
      expect(html).toMatch(new RegExp(`name="${field}"[^>]*required`));
    }
    expect(html).toContain('name="attachments" type="file" accept="image/*,video/*" multiple');
  });

  it('the form lets the browser set the multipart Content-Type', () => {
    const html = formExample(url, 'tke_x');

    expect(html).toContain('body: new FormData(evento.target)');
    expect(html).not.toContain('Content-Type');
  });

  it('the backend example sends a JSON with all required fields', () => {
    const curl = curlExample(url, 'tke_x');
    const body = JSON.parse(curl.slice(curl.indexOf("-d '") + 4, curl.lastIndexOf("'")));

    expect(curl).toContain(`curl -X POST ${url}`);
    expect(curl).toContain(`${API_KEY_HEADER}: tke_x`);
    for (const field of REQUIRED_FIELDS) {
      expect(body[field]).toBeTruthy();
    }
  });

  it('the attachments example sends the required fields and one -F per file', () => {
    const curl = curlWithAttachmentsExample(url, 'tke_x');

    for (const field of REQUIRED_FIELDS) {
      expect(curl).toContain(`-F "${field}=`);
    }
    expect(curl.match(/-F "attachments=@/g)?.length).toBe(2);
  });
});
