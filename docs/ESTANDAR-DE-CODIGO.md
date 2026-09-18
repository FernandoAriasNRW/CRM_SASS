# Estándar de código: nombres en inglés y principios SOLID

Decidido el 17 de septiembre de 2026:

- **Todo identificador pasa a inglés**: clases, records, interfaces, métodos, propiedades,
  variables, constantes, ficheros y carpetas, nombres de pruebas, **rutas de la API, campos del
  JSON, tablas y columnas**.
- **Los comentarios, la documentación XML y `docs/` siguen en español.** Los textos de la interfaz
  también: el español es el idioma de origen del i18n y eso no cambia.
- **Un PR por bloque**, con el CI en verde, empezando cuando el #27 esté mergeado.

Este documento es la referencia de los PRs. Si un nombre no está en el glosario, se añade aquí
**antes** de usarlo: el riesgo de un cambio así no es traducir mal, es traducir lo mismo de dos
maneras en dos módulos.

---

## 1. Qué hay que cambiar

Medido con un inventario automático sobre el código propio (sin migraciones), el 17-09-2026:

| | |
|---|---|
| Ficheros analizados (`.cs`, `.ts`, `.html`) | 815 |
| Ficheros con identificadores en español | **226** como mínimo¹ |
| Ficheros cuyo propio nombre está en español | 54 |
| Palabras españolas distintas en identificadores | ~450 |
| Rutas de la API en español | 22 |
| Tablas en español | 9 |

¹ El inventario reconoce palabras de una lista; las que no están en ella se escapan. El número
real está entre 226 y ~300.

**Rutas en español** (hay que cambiarlas en el servidor y en el frontend a la vez):
`/comparticion`, `/me/favoritos`, `/exportaciones`, `/programaciones`, `/docs/plantillas/usos`,
`/docs/pages/{id}/anotaciones`, `/docs/anotaciones/{id}/resolver`, `/docs/pages/{id}/mover`,
`/calendar/agenda/{dia}`, `/papelera`, `/{id}/restaurar`, `/archivar`, `/desarchivar` (las de
tickets, tareas y proyectos ya están en inglés; quedan las de Calendar y Docs),
~~`/tickets/claves-de-entrada`, `/entrada/tickets`, `/tickets/{id}/adjuntos`~~ (bloque 3).

**Tablas en español**: `Favoritos`, `Exportaciones`, `ContenidosDeExportacion`, `Programaciones`,
`UsosDePlantilla`, `AnotacionesEnDocumentos`, `MencionesEnDocumentos`. ~~`ClavesDeEntrada`,
`AdjuntosDeTicket`~~ (bloque 3). Además hay **columnas** en español en tablas con nombre inglés
(~~`Tickets.Origen`, `Tickets.SolicitanteNombre`~~ (bloque 3), `…ArchivadoEnUtc`,
`…BorradoEnUtc`, etc.).

---

## 2. Convenciones

### Backend (.NET)

| Qué | Convención | Ejemplo |
|---|---|---|
| Tipos, métodos, propiedades, constantes | `PascalCase` | `IntakeKey`, `GetUsageAsync` |
| Parámetros y variables locales | `camelCase` | `tenantId`, `attachment` |
| Campos privados | `_camelCase` | `_storage` |
| Interfaces | prefijo `I` | `IIntakeKeyRepository` |
| Métodos asíncronos | sufijo `Async` | `FindActiveByHashAsync` |
| Comandos / consultas / handlers | `VerbNounCommand`, `GetNounQuery`, `…Handler` | `CreateExternalTicketCommand` |
| DTO | sufijo `Dto` | `IntakeKeyDto` |
| Pruebas | `Method_Scenario_Expected` o frase en inglés con `_` | `Revoked_key_is_rejected` |
| Un tipo público por fichero | nombre del fichero = nombre del tipo | `IntakeKey.cs` |
| Carpetas | inglés, plural cuando agrupan | `Intake/`, `Exports/` |
| Rutas | `kebab-case`, sustantivos en plural | `/api/v1/tickets/intake-keys` |
| JSON | `camelCase` (lo hace el serializador) | `requesterName` |
| Tablas | `PascalCase`, plural | `IntakeKeys`, `TicketAttachments` |

### Frontend (Angular)

