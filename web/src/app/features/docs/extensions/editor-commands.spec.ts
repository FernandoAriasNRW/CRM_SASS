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
describe('matchingCommands', () => {
  const keys = (query: string) => matchingCommands(query).map(c => c.key);

  it('with nothing typed it offers every command valid at the cursor', () => {
    const contextual = EDITOR_COMMANDS.filter(c => c.onlyInside).length;

    expect(matchingCommands('').length).toBe(EDITOR_COMMANDS.length - contextual);
    expect(matchingCommands('', { insideColumns: true }).length).toBe(EDITOR_COMMANDS.length);
  });

  it('«Undo columns» only appears with the cursor inside columns', () => {
    // Fuera de unas columnas no hace nada: ofrecerlo sería poner en el menú un botón que no
    // responde, que es el tipo de fallo que esta tanda ha ido quitando.
    expect(keys('columnas')).not.toContain('columns-unset');
    expect(matchingCommands('columnas', { insideColumns: true }).map(c => c.key))
      .toContain('columns-unset');
  });

  it('finds by the Spanish title', () => {
    expect(keys('lista')).toContain('bullets');
    expect(keys('lista')).toContain('numbered');
    expect(keys('lista')).toContain('tasks');
  });

  it('finds by the middle of the title, not only the start', () => {
    // «Lista con viñetas» no empieza por «viñetas»: con `startsWith` esto no devolvía nada.
    expect(keys('viñetas')).toContain('bullets');
  });

  it('is not affected by accents or case', () => {
    expect(keys('VIDEO')).toContain('video');
    expect(keys('vídeo')).toContain('video');
    expect(keys('codigo')).toContain('code');
    expect(keys('CÓDIGO')).toContain('code');
  });

  it('still finds by the English name, which many people type', () => {
    expect(keys('list')).toContain('bullets');
    expect(keys('table')).toContain('table');
    expect(keys('quote')).toContain('quote');
  });

  it('understands heading shortcuts', () => {
    expect(keys('h1')).toEqual(['h1']);
    expect(keys('h2')).toEqual(['h2']);
  });

  it('returns nothing when there really is nothing', () => {
    expect(matchingCommands('xyzzy')).toEqual([]);
  });

  it('every command has a title, description, icon and group', () => {
    for (const command of EDITOR_COMMANDS) {
      expect(command.title.length).toBeGreaterThan(0);
      expect(command.description.length).toBeGreaterThan(0);
      expect(command.group.length).toBeGreaterThan(0);
      // El icono es el marcado SVG que exporta @ng-icons: si llegara vacío, el menú pintaría un
      // hueco, que es el mismo fallo que tuvo el paginado.
      expect(command.icon).toContain('<svg');
    }
  });

  it('commands are grouped, not mixed', () => {
    // El desplegable pinta la cabecera del grupo al cambiar de grupo. Si el catálogo alternara
    // grupos, la misma cabecera saldría varias veces.
    const groups = EDITOR_COMMANDS.map(c => c.group);
    const withoutConsecutiveRepeats = groups.filter((g, i) => i === 0 || g !== groups[i - 1]);

    expect(withoutConsecutiveRepeats.length).toBe(new Set(groups).size);
  });
});
