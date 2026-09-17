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
`/calendar/agenda/{dia}`, `/papelera`, `/{id}/restaurar`, `/archivar`, `/desarchivar`,
`/tickets/claves-de-entrada`, `/entrada/tickets`, `/tickets/{id}/adjuntos`.

**Tablas en español**: `Favoritos`, `Exportaciones`, `ContenidosDeExportacion`, `Programaciones`,
`UsosDePlantilla`, `AnotacionesEnDocumentos`, `MencionesEnDocumentos`, `ClavesDeEntrada`,
`AdjuntosDeTicket`. Además hay **columnas** en español en tablas con nombre inglés
(`Tickets.Origen`, `Tickets.SolicitanteNombre`, `…ArchivadoEnUtc`, `…BorradoEnUtc`, etc.).

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
| carga de trabajo | `Workload` |
| retraso | `Delay` |
| evento, anular, anulado | `Event`, `Cancel`, `Cancelled` |
| motivo de cancelación | `CancellationReason` |
| agenda del día | `DailyAgenda` |
| semana, mes | `Week`, `Month` |

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
| 1 | **BuildingBlocks + Host** (`IAlcanceDeVista`, `TenantDbContext.ComoInquilino`, `AlmacenamientoEnDisco`, `IUserContext` en todos los endpoints, `TimeProvider`) | Lo usan todos los módulos; renombrarlo después obligaría a tocarlos dos veces |
| 2 | **Identity** (favoritos, compartición, permisos) | Rutas `/comparticion` y `/me/favoritos`, tabla `Favoritos` |
| 3 | **Ticketing** (entrada, claves, adjuntos) | Lo más reciente; rutas públicas `/entrada/tickets` |
| 4 | **WorkItems + Projects + Teams** | Archivo/papelera compartidos |
| 5 | **Docs** (plantillas, anotaciones, árbol) + **Comments** | Editor y extensiones del frontend |
| 6 | **Calendar + Notifications + Communication** | Agenda |
| 7 | **Reporting** (motor, exportaciones, programaciones, paneles) | El bloque más grande del Host |
| 8 | **CustomFields + Automations + Webhook + Tags** | Fórmulas y reglas |
| 9 | **Frontend transversal** (`shared/`, `core/`, e2e) | Lo que no arrastraron los PRs anteriores |

### Migraciones de renombrado

- Siempre `RenameTable` / `RenameColumn` / `RenameIndex`, **nunca** borrar y crear: los datos se
  quedan. Se escriben a mano si EF propone `DropTable` + `CreateTable`.
- Cada migración se prueba contra la base de desarrollo con datos antes de subirla.

### Compatibilidad hacia fuera

- La API cambia de rutas y de campos. No hay integradores externos todavía (la entrada de tickets
  no está desplegada), así que **no se mantienen alias** de las rutas viejas.
- Si antes de llegar al PR 3 alguien integra la entrada de tickets, se mantiene
  `/api/v1/entrada/tickets` como alias durante una versión.

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

