import { COMANDOS_DEL_EDITOR, comandosQueCasan } from './comandos-del-editor';

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
  const claves = (consulta: string) => comandosQueCasan(consulta).map(c => c.clave);

  it('sin nada escrito ofrece todos los comandos que valen donde está el cursor', () => {
    const contextuales = COMANDOS_DEL_EDITOR.filter(c => c.soloDentroDe).length;

    expect(comandosQueCasan('').length).toBe(COMANDOS_DEL_EDITOR.length - contextuales);
    expect(comandosQueCasan('', { dentroDeColumnas: true }).length).toBe(COMANDOS_DEL_EDITOR.length);
  });

  it('«Deshacer columnas» sólo aparece con el cursor dentro de unas columnas', () => {
    // Fuera de unas columnas no hace nada: ofrecerlo sería poner en el menú un botón que no
    // responde, que es el tipo de fallo que esta tanda ha ido quitando.
    expect(claves('columnas')).not.toContain('columnas-deshacer');
    expect(comandosQueCasan('columnas', { dentroDeColumnas: true }).map(c => c.clave))
      .toContain('columnas-deshacer');
  });

  it('encuentra por el título en español', () => {
    expect(claves('lista')).toContain('vinetas');
    expect(claves('lista')).toContain('numerada');
    expect(claves('lista')).toContain('tareas');
  });

  it('encuentra por el medio del título, no sólo por el principio', () => {
    // «Lista con viñetas» no empieza por «viñetas»: con `startsWith` esto no devolvía nada.
    expect(claves('viñetas')).toContain('vinetas');
  });

  it('no le afectan los acentos ni las mayúsculas', () => {
    expect(claves('VIDEO')).toContain('video');
    expect(claves('vídeo')).toContain('video');
    expect(claves('codigo')).toContain('codigo');
    expect(claves('CÓDIGO')).toContain('codigo');
  });

  it('sigue encontrando por el nombre en inglés, que es lo que mucha gente escribe', () => {
    expect(claves('list')).toContain('vinetas');
    expect(claves('table')).toContain('tabla');
    expect(claves('quote')).toContain('cita');
  });

  it('entiende los atajos de encabezado', () => {
    expect(claves('h1')).toEqual(['h1']);
    expect(claves('h2')).toEqual(['h2']);
  });

  it('no devuelve nada cuando de verdad no hay nada', () => {
    expect(comandosQueCasan('xyzzy')).toEqual([]);
  });

  it('todos los comandos tienen título, descripción, icono y grupo', () => {
    for (const comando of COMANDOS_DEL_EDITOR) {
      expect(comando.titulo.length).toBeGreaterThan(0);
      expect(comando.descripcion.length).toBeGreaterThan(0);
      expect(comando.grupo.length).toBeGreaterThan(0);
      // El icono es el marcado SVG que exporta @ng-icons: si llegara vacío, el menú pintaría un
      // hueco, que es el mismo fallo que tuvo el paginado.
      expect(comando.icono).toContain('<svg');
    }
  });

  it('los comandos van agrupados y no mezclados', () => {
    // El desplegable pinta la cabecera del grupo al cambiar de grupo. Si el catálogo alternara
    // grupos, la misma cabecera saldría varias veces.
    const grupos = COMANDOS_DEL_EDITOR.map(c => c.grupo);
    const sinRepetirSeguidos = grupos.filter((g, i) => i === 0 || g !== grupos[i - 1]);

    expect(sinRepetirSeguidos.length).toBe(new Set(grupos).size);
  });
});