| Qué | Convención | Ejemplo |
|---|---|---|
| Clases, interfaces, tipos | `PascalCase` | `TicketAttachment` |
| Variables, métodos, señales | `camelCase` | `saveState`, `uploadAttachments()` |
| Constantes de módulo | `UPPER_SNAKE_CASE` | `REQUIRED_FIELDS` |
| Ficheros | `kebab-case` con tipo | `intake-keys.component.ts` |
| Selectores | prefijo `app-`, inglés | `app-intake-keys` |
| `data-testid` | inglés `kebab-case` | `created-key` |

Los **IDs de i18n no dependen de los identificadores**, dependen del texto; renombrar no rompe las
traducciones. Sí cambian las líneas del catálogo, así que **cada PR re-extrae al final**.

---

## 3. Glosario

Un concepto, un nombre. Ordenado por área.

### Transversal

| Español | Inglés |
|---|---|
| inquilino, organización | `Tenant` |
| usuario, persona | `User` |
| miembro | `Member` |
| invitado | `Guest` |
| administrador | `Admin` |
| permiso, nivel | `Permission`, `Level` |
| tipo de entidad | `EntityType` |
| alcance de vista | `ViewScope` |
| visibilidad de entidades | `EntityVisibility` |
| favorito(s) | `Favorite(s)` |
| compartir, compartición | `Share`, `Sharing` |
| archivar / desarchivar | `Archive` / `Unarchive` |
| papelera, enviar a la papelera | `Trash`, `MoveToTrash` |
| restaurar | `Restore` |
| archivado en, borrado en | `ArchivedAtUtc`, `DeletedAtUtc` |
| acción de archivo | `ArchiveAction` |
| cambiar archivo/papelera (comando) | `Change<Entidad>ArchiveStateCommand` |
| vista guardada | `SavedView` |
| vocabulario | `Vocabulary` (o `…Options` si es una lista de opciones) |
| etiqueta (de texto visible) | `Label` |
| etiqueta (tag) | `Tag` |
| almacenamiento, almacén en disco | `Storage`, `DiskStorage` |
| fichero, adjunto | `File`, `Attachment` |
| subir | `Upload` |
| mención, mencionado en | `Mention`, `MentionedIn` |
| comentario, hilo | `Comment`, `Thread` |
| entidad comentable | `CommentableEntity` |
| idioma | `Language` |
| sembrador | `Seeder` |
| mensaje de error | `ErrorMessage` |
| guardar, guardado, estado de guardado | `Save`, `Saved`, `SaveState` |
| cargando | `loading` |
| crear / editar / borrar / quitar | `Create` / `Edit` / `Delete` / `Remove` |
| nuevo / nueva | `New` |
| fecha, hora, día, hoy | `Date`, `Time`, `Day`, `Today` |
| sin fecha | `NoDate` |
| título, descripción, texto, nombre | `Title`, `Description`, `Text`, `Name` |
| valor, opción, opciones | `Value`, `Option`, `Options` |
| estado, prioridad | `Status`, `Priority` |
| responsable, asignado | `Owner`, `Assignee` |
| panel lateral | `SidePanel` |
| cajón | `Drawer` |
| menú contextual | `ContextMenu` |
| pestaña | `Tab` |
| barra de vistas | `ViewBar` |
| panel de navegación | `NavigationPanel` |
| paleta de comandos | `CommandPalette` |
| ámbito (using) | `Scope` |
| ver también (borrados/archivados) | `IncludeHidden` |
| como inquilino | `AsTenant` |

### Tareas, proyectos, calendario

