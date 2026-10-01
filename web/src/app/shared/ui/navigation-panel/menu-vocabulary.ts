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
export interface MenuEntry {
  /** Lo que se lee en pantalla. */
  readonly label: string;

  /** El icono de lucide. */
  readonly icon: string;

  /**
   * El valor de `?filter=`, o `null` para «ver todo», que es la ausencia de filtro.
   *
   * Se distingue `null` de la cadena vacía a propósito: la vista quita el parámetro de la URL
   * en lugar de mandarlo vacío, y así la URL de «ver todo» es la limpia de siempre.
   */
  readonly filter: string | null;

  /**
   * A dónde lleva, si no es a la lista del propio módulo.
   *
   * Existe porque no todos los submenús son filtros sobre la misma lista: el de Inicio lleva a
   * las tareas y a los tickets de uno, y el de Documentos cambia de pestaña con `?tab=`. Sin
   * esto habría que mantener dos mecanismos —el panel para unos módulos y el desplegable viejo
   * para otros—, que es justo lo que se acaba de quitar.
   */
  readonly route?: string;

  /** Parámetros extra de la URL, para los submenús que no filtran con `?filter=`. */
  readonly params?: Readonly<Record<string, string>>;

  /** Separador visual antes de esta entrada. */
  readonly separatorBefore?: boolean;
}

/** Los nombres tal cual los entiende el servidor. Ver `FiltrosDeVista` en BuildingBlocks. */
export const FILTERS = {
  mine: 'mine',
  createdByMe: 'created',
  favorites: 'favorites',
  sharedWithMe: 'shared',
  privateOnly: 'private',
  archived: 'archived',
  trash: 'trash'
} as const;

/**
 * Las entradas que valen para cualquier módulo.
 *
 * Cada módulo interpreta «mío» a su manera —responsable de la tarea, dueño del proyecto, agente
 * del ticket— y eso es correcto: el nombre es común, el significado lo pone el dominio.
 */
export const SHARED_ENTRIES: readonly MenuEntry[] = [
  { label: $localize`Ver todo`, icon: 'lucideList', filter: null },
  { label: $localize`Asignado a mí`, icon: 'lucideUser', filter: FILTERS.mine },
  { label: $localize`Creado por mí`, icon: 'lucidePenLine', filter: FILTERS.createdByMe },
  { label: $localize`Favoritos`, icon: 'lucideStar', filter: FILTERS.favorites },

  { label: $localize`Compartido conmigo`, icon: 'lucideShare2', filter: FILTERS.sharedWithMe, separatorBefore: true },
  { label: $localize`Privado`, icon: 'lucideLock', filter: FILTERS.privateOnly },

  // Archivo y papelera van al final y separados: son las dos entradas que enseñan cosas que el
  // resto de la aplicación esconde, y conviene que se note que se está saliendo de la vista
  // normal.
  { label: $localize`Archivado`, icon: 'lucideArchive', filter: FILTERS.archived, separatorBefore: true },
  { label: $localize`Papelera`, icon: 'lucideTrash2', filter: FILTERS.trash }
];

/**
 * Lo que se pinta en la cabecera del panel de cada módulo.
 *
 * El plan de la Fase 5 listaba además entradas propias de cada módulo —«Vencen esta semana»,
 * «Sin asignar», «Vencidos de SLA», «En riesgo»—. **No están aquí porque el servidor todavía no
 * las sabe filtrar.** Cuando existan, se añaden; hasta entonces serían justo lo que esta fase
 * viene a quitar.
 */
export interface ModuleVocabulary {
  readonly title: string;
  readonly initial: string;
  readonly entries: readonly MenuEntry[];
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
export const DOCUMENT_ENTRIES: readonly MenuEntry[] = [
  { label: $localize`Todos los documentos`, icon: 'lucideFileText', filter: null },
  { label: $localize`Mis documentos`, icon: 'lucideUser', filter: null, params: { tab: 'my' } },
  { label: $localize`Compartidos conmigo`, icon: 'lucideShare2', filter: null, params: { tab: 'shared' } },
  { label: $localize`Privados`, icon: 'lucideLock', filter: null, params: { tab: 'private' } },
  { label: $localize`Actas de reunión`, icon: 'lucideCalendarDays', filter: null, params: { tab: 'meeting-notes' } },
  { label: $localize`Mis plantillas`, icon: 'lucideLayoutTemplate', filter: null, params: { tab: 'templates' } },
  { label: $localize`Archivados`, icon: 'lucideArchive', filter: null, params: { tab: 'archived' }, separatorBefore: true }
];

/**
 * El de Inicio: atajos a lo de uno en cada módulo.
 *
 * No filtra nada propio —Inicio no es una lista— así que cada entrada lleva a otro sitio. Son las
 * tres que ya ofrecía el desplegable viejo y que **sí** funcionaban.
 */
export const HOME_ENTRIES: readonly MenuEntry[] = [
  { label: $localize`Mis proyectos`, icon: 'lucideFolderKanban', filter: FILTERS.mine, route: '/projects' },
  { label: $localize`Mis tareas`, icon: 'lucideSquareCheck', filter: FILTERS.mine, route: '/tasks' },
  { label: $localize`Mis tickets`, icon: 'lucideTicket', filter: FILTERS.mine, route: '/tickets' }
];

/**
 * El del panel de control. `type` no es `filter`: son paneles, no listas filtradas, y el
 * parámetro que la pantalla lee se llama así.
 */
export const DASHBOARD_ENTRIES: readonly MenuEntry[] = [
  { label: $localize`Todos`, icon: 'lucideList', filter: null, params: { type: 'all' } },
  { label: $localize`Mis paneles`, icon: 'lucideUser', filter: null, params: { type: 'private' } },
  { label: $localize`Del equipo`, icon: 'lucideUsers', filter: null, params: { type: 'public' } }
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
export const MENU_VOCABULARY: Readonly<Record<string, ModuleVocabulary>> = {
  home: { title: $localize`Inicio`, initial: 'I', entries: HOME_ENTRIES },
  dashboard: { title: $localize`Panel`, initial: 'P', entries: DASHBOARD_ENTRIES },
  docs: { title: $localize`Documentos`, initial: 'D', entries: DOCUMENT_ENTRIES },
  projects: { title: $localize`Proyectos`, initial: 'P', entries: SHARED_ENTRIES },
  tasks: { title: $localize`Tareas`, initial: 'T', entries: SHARED_ENTRIES },
  tickets: { title: $localize`Tickets`, initial: 'S', entries: SHARED_ENTRIES }
};

/**
 * El vocabulario de un módulo, con una salida digna para los que no tienen uno escrito.
 *
 * Un módulo nuevo en la barra lateral obtiene panel sin tocar nada: enseña su nombre y «Ver todo»,
 * que es lo único que se puede afirmar sin conocerlo. Inventarle «Mis X» o «X del equipo» sería
 * repetir el fallo que quitamos: ofrecer filtros que el servidor no aplica.
 */
export function vocabularyOf(moduleKey: string, title?: string): ModuleVocabulary {
  const known = MENU_VOCABULARY[moduleKey];
  if (known) return known;

  const name = title ?? moduleKey;

  return {
    title: name,
    initial: (name[0] ?? '·').toUpperCase(),
    entries: [{ label: $localize`Ver todo`, icon: 'lucideList', filter: null }]
  };
}
