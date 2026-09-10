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

  /**
   * A dónde lleva, si no es a la lista del propio módulo.
   *
   * Existe porque no todos los submenús son filtros sobre la misma lista: el de Inicio lleva a
   * las tareas y a los tickets de uno, y el de Documentos cambia de pestaña con `?tab=`. Sin
   * esto habría que mantener dos mecanismos —el panel para unos módulos y el desplegable viejo
   * para otros—, que es justo lo que se acaba de quitar.
   */
  readonly ruta?: string;

  /** Parámetros extra de la URL, para los submenús que no filtran con `?filter=`. */
  readonly params?: Readonly<Record<string, string>>;

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

/**
 * El submenú de Documentos.
 *
 * Vivía dentro de la pantalla de Docs, con su propio estado interno (`activeSidebarTab`). Se pasa
 * a `?tab=` por dos razones: para que sea el mismo panel que el resto —Docs era el único módulo
 * con submenú propio, y esa excepción es la que hacía que la barra lateral se comportara distinto
 * según dónde estuvieras— y porque el estado en la URL se puede compartir por enlace y responde al
 * botón de atrás.
 */
export const ENTRADAS_DE_DOCUMENTOS: readonly EntradaDeMenu[] = [
  { etiqueta: $localize`Todos los documentos`, icono: 'lucideFileText', filtro: null },
  { etiqueta: $localize`Mis documentos`, icono: 'lucideUser', filtro: null, params: { tab: 'my' } },
  { etiqueta: $localize`Compartidos conmigo`, icono: 'lucideShare2', filtro: null, params: { tab: 'shared' } },
  { etiqueta: $localize`Privados`, icono: 'lucideLock', filtro: null, params: { tab: 'private' } },
  { etiqueta: $localize`Actas de reunión`, icono: 'lucideCalendarDays', filtro: null, params: { tab: 'meeting-notes' } },
  { etiqueta: $localize`Archivados`, icono: 'lucideArchive', filtro: null, params: { tab: 'archived' }, separadorAntes: true }
];

/**
 * El de Inicio: atajos a lo de uno en cada módulo.
 *
 * No filtra nada propio —Inicio no es una lista— así que cada entrada lleva a otro sitio. Son las
 * tres que ya ofrecía el desplegable viejo y que **sí** funcionaban.
 */
export const ENTRADAS_DE_INICIO: readonly EntradaDeMenu[] = [
  { etiqueta: $localize`Mis proyectos`, icono: 'lucideFolderKanban', filtro: FILTROS.mios, ruta: '/projects' },
  { etiqueta: $localize`Mis tareas`, icono: 'lucideSquareCheck', filtro: FILTROS.mios, ruta: '/tasks' },
  { etiqueta: $localize`Mis tickets`, icono: 'lucideTicket', filtro: FILTROS.mios, ruta: '/tickets' }
];

/**
 * El del panel de control. `type` no es `filter`: son paneles, no listas filtradas, y el
 * parámetro que la pantalla lee se llama así.
 */
export const ENTRADAS_DE_PANEL: readonly EntradaDeMenu[] = [
  { etiqueta: $localize`Todos`, icono: 'lucideList', filtro: null, params: { type: 'all' } },
  { etiqueta: $localize`Mis paneles`, icono: 'lucideUser', filtro: null, params: { type: 'private' } },
  { etiqueta: $localize`Del equipo`, icono: 'lucideUsers', filtro: null, params: { type: 'public' } }
];

/**
 * Qué panel enseña cada módulo.
 *
 * <b>Están todos los del menú lateral, no sólo tres.</b> Antes sólo tenían panel tareas, tickets y
 * proyectos; el resto se quedaba con el desplegable viejo, así que la barra se comportaba de dos
 * maneras según dónde pusieras el ratón.
 *
 * Un módulo que no aparezca aquí —uno nuevo— recibe igualmente su panel, con la entrada «Ver todo»
 * hacia su ruta: ver <c>vocabularioDe</c>. Es poco, pero es cierto, y se amplía añadiéndolo aquí.
 */
export const VOCABULARIO: Readonly<Record<string, VocabularioDeModulo>> = {
  home: { titulo: $localize`Inicio`, inicial: 'I', entradas: ENTRADAS_DE_INICIO },
  dashboard: { titulo: $localize`Panel`, inicial: 'P', entradas: ENTRADAS_DE_PANEL },
  docs: { titulo: $localize`Documentos`, inicial: 'D', entradas: ENTRADAS_DE_DOCUMENTOS },
  projects: { titulo: $localize`Proyectos`, inicial: 'P', entradas: ENTRADAS_TRANSVERSALES },
  tasks: { titulo: $localize`Tareas`, inicial: 'T', entradas: ENTRADAS_TRANSVERSALES },
  tickets: { titulo: $localize`Tickets`, inicial: 'S', entradas: ENTRADAS_TRANSVERSALES }
};

/**
 * El vocabulario de un módulo, con una salida digna para los que no tienen uno escrito.
 *
 * Un módulo nuevo en la barra lateral obtiene panel sin tocar nada: enseña su nombre y «Ver todo»,
 * que es lo único que se puede afirmar sin conocerlo. Inventarle «Mis X» o «X del equipo» sería
 * repetir el fallo que quitamos: ofrecer filtros que el servidor no aplica.
 */
export function vocabularioDe(modulo: string, titulo?: string): VocabularioDeModulo {
  const conocido = VOCABULARIO[modulo];
  if (conocido) return conocido;

  const nombre = titulo ?? modulo;

  return {
    titulo: nombre,
    inicial: (nombre[0] ?? '·').toUpperCase(),
    entradas: [{ etiqueta: $localize`Ver todo`, icono: 'lucideList', filtro: null }]
  };
}
