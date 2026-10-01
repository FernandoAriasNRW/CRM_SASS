import { currentLanguage, urlInLanguage, LANGUAGES } from './language';

/**
 * El idioma se deduce del `<base href>` que escribe la compilación, no del navegador: el
 * navegador dice qué prefiere el usuario, no qué está viendo. Confundir ambas cosas hace
 * que el selector ofrezca cambiar al idioma que ya está puesto.
 */
describe('idioma', () => {
  function docWith(baseHref: string | null): Document {
    return {
      querySelector: () => (baseHref === null ? null : { getAttribute: () => baseHref }),
    } as unknown as Document;
  }

  describe('currentLanguage', () => {
    it('lee inglés del prefijo', () => {
      expect(currentLanguage(docWith('/en/'))).toBe('en');
    });

    it('lee español del prefijo', () => {
      expect(currentLanguage(docWith('/es/'))).toBe('es');
    });

    it('sin prefijo asume el idioma de origen', () => {
      // Es el caso del servidor de desarrollo, que sirve sin prefijo de idioma.
      expect(currentLanguage(docWith('/'))).toBe('es');
    });

    it('sin etiqueta base tampoco falla', () => {
      expect(currentLanguage(docWith(null))).toBe('es');
    });
  });

  describe('urlInLanguage', () => {
    it('conserva la ruta al cambiar de idioma', () => {
      // Cambiar de idioma desde una pantalla concreta no debe devolver al inicio.
      expect(urlInLanguage('en', { pathname: '/es/tasks', search: '' })).toBe('/en/tasks');
    });

    it('conserva los parámetros de consulta', () => {
      expect(urlInLanguage('en', { pathname: '/es/tasks', search: '?filter=mine' }))
        .toBe('/en/tasks?filter=mine');
    });

    it('añade el prefijo cuando no lo hay', () => {
      expect(urlInLanguage('en', { pathname: '/tasks', search: '' })).toBe('/en/tasks');
    });

    it('funciona desde la raíz', () => {
      expect(urlInLanguage('en', { pathname: '/es/', search: '' })).toBe('/en/');
    });

    it('no duplica el prefijo al repetir idioma', () => {
      expect(urlInLanguage('es', { pathname: '/es/projects', search: '' })).toBe('/es/projects');
    });
  });

  it('los dos idiomas declarados tienen código y nombre', () => {
    expect(LANGUAGES.map(i => i.code)).toEqual(['es', 'en']);
    expect(LANGUAGES.every(i => i.name.length > 0)).toBeTrue();
  });
});