| Español | Inglés |
|---|---|
| tarea, subtarea | `Task`, `Subtask` |
| proyecto, espacio, carpeta | `Project`, `Space`, `Folder` |
| dependencia, arista | `Dependency`, `Edge` |
| recurrencia, ocurrencia | `Recurrence`, `Occurrence` |
| patrón de recurrencia | `RecurrencePattern` |
| calendario de recurrencia | `RecurrenceCalendar` |
| generador de tareas recurrentes | `RecurringTaskGenerator` |
| frecuencia, intervalo | `Frequency`, `Interval` |
| diaria, semanal, mensual | `Daily`, `Weekly`, `Monthly` (también el valor guardado) |
| próxima ocurrencia, fecha de fin | `NextOccurrence`, `EndDate` |
| día de la serie | `SeriesDay` |
| toca generar, agotado | `IsDue`, `IsExhausted` |
| punto de checklist: texto, hecho, posición | `ChecklistItem`: `Text`, `IsDone`, `Position` |
| progreso de checklist | `ChecklistProgress` |
| reglas (de un agregado) | `Rules` (`DetailRules`, `NestingRules`, `AssigneeRules`) |
| es responsable | `IsAssignee` |
| actualizar detalles | `UpdateDetails` |
| cerraría un ciclo | `WouldCloseCycle` |
| largo máximo | `MaxLength` |
| por defecto | `Default` |
| carga de trabajo | `Workload` |
| retraso | `Delay` |
| evento, anular, anulado | `Event`, `Cancel`, `Cancelled` |
| motivo de cancelación | `CancellationReason` |
| agenda del día | `DailyAgenda` |
| semana, mes | `Week`, `Month` |
| fecha límite, sin fecha límite, fecha de inicio | `DueDate`, `WithoutDueDate`, `StartDate` |
| Gantt: barra, hito, rango, eje, marca del eje | `Bar`, `Milestone`, `Range`, `Axis`, `AxisTick` |
| flecha de dependencia, incumplida | `Arrow`, `IsViolated` |
| bloqueante, bloqueada, bloqueada por, bloquea a | `Blocker`, `IsBlocked`, `BlockedBy`, `Blocks` |
| candidata (a bloquear, a responsable) | `Candidate` (`BlockerCandidates`, `AssigneeCandidates`) |
| fila, celda (de la carga) | `WorkloadRow`, `WorkloadCell` |
| laborable, fin de semana, el lunes de | `IsWorkday`, `IsWeekend`, `MondayOf` |
| tanda (de tarjetas en una columna) | `Batch` (`BATCH_SIZE`) |
| vistas de fábrica | `BUILT_IN_VIEWS` |

### Tickets

| Español | Inglés |
|---|---|
| entrada de tickets | `TicketIntake` |
| clave de entrada | `IntakeKey` |
| en claro (clave) | `PlainText` |
| inicio (de la clave) | `Prefix` → la constante actual `Prefijo` pasa a `KeyPrefix` |
| revocar, revocada | `Revoke`, `RevokedAtUtc` |
| último uso | `LastUsedAtUtc` |
| solicitante (nombre, email, teléfono, empresa) | `Requester` (`RequesterName`, `RequesterEmail`, `RequesterPhone`, `RequesterCompany`) |
| solicitud externa | `ExternalTicketRequest` |
| origen (aplicación / externo) | `Source` (`App` / `External`) |
| clasificación | `Classification` |
| etiquetas (claves) | `Tags` |
| adjunto de ticket | `TicketAttachment` |
| reglas de adjuntos | `AttachmentRules` |
| fichero recibido | `IncomingFile` |
| marcar usada (clave) | `MarkUsed` |
| tamaño (de un fichero) | `Size` |
| lista de etiquetas (derivada) | `TagList` |
| cuerpo HTTP del ticket externo | `ExternalTicketBody` (el contrato; `ExternalTicketRequest` es el del dominio) |
| guardado de adjuntos | `AttachmentStorage` |
| motivo de rechazo | `RejectionReason` |
| errores de la entrada | `IntakeErrors` |

### Documentos

| Español | Inglés |
|---|---|
| plantilla, plantillas predefinidas | `Template`, `BuiltInTemplates` |
| uso de plantilla | `TemplateUsage` |
| anotación (comentario en línea) | `Annotation` |
| texto citado | `QuotedText` |
| resolver / reabrir | `Resolve` / `Reopen` |
| árbol de páginas | `PageTree` |
| mover página, renombrar | `MovePage`, `Rename` |
| es descendiente | `IsDescendantOf` |
| aviso (bloque), tono | `Callout`, `Tone` |
| desplegable | `Toggle` (bloque) |
| columnas / columna | `Columns` / `Column` |
| bloque de código | `CodeBlock` |
| comandos del editor | `EditorCommands` |
| pedir URL | `PromptUrl` |
| subir fichero | `UploadFile` |
| exportar | `Export` |
| anotación en un documento (entidad), sus fechas | `DocumentAnnotation`, `CreatedAtUtc`, `ResolvedAtUtc` |
| mención en un documento, tipo mencionado, entidad mencionada | `DocumentMention`, `MentionedType`, `MentionedEntityId` |
| tipos mencionables, persona | `MentionableTypes`, `Person` |
| lector de menciones, actualizador de menciones | `MentionReader`, `MentionUpdater` |
| veces (que se usó), último uso | `Count`, `LastUsedAtUtc` |
| casillas (lista marcable) | `Checklist` |
| autor, responde a, editado en | `AuthorId`, `ReplyToId`, `EditedAtUtc` |
| tipos comentables | `CommentableEntityTypes` |
| entidad comentada (en un comentario) | `EntityType` + `EntityId` |

