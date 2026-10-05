import { currentLanguage, urlInLanguage, LANGUAGES } from './language';

/**
 * El idioma se deduce del `<base href>` que escribe la compilación, no del navegador: el
 * navegador dice qué prefiere el usuario, no qué está viendo. Confundir ambas cosas hace
 * que el selector ofrezca cambiar al idioma que ya está puesto.
 */
describe('language', () => {
  function docWith(baseHref: string | null): Document {
    return {
      querySelector: () => (baseHref === null ? null : { getAttribute: () => baseHref }),
    } as unknown as Document;
  }

  describe('currentLanguage', () => {
    it('reads English from the prefix', () => {
      expect(currentLanguage(docWith('/en/'))).toBe('en');
    });

    it('reads Spanish from the prefix', () => {
      expect(currentLanguage(docWith('/es/'))).toBe('es');
    });

    it('without a prefix it assumes the source language', () => {
      // Es el caso del servidor de desarrollo, que sirve sin prefijo de idioma.
      expect(currentLanguage(docWith('/'))).toBe('es');
    });

    it('without a base tag it does not fail either', () => {
      expect(currentLanguage(docWith(null))).toBe('es');
    });
  });

  describe('urlInLanguage', () => {
    it('keeps the path when switching language', () => {
      // Cambiar de idioma desde una pantalla concreta no debe devolver al inicio.
      expect(urlInLanguage('en', { pathname: '/es/tasks', search: '' })).toBe('/en/tasks');
    });

    it('keeps the query parameters', () => {
      expect(urlInLanguage('en', { pathname: '/es/tasks', search: '?filter=mine' }))
        .toBe('/en/tasks?filter=mine');
    });

    it('adds the prefix when there is none', () => {
      expect(urlInLanguage('en', { pathname: '/tasks', search: '' })).toBe('/en/tasks');
    });

    it('works from the root', () => {
      expect(urlInLanguage('en', { pathname: '/es/', search: '' })).toBe('/en/');
    });

    it('does not duplicate the prefix when repeating the language', () => {
      expect(urlInLanguage('es', { pathname: '/es/projects', search: '' })).toBe('/es/projects');
    });
  });

  it('both declared languages have a code and a name', () => {
    expect(LANGUAGES.map(i => i.code)).toEqual(['es', 'en']);
    expect(LANGUAGES.every(i => i.name.length > 0)).toBeTrue();
  });
});
