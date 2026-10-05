import { listQueryParams } from './list-query';

describe('listQueryParams', () => {
  it('uses the names the API binds: page and search', () => {
    const params = listQueryParams({
      page: 3, pageSize: 25, sortColumn: 'title', sortDirection: 'desc', searchTerm: 'factura',
    });

    expect(params).toEqual({
      page: 3, pageSize: 25, sortColumn: 'title', sortDirection: 'desc', search: 'factura',
    });
  });

  it('does not send the table state names, which the API ignores', () => {
    const params = listQueryParams({ page: 2, pageSize: 10, searchTerm: 'x' });

    expect(params['pageNumber']).toBeUndefined();
    expect(params['searchTerm']).toBeUndefined();
  });
});