### Informes y panel

| Español | Inglés |
|---|---|
| informe, motor de informes | `Report`, `ReportEngine` |
| constructor de informes | `ReportBuilder` |
| definición | `Definition` |
| origen de datos | `DataSource` |
| dimensión, medida, agrupar | `Dimension`, `Measure`, `GroupBy` |
| exportación, contenido de exportación | `Export`, `ExportContent` |
| programación | `Schedule` |
| panel, recuadro, disposición | `Dashboard`, `Widget`, `Layout` |
| gráfica | `Chart` |

### Campos personalizados y automatizaciones

| Español | Inglés |
|---|---|
| campo personalizado, definición | `CustomField`, `Definition` |
| tipo de campo | `FieldType` |
| se calcula, calculado | `IsComputed`, `Computed` |
| fórmula, analizador de fórmula | `Formula`, `FormulaParser` |
| detector de ciclos | `CycleDetector` |
| automatización, regla | `Automation`, `Rule` |
| condición, acción, ejecución | `Condition`, `Action`, `Execution` |
| disparador | `Trigger` |
| operador | `Operator` |

---

## 4. SOLID: qué se aplica y dónde

No es una reescritura. En cada PR se renombra el bloque y, **en el mismo bloque**, se corrigen las
violaciones de esta lista que le tocan. Lo que no está en la lista no se toca por el nombre de un
principio: un cambio sin un fallo o un coste concreto detrás es riesgo sin retorno.

### S — Responsabilidad única

| Dónde | Problema | Qué se hace |
|---|---|---|
| `ApiHost/Services/DataSeederService.cs` (760 líneas) | Siembra los doce módulos en un método | Un sembrador por módulo detrás de `IModuleSeeder`; el servicio sólo los ordena |
| `ApiHost/Program.cs` (661 líneas) | Configuración, CORS, límites, migraciones y rutas en un fichero | Extensiones `AddRateLimiting`, `AddCorsPolicies`, `ApplyMigrationsAsync`… |
| `ApiHost/Reporting/MotorDeInformes.cs` (673 líneas) | Un origen de datos por módulo dentro de la misma clase | Un `IReportDataSource` por origen |
| Ficheros `*Cqrs.cs` con 10–20 tipos | Comandos, handlers, DTO y repositorios juntos | Un tipo por fichero, en carpetas `Commands/`, `Queries/`, `Dtos/` |
| `docs.component.ts` (1265 líneas) | Editor, guardado, árbol, exportación y comentarios | Servicios `DocumentSaveService`, `DocumentExportService`; el componente orquesta |

### O — Abierto/cerrado

| Dónde | Problema | Qué se hace |
|---|---|---|
| `CloudinaryStorageService` | `if` imagen / vídeo / otro | Estrategia por tipo de recurso |
| `EntityPermissionService.EvaluateLevel` | Niveles como cadenas en un `switch` | `PermissionLevel` como enumeración con `Satisfies(required)` |
| Endpoints de archivo y papelera | Tabla de rutas repetida en tres módulos | Extensión común `MapArchiveEndpoints<TCommand>` |

### L — Sustitución de Liskov

Sin violaciones medidas. Se vigila en las estrategias nuevas de la O.

### I — Segregación de interfaces

Sin violaciones medidas: las lecturas de pantalla ya van por interfaces `I…Queries` separadas de
los repositorios (28). Se mantiene así en los tipos nuevos.

### D — Inversión de dependencias

| Dónde | Problema | Qué se hace |
|---|---|---|
| **13 ficheros de endpoints leen los claims a mano** (≈100 veces `FindFirstValue("tenantId")`) | Cada endpoint decide cómo se obtiene el inquilino; ya hubo un fallo por eso (2.5) | Todos usan `IUserContext` |
| **27 usos de `DateTime.UtcNow`** en dominio y aplicación | El tiempo no se puede fijar en pruebas | `TimeProvider` inyectado |

---

## 5. Orden de los PRs

Cada uno: renombrar el bloque de punta a punta (dominio → aplicación → infraestructura con
migración → presentación → frontend que lo consume → pruebas), aplicar sus puntos de la sección 4,
suites completas en verde, catálogo i18n re-extraído al final.

