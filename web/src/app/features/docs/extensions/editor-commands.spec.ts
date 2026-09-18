import { EDITOR_COMMANDS, matchingCommands } from './editor-commands';

/**
 * El filtro del menú `/`.
 *
 * El anterior era `title.toLowerCase().startsWith(query)` sobre unos títulos escritos en inglés,
 * así que casi ninguna búsqueda encontraba nada: `/lista` no daba resultados, `/list` tampoco
 * encontraba «Bullet List» —no empieza por «list»— y `/h1` tampoco. Sólo acertaba quien supiera de
 * memoria la primera palabra en inglés de cada comando.
 *
 * Se prueba aquí y no a través del editor porque es una función pura: montar TipTap para
 * comprobar una búsqueda de texto haría la prueba lenta y frágil sin comprobar nada más.
 */
describe('comandosQueCasan', () => {
  const keys = (query: string) => matchingCommands(query).map(c => c.key);

  it('sin nada escrito ofrece todos los comandos que valen donde está el cursor', () => {
    const contextual = EDITOR_COMMANDS.filter(c => c.onlyInside).length;

    expect(matchingCommands('').length).toBe(EDITOR_COMMANDS.length - contextual);
    expect(matchingCommands('', { insideColumns: true }).length).toBe(EDITOR_COMMANDS.length);
  });

  it('«Deshacer columnas» sólo aparece con el cursor dentro de unas columnas', () => {
    // Fuera de unas columnas no hace nada: ofrecerlo sería poner en el menú un botón que no
    // responde, que es el tipo de fallo que esta tanda ha ido quitando.
    expect(keys('columnas')).not.toContain('columns-unset');
    expect(matchingCommands('columnas', { insideColumns: true }).map(c => c.key))
      .toContain('columns-unset');
  });

  it('encuentra por el título en español', () => {
    expect(keys('lista')).toContain('bullets');
    expect(keys('lista')).toContain('numbered');
    expect(keys('lista')).toContain('tasks');
  });

  it('encuentra por el medio del título, no sólo por el principio', () => {
    // «Lista con viñetas» no empieza por «viñetas»: con `startsWith` esto no devolvía nada.
    expect(keys('viñetas')).toContain('bullets');
  });

  it('no le afectan los acentos ni las mayúsculas', () => {
    expect(keys('VIDEO')).toContain('video');
    expect(keys('vídeo')).toContain('video');
    expect(keys('codigo')).toContain('code');
    expect(keys('CÓDIGO')).toContain('code');
  });

  it('sigue encontrando por el nombre en inglés, que es lo que mucha gente escribe', () => {
    expect(keys('list')).toContain('bullets');
    expect(keys('table')).toContain('table');
    expect(keys('quote')).toContain('quote');
  });

  it('entiende los atajos de encabezado', () => {
    expect(keys('h1')).toEqual(['h1']);
    expect(keys('h2')).toEqual(['h2']);
  });

  it('no devuelve nada cuando de verdad no hay nada', () => {
    expect(matchingCommands('xyzzy')).toEqual([]);
  });

  it('todos los comandos tienen título, descripción, icono y grupo', () => {
    for (const command of EDITOR_COMMANDS) {
      expect(command.title.length).toBeGreaterThan(0);
      expect(command.description.length).toBeGreaterThan(0);
      expect(command.group.length).toBeGreaterThan(0);
      // El icono es el marcado SVG que exporta @ng-icons: si llegara vacío, el menú pintaría un
      // hueco, que es el mismo fallo que tuvo el paginado.
      expect(command.icon).toContain('<svg');
    }
  });

  it('los comandos van agrupados y no mezclados', () => {
    // El desplegable pinta la cabecera del grupo al cambiar de grupo. Si el catálogo alternara
    // grupos, la misma cabecera saldría varias veces.
    const groups = EDITOR_COMMANDS.map(c => c.group);
    const withoutConsecutiveRepeats = groups.filter((g, i) => i === 0 || g !== groups[i - 1]);

    expect(withoutConsecutiveRepeats.length).toBe(new Set(groups).size);
  });
});
