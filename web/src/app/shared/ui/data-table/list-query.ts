import { TableState } from './data-table.component';

/**
 * Los parámetros de consulta de una lista paginada de la API, sacados del estado de la tabla.
 *
 * Los nombres son los que enlazan los endpoints (`page`, `search`), no los del estado de la
 * tabla. Cada pantalla los escribía a mano como `pageNumber` y `searchTerm`: la API los ignoraba
 * sin dar ningún error, así que pasar de página devolvía siempre la primera y el buscador no
 * filtraba nada. Estar en un solo sitio, con su prueba, es lo que evita que vuelva a pasar.
 */
export function listQueryParams(state: TableState): Record<string, string | number | undefined> {
  return {
    page: state.page,
    pageSize: state.pageSize,
    sortColumn: state.sortColumn,
    sortDirection: state.sortDirection,
    search: state.searchTerm,
  };
}