| # | Bloque | Por qué en este orden |
|---|---|---|
| 1 ✅ | **BuildingBlocks + Host** (`IAlcanceDeVista`, `TenantDbContext.ComoInquilino`, `AlmacenamientoEnDisco`, `IUserContext` en todos los endpoints, `TimeProvider`) | Lo usan todos los módulos; renombrarlo después obligaría a tocarlos dos veces |
| 2 ✅ | **Identity** (favoritos, compartición, permisos) | Rutas `/comparticion` y `/me/favoritos`, tabla `Favoritos` |
| 3 ✅ | **Ticketing** (entrada, claves, adjuntos) | Lo más reciente; rutas públicas `/entrada/tickets` |
| 4a ✅ | **WorkItems + Projects** (backend) | Archivo/papelera compartidos. Teams ya estaba en inglés |
| 4b ✅ | **Frontend de tareas y proyectos** | Gantt, carga de trabajo y la ficha: 329 identificadores, diff aparte para poder revisarlo |
| 5a ✅ | **Docs + Comments** (backend) | Plantillas, anotaciones, árbol y menciones; tablas y rutas |
| 5b | **Frontend de documentos y comentarios** | Editor y extensiones: más de 200 identificadores, diff aparte |
| 5c | **Valores guardados de los tipos de entidad** («Tarea» → «Task»…) | Viven en tablas de cinco módulos y dentro del HTML de las páginas: cambio propio con su migración de datos |
| 6 | **Calendar + Notifications + Communication** | Agenda |
| 7 | **Reporting** (motor, exportaciones, programaciones, paneles) | El bloque más grande del Host |
| 8 | **CustomFields + Automations + Webhook + Tags** | Fórmulas y reglas |
| 9 | **Frontend transversal** (`shared/`, `core/`, e2e) | Lo que no arrastraron los PRs anteriores |
| 10 | **Nombres de las pruebas** | Son frases, no identificadores de producción; traducirlas dentro de cada bloque ensucia el diff de revisión |
| 11 | **`TimeProvider` en todos los módulos** | Cambiar el reloj módulo a módulo deja dos formas de dar la hora conviviendo; va de una vez, al final |

### Migraciones de renombrado

- Siempre `RenameTable` / `RenameColumn` / `RenameIndex`, **nunca** borrar y crear: los datos se
  quedan. Se escriben a mano si EF propone `DropTable` + `CreateTable`.
- Cada migración se prueba contra la base de desarrollo con datos antes de subirla.

### Compatibilidad hacia fuera

- La API cambia de rutas y de campos. No hay integradores externos todavía (la entrada de tickets
  no está desplegada), así que **no se mantienen alias** de las rutas viejas.
- La entrada de tickets ya cambió de ruta (bloque 3) sin dejar alias, porque nadie la había
  integrado todavía. Quien la integre desde ahora usa `/api/v1/ticket-intake`, y esa ruta ya no se
  cambia sin alias.

---

## 6. Cómo se hace cada bloque (aprendido en el bloque 1)

**Renombrar con Roslyn, no con buscar y reemplazar.** Hay una herramienta que abre la solución y usa
`Renamer.RenameSymbolAsync`, como el IDE: cambia la declaración y todas sus referencias, y no toca
textos ni comentarios. Se le pasa un mapa `fichero → nombre viejo → nombre nuevo`.

**El renombrado se propaga, y hay que medirlo.** Renombrar un método de una interfaz renombra
también el de cualquier clase que lo implemente, aunque venga de otra interfaz de otro módulo. En el
bloque 1 eso cambió una propiedad de una entidad de Docs, que es una columna: la aplicación
compilaba y las menciones daban 500. Después de cada renombrado:

1. `dotnet ef migrations has-pending-model-changes` en **todos** los contextos. Si alguno tiene
   cambios, es una columna renombrada sin migración.
2. Comparar las propiedades públicas de los tipos de `main` y de la rama (lo que sale en el JSON).
   Cada campo renombrado se busca en el frontend por su nombre viejo.
3. Las suites completas.

**Cada nombre se renombra en el bloque del módulo dueño, en todo el repositorio a la vez.** Muchos
ficheros del Host pertenecen a otro módulo (las exportaciones a Reporting, los avisos de
automatización a Automations, la agenda a Calendar) y van con él.

**Los valores guardados no son nombres.** `EntityTypes.Task` se renombró, pero su valor sigue
siendo `"Tarea"`: está escrito dentro del HTML de los documentos (`data-mencion-tipo`) y en las
tablas de favoritos y menciones. Cambiarlo exige migrar ese contenido, y va con el bloque de Docs.

