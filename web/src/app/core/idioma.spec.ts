import { idiomaActual, urlEnIdioma, IDIOMAS } from './idioma';

/**
 * El idioma se deduce del `<base href>` que escribe la compilación, no del navegador: el
 * navegador dice qué prefiere el usuario, no qué está viendo. Confundir ambas cosas hace
 * que el selector ofrezca cambiar al idioma que ya está puesto.
 */
describe('idioma', () => {
  function documentoCon(baseHref: string | null): Document {
    return {
      querySelector: () => (baseHref === null ? null : { getAttribute: () => baseHref }),
    } as unknown as Document;
  }

  describe('idiomaActual', () => {
    it('lee inglés del prefijo', () => {
      expect(idiomaActual(documentoCon('/en/'))).toBe('en');
    });

    it('lee español del prefijo', () => {
      expect(idiomaActual(documentoCon('/es/'))).toBe('es');
    });

    it('sin prefijo asume el idioma de origen', () => {
      // Es el caso del servidor de desarrollo, que sirve sin prefijo de idioma.
      expect(idiomaActual(documentoCon('/'))).toBe('es');
    });

    it('sin etiqueta base tampoco falla', () => {
      expect(idiomaActual(documentoCon(null))).toBe('es');
    });
  });

  describe('urlEnIdioma', () => {
    it('conserva la ruta al cambiar de idioma', () => {
      // Cambiar de idioma desde una pantalla concreta no debe devolver al inicio.
      expect(urlEnIdioma('en', { pathname: '/es/tasks', search: '' })).toBe('/en/tasks');
    });

    it('conserva los parámetros de consulta', () => {
      expect(urlEnIdioma('en', { pathname: '/es/tasks', search: '?filter=mine' }))
        .toBe('/en/tasks?filter=mine');
    });

    it('añade el prefijo cuando no lo hay', () => {
      expect(urlEnIdioma('en', { pathname: '/tasks', search: '' })).toBe('/en/tasks');
    });

    it('funciona desde la raíz', () => {
      expect(urlEnIdioma('en', { pathname: '/es/', search: '' })).toBe('/en/');
    });

    it('no duplica el prefijo al repetir idioma', () => {
      expect(urlEnIdioma('es', { pathname: '/es/projects', search: '' })).toBe('/es/projects');
    });
  });

  it('los dos idiomas declarados tienen código y nombre', () => {
    expect(IDIOMAS.map(i => i.codigo)).toEqual(['es', 'en']);
    expect(IDIOMAS.every(i => i.nombre.length > 0)).toBeTrue();
  });
});
