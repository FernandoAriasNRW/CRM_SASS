/**
 * Las entradas del panel de navegación, y a qué filtro del servidor corresponde cada una.
 *
 * **Aquí sólo hay entradas que filtran de verdad.** Es la regla que da sentido a toda la fase
 * 5A, y viene de un fallo real: el menú ofrecía «Mis Tickets» apuntando a `?filter=mine`, el
 * parámetro llegaba al servidor, nadie lo leía y la pantalla devolvía los 175 tickets de
 * siempre. Quien lo usaba creía estar viendo los suyos.
 *
 * Un menú que promete y devuelve la misma lista es peor que un menú corto. Así que antes de
 * añadir una entrada aquí, el filtro tiene que existir en el backend y tener una prueba de
 * integración que compruebe que **devuelve algo distinto de no filtrar**.
 *
 * Los nombres de los filtros son los de `FiltrosDeVista` en el servidor. No se escriben sueltos
 * en cada componente por la misma razón: dos copias acaban divergiendo en una letra y el fallo
 * no da error, sólo devuelve la lista entera.
 */
export interface EntradaDeMenu {
  /** Lo que se lee en pantalla. */
  readonly etiqueta: string;

  /** El icono de lucide. */
  readonly icono: string;

  /**
   * El valor de `?filter=`, o `null` para «ver todo», que es la ausencia de filtro.
   *
   * Se distingue `null` de la cadena vacía a propósito: la vista quita el parámetro de la URL
   * en lugar de mandarlo vacío, y así la URL de «ver todo» es la limpia de siempre.
   */
  readonly filtro: string | null;

  /** Separador visual antes de esta entrada. */
  readonly separadorAntes?: boolean;
}

/** Los nombres tal cual los entiende el servidor. Ver `FiltrosDeVista` en BuildingBlocks. */
export const FILTROS = {
  mios: 'mine',
  creadosPorMi: 'created',
  favoritos: 'favorites',
  compartidosConmigo: 'shared',
  privados: 'private',
  archivados: 'archived',
  papelera: 'trash'
} as const;

/**
 * Las entradas que valen para cualquier módulo.
 *
 * Cada módulo interpreta «mío» a su manera —responsable de la tarea, dueño del proyecto, agente
 * del ticket— y eso es correcto: el nombre es común, el significado lo pone el dominio.
 */
export const ENTRADAS_TRANSVERSALES: readonly EntradaDeMenu[] = [
  { etiqueta: $localize`Ver todo`, icono: 'lucideList', filtro: null },
  { etiqueta: $localize`Asignado a mí`, icono: 'lucideUser', filtro: FILTROS.mios },
  { etiqueta: $localize`Creado por mí`, icono: 'lucidePenLine', filtro: FILTROS.creadosPorMi },
  { etiqueta: $localize`Favoritos`, icono: 'lucideStar', filtro: FILTROS.favoritos },

  { etiqueta: $localize`Compartido conmigo`, icono: 'lucideShare2', filtro: FILTROS.compartidosConmigo, separadorAntes: true },
  { etiqueta: $localize`Privado`, icono: 'lucideLock', filtro: FILTROS.privados },

  // Archivo y papelera van al final y separados: son las dos entradas que enseñan cosas que el
  // resto de la aplicación esconde, y conviene que se note que se está saliendo de la vista
  // normal.
  { etiqueta: $localize`Archivado`, icono: 'lucideArchive', filtro: FILTROS.archivados, separadorAntes: true },
  { etiqueta: $localize`Papelera`, icono: 'lucideTrash2', filtro: FILTROS.papelera }
];

/**
 * Lo que se pinta en la cabecera del panel de cada módulo.
 *
 * El plan de la Fase 5 listaba además entradas propias de cada módulo —«Vencen esta semana»,
 * «Sin asignar», «Vencidos de SLA», «En riesgo»—. **No están aquí porque el servidor todavía no
 * las sabe filtrar.** Cuando existan, se añaden; hasta entonces serían justo lo que esta fase
 * viene a quitar.
 */
export interface VocabularioDeModulo {
  readonly titulo: string;
  readonly inicial: string;
  readonly entradas: readonly EntradaDeMenu[];
}

export const VOCABULARIO: Readonly<Record<string, VocabularioDeModulo>> = {
  tasks: { titulo: $localize`Tareas`, inicial: 'T', entradas: ENTRADAS_TRANSVERSALES },
  tickets: { titulo: $localize`Tickets`, inicial: 'S', entradas: ENTRADAS_TRANSVERSALES },
  projects: { titulo: $localize`Proyectos`, inicial: 'P', entradas: ENTRADAS_TRANSVERSALES }
};