### Hecho en el bloque 1

- BuildingBlocks y Host genérico en inglés; columnas `ArchivedAtUtc` y `DeletedAtUtc`.
- `IUserContext` en todos los endpoints (≈130 lecturas de claims a mano).
- `Program.cs` dividido en `Startup/`; el sembrador, en un `IModuleSeeder` por módulo.
- `TimeProvider` registrado y usado en el outbox; los usos en entidades de dominio van con su módulo.
- Borrado: el segundo sistema de webhooks de `BuildingBlocks.Infrastructure/Webhooks`, que nadie
  registraba y enviaba a la URL literal `"URL_DESTINO"`, y `DbSeeder.cs`, comentado entero.

### Hecho en el bloque 2

- Favoritos, compartición y permisos en inglés; tabla `Favoritos` → `Favorites` con migración de
  renombrado escrita a mano, porque EF proponía borrarla y crearla.
- Rutas `/comparticion` → `/sharing` y `/users/me/favoritos` → `/me/favorites`; campos `marcado` →
  `isFavorite` y `nivel` → `level`.
- Un solo vocabulario de tipos de entidad y un solo traductor a permisos.

### Hecho en el bloque 3

- Ticketing entero: dominio, aplicación, infraestructura, endpoints, frontend y pruebas.
  `ClaveDeEntrada` → `IntakeKey`, `AdjuntoDeTicket` → `TicketAttachment`, `SolicitudExterna` →
  `ExternalTicketRequest`, `Solicitante*` → `Requester*`, `Origen` → `Source`.
- Rutas: `/entrada/tickets` → `/ticket-intake`, `/tickets/claves-de-entrada` →
  `/tickets/intake-keys`, `/tickets/{id}/adjuntos` → `/tickets/{id}/attachments`, y
  `archivar`/`desarchivar`/`restaurar` → `archive`/`unarchive`/`restore`.
- Tablas `ClavesDeEntrada` → `IntakeKeys` y `AdjuntosDeTicket` → `TicketAttachments`, con sus
  columnas y sus índices. **La migración se escribió a mano**: EF proponía `DropTable` +
  `CreateTable` para las dos y `DropColumn` para `Origen`, que habría borrado 1 clave, 2 adjuntos y
  el origen de 232 tickets de la base de desarrollo.
- **Un valor guardado también cambió**: `Origen` valía «Aplicacion»/«Externo» y `Source` vale
  «App»/«External». Va con un `UPDATE` en la propia migración; sin él, un ticket externo dejaría de
  reconocerse como tal y la ficha no enseñaría al solicitante. Ver la nota de «los valores
  guardados no son nombres» de más arriba: aquí sí se podía migrar, porque el valor sólo vive en
  esa columna.
- `EntradaDeTicketsCqrs.cs` (21 tipos en un fichero) dividido en `Intake/`, un tipo por fichero;
  `ArchivoYPapeleraCqrs.cs`, en `Archiving/`.
- Dos funciones del vocabulario del frontend pasaron a exponerse tal cual en los componentes: un
  método con el mismo nombre que la función importada (`statusBadge`) se llamaba a sí mismo.
- **Pendiente y a propósito: el reloj.** Las entidades de Ticketing siguen llamando a
  `DateTime.UtcNow` (`Ticket.Create`, `Archive`, `MoveToTrash`) y los handlers de la entrada se lo
  pasan a mano. El bloque 2 dejó igual `Favorite.Mark`. Convertirlo módulo a módulo deja el
  repositorio con dos formas de dar la hora a la vez, que es justo lo que el estándar quiere
  evitar, así que va en **un cambio propio para todos los módulos** —con `TimeProvider` inyectado
  y las entidades recibiendo `nowUtc`— después del bloque 8. Anotado también en el bloque 10.
### Hecho en el bloque 4a (backend de tareas y proyectos)

- Recurrencia entera en inglés: `PatronDeRecurrencia` → `RecurrencePattern`,
  `CalendarioDeRecurrencia` → `RecurrenceCalendar`, `GeneradorDeTareasRecurrentes` →
  `RecurringTaskGenerator`, y el detector de ciclos `DetectorDeCiclos` → `CycleDetector` con su
  `Arista` → `Edge`.
