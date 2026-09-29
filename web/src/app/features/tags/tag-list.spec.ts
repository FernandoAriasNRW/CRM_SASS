import { filterRows, sortRows, tagKind, toRows, visibleRows } from './tag-list';
import { type TagItem } from '../../shared/services/tags.service';

describe('tag-list', () => {
  const tag = (over: Partial<TagItem>): TagItem => ({
    id: over.name ?? 'x', name: 'x', colorHex: '#000000', category: 'Business', categoryLabel: 'Negocio', ...over,
  });

  const rows = toRows([
    tag({ name: 'Socio', builtInKey: 'partner' }),
    tag({ name: 'Portal web', category: 'Project', categoryLabel: 'Proyecto' }),
    tag({ name: 'Clientes grandes', category: 'Clientes', categoryLabel: 'Clientes', createdBy: 'u1' }),
    tag({ name: 'Implementación', category: 'DevelopmentPhase', categoryLabel: 'Fase de desarrollo', builtInKey: 'implementation' }),
  ]);

  it('distingue predefinidas, automáticas y propias', () => {
    expect(rows.map(r => r.kind)).toEqual(['builtIn', 'automatic', 'custom', 'builtIn']);
    // Una de proyecto es automática aunque no tenga clave; la categoría manda.
    expect(tagKind(tag({ category: 'Team', builtInKey: null }))).toBe('automatic');
  });

  it('busca en nombre y categoría sin distinguir tildes ni mayúsculas', () => {
    expect(filterRows(rows, 'IMPLEMENTACION').map(r => r.name)).toEqual(['Implementación']);
    expect(filterRows(rows, 'negocio').map(r => r.name)).toEqual(['Socio']);
    expect(filterRows(rows, '  ').length).toBe(4);
  });

  it('ordena por la columna pedida y desempata por nombre', () => {
    expect(sortRows(rows, 'name', 'desc').map(r => r.name)).toEqual(['Socio', 'Portal web', 'Implementación', 'Clientes grandes']);
    expect(sortRows(rows, 'kind').map(r => r.name)).toEqual(['Portal web', 'Implementación', 'Socio', 'Clientes grandes']);
    expect(sortRows(rows, 'desconocida').map(r => r.name)).toEqual(rows.map(r => r.name));
  });

  it('pagina después de filtrar y cuenta el total filtrado', () => {
    const { page, total } = visibleRows(rows, { page: 2, pageSize: 1, searchTerm: 'o', sortColumn: 'name', sortDirection: 'asc' });
    expect(total).toBe(3);
    expect(page.map(r => r.name)).toEqual(['Portal web']);
  });
});