- Checklist: `Texto`/`Hecho`/`Posicion` → `Text`/`IsDone`/`Position`, columnas incluidas.
- Rutas `archivar`/`desarchivar`/`restaurar` → `archive`/`unarchive`/`restore` en tareas y
  proyectos, y `CambiarArchivoDeTareaCommand` → `ChangeTaskArchiveStateCommand`.
- **La migración volvió a necesitar reescritura, y esta vez por un cruce.** EF emparejó
  `Recurrence_Intervalo → Recurrence_SeriesDay` y `Recurrence_DiaDeLaSerie → Recurrence_Interval`:
  las dos columnas son `int`, así que las cruzó. Aplicada tal cual, una tarea que se repite cada 2
  meses el día 31 habría pasado a repetirse cada 31 meses el día 2. Se comprobó poniendo una serie
  con intervalo 2 y día 31 en la base de desarrollo antes de migrar, y leyéndola después.
  **Lección: en una migración de renombrado hay que leer cada pareja, no sólo comprobar que no hay
  `DropColumn`.**
- Los valores guardados de la frecuencia también cambiaron («Diaria» → «Daily»), con su `UPDATE`
  en la migración: sin él el generador no reconocería las series existentes y dejaría de crear sus
  tareas.
- Del frontend, en este bloque va sólo el contrato: los campos de la recurrencia y de la
  checklist y las claves de frecuencia. **Dos fallos que compilaban**: el PATCH de la checklist
  seguía mandando `hecho` (el servidor lo habría ignorado sin error) y el estado local escribía un
  campo que ya no existía, así que marcar un punto habría dejado de pintarse.

- Lo que sigue en español dentro de estas pantallas es de otros bloques: la barra de vistas
  (`BarraDeVistasComponent`, `VistaIntegrada` y sus campos `clave`/`etiqueta`/`icono`),
  `mensajeDeError`, `urlDeFichero`, los comentarios y las menciones. Van con el bloque 9.

### Hecho en el bloque 4b (frontend de tareas y proyectos)

- Gantt (`gantt.ts` y su componente), carga de trabajo (`carga*` → `workload*`, selector
  `app-carga` → `app-workload`), la ficha, el listado y el tablero, y los vocabularios
  (`vocabulario-de-tareas.ts` → `task-vocabulary.ts`, `vocabulario-de-proyectos.ts` →
  `project-vocabulary.ts`). Las pruebas unitarias de Gantt y carga van con sus nombres de
  variables; los textos de `it(...)` se quedan para el bloque 10.
- Se hizo con `tools/scripts/rename-frontend.py`, que no existía. **Tuvo tres fallos, y dos no los
  veía el compilador:**
  - Trataba los atributos planos (`subtitle="..."`, `placeholder`, `aria-label`, el `message` de un
    estado vacío) como expresiones y **tradujo 18 textos de la interfaz**: «Crea una nueva task en
    tu project», «Sin tasks». Tampoco respetaba las cadenas dentro de un enlace
    (`[subtitle]="'Detalles del proyecto'"`). Compilaba y las pruebas pasaban.
  - Saltaba las cabeceras `@if (x > 0)` y los enlaces `[style.width.%]`: la carga leía
    `celda.horas` en dos sitios y `celda.hours` en el tercero.
  - Por trabajar por tokens, renombró campos de tipos compartidos que son del bloque 9
    (`VistaIntegrada.clave`, `CellEdit.valor`, `OpcionesDeLlamada.sinAviso`). Éste sí lo vio el build.
  Los tres están corregidos en el script, y sus límites, en `tools/README.md`. **Lección: en el
  frontend hay que comparar con `main` los atributos y las cadenas de las plantillas**, porque
  un texto de la interfaz cambiado no lo detecta ni el build ni las pruebas.
- Los inputs y outputs de Gantt y carga (`[tareas]`, `[dependencias]`, `(abrir)`) cambiaron a
  mano en `tasks.component.html`: el script sólo toca valores, no nombres de atributo.
- **Se queda en español a propósito: el valor `'carga'`** del modo de vista. Se guarda en el
  `stateJson` de las vistas guardadas (`viewType`), así que cambiarlo exige migrar esas filas; va
  con el bloque 9, junto con la barra de vistas.

### Hecho en el bloque 5a (backend de Docs y Comments)

- Docs: `AnotacionEnDocumento` → `DocumentAnnotation`, `MencionEnDocumento` → `DocumentMention`,
  `UsoDePlantilla` → `TemplateUsage`, `PlantillasPredefinidas` → `BuiltInTemplates`,
  `LectorDeMenciones` → `MentionReader`, y los comandos (`MoverPagina` → `MovePage`,
  `RenombrarDocumento` → `RenameDocument`, `CrearAnotacion` → `CreateAnnotation`…). Carpetas y
  espacios de nombres `Menciones`/`Anotaciones`/`Plantillas` → `Mentions`/`Annotations`/`Templates`.
- Comments: `Texto`/`AutorId`/`CreadoUtc`/`EditadoUtc`/`RespondeAId`/`EntidadDestino` →
  `Text`/`AuthorId`/`CreatedAtUtc`/`EditedAtUtc`/`ReplyToId`/`EntityType`, y `Reglas` → `Rules`.
- Un tipo por fichero en `CommentsCqrs.cs`, `CommentsInfrastructure.cs`, `AnotacionesCqrs.cs`,
  `MencionesCqrs.cs` y los ficheros de menciones y plantillas.
- Rutas: `/docs/plantillas/usos` → `/docs/templates/usage`, `/pages/{id}/mover` → `/move`,
  `/anotaciones` → `/annotations` (con `/resolver` → `/resolve`), `/docs/menciones` →
  `/docs/mentions`, y en Comments `{entidad}` → `{entityType}`.
- Tablas `AnotacionesEnDocumentos`, `MencionesEnDocumentos` y `UsosDePlantilla` →
  `DocumentAnnotations`, `DocumentMentions` y `TemplateUsages`, con columnas e índices, y las
  seis columnas de `Comments`. **La migración de Docs se reescribió a mano**: EF volvía a proponer
  `DropTable` + `CreateTable` para las tres tablas, que habría borrado menciones, anclajes de
  comentarios y contadores. Se comprobó contra la base de desarrollo con una anotación resuelta y
  una respuesta editada sembradas antes de migrar: cada valor quedó en su columna.
- **Un fallo que compilaba:** Roslyn renombró el parámetro de las lambdas de los endpoints
  (`entidad` → `entityType`, `tipo` → `type`) pero no la plantilla de la ruta (`{entidad}`,
  `{tipo}`), que es una cadena. La API arrancaba y los comentarios y las menciones habrían dado
  400. **Lección: tras renombrar un parámetro de un endpoint, revisar su plantilla de ruta.**
- **Un vocabulario duplicado:** `CommentableEntityTypes` repetía «Tarea», «Ticket» y «Proyecto»
  a mano; ahora sale de `EntityTypes`.
- **Un defecto de producto:** mover una página a otro padre no renumeraba el grupo de origen,
  aunque un comentario del manejador decía que sí. Quedaban huecos en el orden y el siguiente
  «ponla en la posición 1» caía donde no se veía. Arreglado, con su prueba de integración.
- Del frontend va sólo el contrato: los campos de comentarios, anotaciones y usos de plantilla,
  el `language` de crear desde plantilla, y las rutas. Los identificadores, en el 5b.
- **Se quedan a propósito para el 5c** los valores guardados: «Tarea», «Proyecto», «Documento»,
  «Persona» y «Anotacion» en `DocumentMentions.MentionedType`, `Comments.EntityType`, favoritos,
  compartición y campos personalizados, y los atributos del HTML de las páginas
  (`data-mencion-tipo`, `data-tipo="aviso"`, `data-tono`), que obligan a reescribir contenido.

---

## 7. La skill y las herramientas---

## 7. La skill y las herramientas

Este documento es la referencia; lo que hay que **hacer y comprobar** en cada cambio está en la
skill del proyecto, `.claude/skills/estandar-de-codigo/SKILL.md`, que se comparte en el repositorio
a propósito (el resto de `.claude/` está ignorado). `CLAUDE.md` apunta a las dos cosas.

Las herramientas viven en `tools/` y están documentadas en `tools/README.md`:

| Herramienta | Para qué |
|---|---|
| `tools/rename-symbols` | Renombrar con Roslyn: declaración y todas las referencias, sin tocar textos ni comentarios |
| `tools/contract-snapshot` | Ver qué campos del JSON cambian entre `main` y la rama |
| `tools/scripts/split-types.py` | Un tipo público por fichero, conservando sus comentarios |
| `tools/scripts/spanish-identifiers.py` | Qué identificadores siguen en español |
| `tools/scripts/rename-frontend.py` | Renombrar en TypeScript y plantillas sin tocar comentarios ni textos |

**Nada de esto entra en el CI ni en la aplicación**: `tools/` no forma parte de `CrmSaaS.sln`.

