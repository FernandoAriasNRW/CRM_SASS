# CRM SaaS Suite — Estado y Ruta de Trabajo

**Auditoría:** 2026-08-11 · **Última actualización:** 2026-08-11 (Fases 1, 2 y 3)
**Posicionamiento:** plataforma de **work management**, compitiendo con ClickUp y Monday.com
**Alcance:** backend (.NET 9), frontend (Angular 21), testing, CI/CD, UI/UX

---

## 1. Resumen ejecutivo

La arquitectura backend está bien planteada —monolito modular, Clean Architecture por
bounded context, CQRS, Outbox— y es una base sólida sobre la que escalar. El problema
no era el diseño, era la salud operativa: al auditar se encontraron cero tests
ejecutándose, credenciales versionadas y un CI que no podía pasar.

Estado tras las tres primeras fases:

| | Antes | Ahora |
|---|---|---|
| Repositorio git | sólo `web/` | backend + frontend, historia preservada |
| Tests ejecutándose | 0 | **120 backend** (112 + 8) + **27 frontend** + **35 E2E** |
| Secretos versionados | 3 ficheros | 0 |
| Endpoints sin autenticación | 1 | 0 |
| Validación de entrada | escrita pero nunca ejecutada | activa en el pipeline |
| CI | imposible de pasar | funcional, con E2E y contenedores |
| Lint frontend | inexistente | 0 errores, 171 avisos medidos |
| Esquema de base de datos | `EnsureCreated` y `ALTER` crudos al arrancar | migraciones aplicadas y verificadas (§7, bloque 4.0) |

**Las Fases 1, 2 y 3 están implementadas por completo.** El frente que queda es de
producto (§3), no de calidad.

El aislamiento multi-tenant está blindado y verificado al arrancar (§5), y la interfaz ya
no es de MVP: tokens semánticos completos, accesibilidad auditada en 9 vistas, paleta de
comandos, tableros que no mienten sobre lo que se guardó, y español e inglés.

El frente que queda es de producto: **no compite todavía en features** con ClickUp o
Monday (§3). Ahí es donde entra la Fase 4.

---

## 2. Inventario del estado actual

### 2.1 Backend — .NET 9

452 archivos `.cs`. 13 módulos con separación física estricta en 4 proyectos
(`Domain` / `Application` / `Infrastructure` / `Presentation`):

`Identity` · `Projects` · `WorkItems` · `Ticketing` · `Notifications` · `Calendar`
`Communication` · `Docs` · `Reporting` · `Tags` · `Teams` · `Webhook`

**Fortalezas reales:**
- Separación de contextos consistente, con `DbContext` por módulo. Buena base para
  extraer servicios más adelante sin reescribir.
- Outbox implementado de verdad (`outbox_messages` + `OutboxDispatcherWorker`), con
  domain events e integration events separados.
- Aislamiento por tenant y soft delete aplicados por el `DbContext`, no por cada consulta,
  y verificados al arrancar.
- 3 hubs SignalR reales: notificaciones, tablero y tickets.
- Plantillas de proyecto ya existen (`/from-template`, `/save-as-template`).

**Pendiente (detalle y plan en §5):**

| Problema | Estado |
|---|---|
| Multi-tenancy con filtro global, verificado al arrancar | ✅ Fase 2 |
| `IntegrationTests` contra MySQL real en contenedor | ✅ Fase 2 |
| Build sin advertencias (eran 84: `EF1002` y nulabilidad) | ✅ Fase 2 |
| `Page` y `TeamMember` sin `TenantId`: quedan fuera del filtro global | 🟠 Fase 3 |
| Sin caché de salida ni índices revisados | 🟡 Fase 4 |

### 2.2 Frontend — Angular 21

96 archivos `.ts`, 34 templates, ~15k líneas. Standalone components, lazy routing,
NgRx + Signals, PWA.

Features enrutadas: `home` · `dashboard` · `projects` · `tasks` · `tickets` · `chat`
`calendar` · `reports` · `teams` · `docs` · `profile` · `admin` (+ `login`, `support` público).

**Fortalezas reales:**
- Stack moderno y coherente: Angular 21, Tailwind, Spartan-ng (ShadCN para Angular),
  ng-icons/lucide, TipTap para documentos.
- Design tokens HSL bien definidos en `styles.scss` con modo oscuro, cableados a Tailwind.
- **El token de acceso vive en memoria, no en localStorage** (`auth-signal.store.ts:40`).
  Decisión de seguridad correcta y deliberada.
- 25 componentes compartidos en `shared/ui` (modal, drawer, data-table, skeleton,
  empty-state, toast…).

**Pendiente:**

| Problema | Medida | Estado |
|---|---|---|
| Paleta cruda de Tailwind en vez de tokens | eran 1141 usos | ✅ Fase 3.1 |
| Accesibilidad: teclado, etiquetas, contraste, nombres | eran 55 avisos + 7 fallos que sólo destapó axe | ✅ Fases 3.2, 3.8 y 3.10 |
| Sintaxis de control antigua (`*ngIf`) | eran 33 avisos | ✅ Fase 3.6 |
| Imports muertos | eran 28 | ✅ Fase 3.6 |
| Tablero pintando hasta 1000 tarjetas | 25 por columna, con «mostrar más» | ✅ Fase 3.4 |
| Estados vacíos y de carga sin usar (y rotos) | 2 errores latentes | ✅ Fase 3.5 |
| `any` sin tipar | 84 avisos; los de `data-table` resueltos con genéricos | 🟡 quedan los de interop |
| Funciones vacías | 20 avisos | 🟡 |
| i18n | 319 cadenas, español e inglés | ✅ Fase 3.9 |
| Componentes obesos | `docs` partido en 3 modales | ✅ Fase 3.7 |
| Specs frontend unitarias | 17 | 🟡 cobertura aún baja |

### 2.3 Testing

```
tests/UnitTests/        112 tests en verde ✅
tests/IntegrationTests/   8 tests contra MySQL en contenedor ✅
web/e2e/                 35 flujos con Playwright, con axe en 9 vistas ✅
web/ (unitarios)         27 specs — cobertura aún baja fuera de shared/ y core/
```

---

## 3. Análisis competitivo: el hueco frente a ClickUp y Monday.com

El producto tiene la **infraestructura** de una plataforma de work management pero le
falta el **modelo de dominio**. La jerarquía `Space → Folder → Project → Task` ya existe
y es correcta —es la misma de ClickUp—, pero la entidad `WorkTask` es muy delgada:

```
WorkTask: TenantId, ProjectId, Title, Description, Status,
          AssigneeId, CreatedById, EstimatedHours, DueDate, TagIds
Métodos:  Create, Move, Assign, AddTag, RemoveTag
```

Comparado con lo que un usuario espera de ClickUp o Monday en 2026:

| Capacidad | Estado | Impacto competitivo |
|---|---|---|
| **Prioridad de tarea** | ❌ no existe | 🔴 Ausencia llamativa: está en todos los competidores |
| **Subtareas / jerarquía** | ❌ no existe | 🔴 Bloquea desglose de trabajo real |
| **Dependencias entre tareas** | ❌ no existe | 🔴 Sin esto no hay Gantt ni ruta crítica |
| **Múltiples responsables** | ❌ `AssigneeId` es un solo Guid | 🔴 Monday y ClickUp permiten varios |
| **Campos personalizados** | ❌ no existe | 🔴 Bloqueante para enterprise; es *la* feature de Monday |
| **Time tracking** | ❌ sólo `EstimatedHours` | 🔴 Sin registro real de tiempo |
| **Automatizaciones** | ❌ no existe | 🔴 Feature #1 en comparativas |
| **Vista Gantt / timeline** | ❌ no existe | 🟠 Esperada en plan de pago |
| **Vista tabla / hoja de cálculo** | ❌ no existe | 🟠 Es la vista por defecto de Monday |
| **Vista carga de trabajo** | ❌ no existe | 🟠 Diferenciador de plan alto |
| **Checklists** | ❌ no existe | 🟠 |
| **Tareas recurrentes** | ❌ no existe | 🟠 |
| **Formularios de captura** | ❌ no existe | 🟠 Entrada de trabajo desde fuera |
| **Metas / OKRs** | ❌ no existe | 🟡 |
| Vista tablero (Kanban) | ✅ | |
| Vista calendario | ✅ | |
| Documentos colaborativos | ✅ TipTap | Paridad con ClickUp Docs |
| Chat / conversaciones | ✅ | |
| Tickets de soporte | ✅ | **Diferenciador**: ninguno lo trae de serie |
| Dashboards e informes | ✅ parcial | |
| Plantillas de proyecto | ✅ | |
| Vistas guardadas | 🟡 `SavedView` existe, a medias | |
| Acceso de invitado | ✅ `GuestToken` | |
| Webhooks | ✅ | |
| Tiempo real | ✅ SignalR | Paridad |

### Dónde se puede ganar

Competir de frente con ClickUp en cantidad de features es una guerra perdida: llevan
años y cientos de ingenieros. Las dos aperturas reales son:

**1. Soporte integrado en la plataforma de trabajo.** El módulo `Ticketing` ya existe,
con alta pública y hub propio. Ni ClickUp ni Monday traen un helpdesk de verdad —obligan
a integrar Zendesk o Intercom. Un ticket que se convierte en tarea, con el hilo del
cliente enganchado, es una costura que ellos no tienen. **Es el diferenciador más barato
de construir porque el 70% ya está hecho.**

**2. Velocidad y foco.** ClickUp tiene fama de lento y sobrecargado; es su queja número
uno. Un producto que haga el 80% de los casos de uso con la mitad de la fricción tiene
mercado. Esto es una decisión de UX, no de features: se gana en la Fase 3, no añadiendo
más cosas.

Lo que **no** hay que hacer es perseguir la paridad completa. La Fase 4 prioriza sólo lo
que es bloqueante para vender (prioridad, subtareas, dependencias, campos personalizados,
time tracking, automatizaciones) y deja el resto en backlog.

---

## 4. Fase 1 — Estabilización ✅ COMPLETADA

*Objetivo: que el proyecto sea seguro de tocar.*

| # | Tarea | Resultado |
|---|---|---|
| 1.0 | Unificar el control de versiones | El repo git sólo cubría `web/`. La historia del frontend se reescribió bajo el prefijo `web/` (4 commits preservados, remoto intacto) y el backend entró en el mismo repositorio. `.gitignore` y `.gitattributes` nuevos excluyen `bin/`, `obj/`, `node_modules/`, `build.log` (13 MB) y `sidebar_clickup.webm` (4,8 MB). |
| 1.1 | Retirar secretos | Credenciales fuera de `appsettings.json` y `docker-compose.yml`. Desarrollo → `appsettings.Development.json` (ignorado); Docker → `.env` (ver `.env.example`). |
| 1.2 | Proteger el seed | `/api/v1/admin/seed-database` estaba **sin autenticación**. Ahora exige rol Admin y no se registra en producción. |
| 1.3 | Reparar los tests | `UnitTests` no compilaba: 13 firmas desfasadas y 2 mocks apuntando a métodos que los handlers ya no llaman. Ambos proyectos añadidos a la solución. **42 tests en verde.** |
| 1.4 | Arreglar el CI | `dotnet.yml` → `ci.yml`. El anterior usaba pnpm, ejecutaba un lint inexistente tras `\|\| true` y corría tests que no compilaban. Añadida verificación de secretos versionados. |
| 1.5 | Activar la validación | `ValidationBehavior` añadido al pipeline de MediatR y validadores registrados. FluentValidation estaba instalado, con validadores escritos, y **no se ejecutaba ninguno**. |
| 1.6 | Manejo global de errores | `GlobalExceptionHandler` con ProblemDetails (RFC 7807). En producción nunca se expone el detalle interno. |
| 1.7 | Corregir el README | Documentaba PostgreSQL; el proyecto usa MySQL. Reescrito con el arranque real. |
| + | Rate limiting, health checks, compresión | Política global, y estricta en login y alta pública de tickets. `/health/live` y `/health/ready`. |

### Hallazgos no previstos, corregidos sobre la marcha

**Vulnerabilidad en los tokens JWT.** El refresh token se generaba con **exactamente
los mismos claims** que el access token. Como está firmado con la misma clave y tiene el
mismo issuer y audience, el middleware de autenticación lo aceptaba: era una credencial
de acceso válida durante 7 días, con el rol del usuario. Además, dos llamadas en el mismo
segundo producían tokens idénticos, porque el único claim variable era `exp` (resolución
de segundos), lo que hace imposible revocarlos o auditarlos por separado. El propio
código tenía un `GenerateSecureToken()` privado sin usar: la intención estaba, el cableado
no. Corregido con claims mínimos, `jti` único y un claim `token_type` verificado en el
middleware y al renovar sesión. Cubierto con tests.

**El árbol de dependencias del frontend no resolvía con npm.** Los paquetes `@tiptap/*`
fijan sus peers a versión exacta y estaban desincronizados (`core@3.27.3` contra
extensiones que exigían `3.27.4`). Por eso convivían dos lockfiles en conflicto
(`package-lock.json` y `pnpm-lock.yaml`). Unificados en `3.30.0` con una sola copia de
`@tiptap/core`; eliminado el lockfile de pnpm, ya que `angular.json` declara npm.

**Dependencia fantasma.** `@tiptap/extension-link` se importa en `docs.component.ts`
pero nunca estuvo en `package.json`. Funcionaba por hoisting; una instalación limpia
rompía el build. Declarada.

**Deriva de versiones en NuGet.** `FluentValidation` estaba en 11.9.0 y 11.11.0 según
el proyecto. Unificada.

### Corrección a la auditoría inicial

Dos afirmaciones del informe original eran inexactas:

- Dije que faltaba `pnpm-lock.yaml`. **Sí existía.** El problema real era distinto y
  peor: había dos lockfiles en conflicto porque el árbol no resolvía con npm.
- Dije «0 advertencias de compilación». Ese build estaba en caché y no recompiló nada.
  Hay **42 advertencias reales**, todas preexistentes y concentradas en
  `DataSeederService.cs` (32 `EF1002` por SQL interpolado y 24 de nulabilidad).
  Pendientes para la Fase 2.

---

## 5. Fase 2 — Cimientos de calidad ✅ COMPLETADA

*Objetivo: blindar el aislamiento entre tenants y construir la red de seguridad.*

| # | Tarea | Resultado |
|---|---|---|
| 2.1 | Aislamiento multi-tenant | `ITenantEntity` e `ISoftDeletable` marcan las entidades (21 y 17). `TenantQueryFilter` compone **ambos filtros en una sola expresión**: EF reemplaza el filtro en cada `HasQueryFilter`, así que declararlos por separado habría desactivado el aislamiento en silencio. `TenantDbContext` es la base de los 13 contextos y lee el tenant por consulta, no al construir el modelo. Sin contexto de usuario cierra por defecto. `TenantIsolationVerifier` aborta el arranque si alguna entidad queda fuera. Los filtros manuales se conservan como defensa en profundidad. Ver [ADR-0004](adr/0004-aislamiento-multi-tenant.md). |
| 2.2 | Tests de integración | El proyecto estaba vacío y declaraba Testcontainers de PostgreSQL sobre un proyecto MySQL. `CrmApiFactory` levanta la API real contra MySQL 8.0 en contenedor. **5 flujos**, elegidos por lo que confirman del cableado: entre ellos, que email vacío devuelva 400 y no 401 —lo que demuestra que `ValidationBehavior` está activo— y que el endpoint de seed siga cerrado. |
| 2.3 | Invariantes de dominio | 26 tests sobre las dos máquinas de estados: `WorkTask` (17) y `Ticket` (9). Queda documentado que `Closed` es terminal. |
| 2.4 | Build sin advertencias | De 84 a **0**. Los 32 `EF1002` eran SQL interpolado vía `ExecuteSqlRawAsync`, convertido a la API que parametriza. Las 24 de nulabilidad venían todas de **una sola línea**: `adminUser` recibía el resultado de `FirstOrDefaultAsync`, y eso lo marcaba como posiblemente nulo durante las 500 líneas siguientes. |
| 2.5 | E2E con Playwright | 4 flujos de acceso en 15 s. No levantan backend: interceptan con `page.route`, lo que los hace deterministas. El servidor lo arranca la propia configuración. |
| 2.6 | ADRs | 5 documentos en [`docs/adr/`](adr/). Documentan decisiones ya implementadas cuyo razonamiento no estaba registrado. |

**Estado tras la fase:** build 0/0, **79 tests** (74 unitarios + 5 integración) y 4 E2E.

### Hallazgos no previstos

**La página de login no tiene ningún encabezado.** Al escribir los selectores E2E se vio
que `ui-card-title` renderiza sólo un `<ng-content />`: no hay un `<h1>`–`<h6>` en toda la
pantalla, así que un lector de pantalla no percibe estructura. Es una muestra de que los
64 avisos de a11y del lint son un problema estructural del design system, no una lista de
atributos sueltos que añadir. Va a la Fase 3.2.

**Dos entidades quedan fuera del aislamiento.** `Page` (Docs) y `TeamMember` (Teams) no
tienen `TenantId`: son hijas y se alcanzan por su padre, que sí está filtrado. Hoy no hay
ninguna consulta directa sobre ellas, pero nada lo impide. Anotado como riesgo residual en
el ADR-0004.

---

## 6. Fase 3 — UX/UI de nivel producto ✅ COMPLETADA

*Objetivo: que la interfaz deje de parecer un MVP. Es donde se gana contra ClickUp,
cuya queja número uno es la lentitud y la sobrecarga.*

### Completado

| # | Tarea | Resultado |
|---|---|---|
| 3.1 | Paleta cruda a tokens | **1141 sustituciones en 25 ficheros; 0 clases crudas** fuera de la exclusión deliberada. El sistema no alcanzaba: faltaban `success`, `warning`, `info`, y sobre todo la distinción entre relleno sólido y fondo tenue, sin la cual no había forma de expresar `bg-green-100 text-green-800` y cada pantalla se inventaba los suyos. Ahora cada color tiene cuatro tokens. Había **tres escalas de neutros compitiendo** (zinc, slate, gray); todas colapsan, y con ellas las variantes `dark:` redundantes. Corregido que en oscuro `--card` valía lo mismo que `--background`, así que las tarjetas no se distinguían del lienzo. **Azul confirmado como marca**: los 157 acentos púrpura pasan a `primary`. |
| 3.2 | Accesibilidad | Directiva `appClickable` para los 20 elementos con `(click)` que no existían para el teclado, y 14 etiquetas asociadas a su control. Auditoría **axe sin violaciones críticas ni graves** en rutas públicas, más pruebas de teclado que axe no puede cubrir: completar el login sin ratón e indicador de foco visible. `ui-card-title` emite encabezado con nivel configurable. |
| 3.6 | Deuda de lint (parcial) | 28 imports muertos eliminados. **De 253 avisos a 213.** |

### Completado en la segunda tanda

| # | Tarea | Resultado |
|---|---|---|
| 3.3 | Paleta de comandos (⌘K) | Navegación, búsqueda en tres módulos y 7 acciones. Los estáticos se filtran en memoria y los remotos se **añaden** en lugar de sustituir, para que la lista nunca parpadee a vacío. Ignora acentos. 10 tests + 5 E2E. |
| 3.4 | Rendimiento del tablero | Reversión al rechazar el servidor —antes la interfaz mentía— y **paginación por columna** con «mostrar más». El recorte se hace al repartir, no en la plantilla, porque `cdkDropListData` y los índices del arrastre deben apuntar al mismo array. |
| 3.5 | Estados vacíos y de carga | Los componentes existían **sin usar y rotos**: dos errores que nunca se compilaron. Conectados en los dos tableros. |
| 3.6 | Deuda de lint | De 253 a **177**. Migración oficial a `@if`/`@for`, 28 imports muertos, y `data-table` pasa a ser **genérica en el tipo de sus filas**. |
| 3.8 | Guía de diseño viva | Renderiza los componentes reales, así que no puede quedarse desfasada. Al auditarla destapó **tres fallos de contraste** en los rellenos sólidos que ninguna pantalla había expuesto. |
| 3.10 | axe en rutas autenticadas | Las 6 vistas principales más la paleta y docs. Encontró contraste insuficiente en 6 de 7 —una sola causa en `--muted-foreground`— y botones sin nombre accesible. |

### Lo que queda, y por qué

| # | Tarea | Estado |
|---|---|---|
| 3.7 | Partir `docs.component` | ✅ **Hecho.** Tres modales extraídos a `docs/modals/`. La plantilla pasa de 716 a 580 líneas y el componente de 625 a 571. De paso se elimina la duplicación que escondían: dos copias del flujo de creación desde plantilla y tres de la normalización del id. |
| 3.9 | i18n con `@angular/localize` | ✅ **Hecha.** Español por defecto, inglés como segundo idioma. Ver §6.1. |

### 6.1 i18n: cómo quedó

**Traducción en tiempo de compilación con `@angular/localize`.** Español es el idioma de
origen; inglés se traduce desde `src/locale/messages.en.xlf`. **319 cadenas, ninguna
pendiente.**

`ng build` produce una copia completa por idioma en `dist/web/browser/{es,en}`. Eso es lo
que obligaba a decidir antes de escribir código: cambia el despliegue.

**Qué se gana:** ningún catálogo viaja al navegador y ningún texto puede faltar en
ejecución, porque la traducción se resuelve al compilar.

**Qué se paga:** cada idioma es un artefacto, y cambiar de idioma recarga la página. Como
el token de acceso vive en memoria por seguridad, **cambiar de idioma obliga a iniciar
sesión de nuevo**. Es la consecuencia directa de la opción elegida, no un descuido.

**Cómo se elige.** nginx redirige `/` según `Accept-Language`, con español por defecto.
La redirección es 302 y no 301 a propósito: un permanente se cachea para siempre y dejaría
clavado en `/en/` a quien entrara una vez con el navegador en inglés. Desde la aplicación
se cambia con la paleta de comandos, conservando la ruta.

**Cómo no se pudre.** El CI comprueba que el catálogo esté al día con el código y que no
quede ninguna cadena sin traducir. Sin eso, un texto nuevo saldría en español dentro de la
versión inglesa y nadie lo vería hasta que lo encontrara un usuario.

---

## 7. Fase 4 — Cerrar el hueco competitivo (Semanas 7-18) 🚀

*Sólo lo bloqueante para vender. El resto queda en backlog.*

### Bloque 4.0 — Migraciones ✅ COMPLETADO (2026-08-12)

`Program.cs` aplica `Migrate()` en los trece contextos y aborta el arranque si falla, en
lugar de crear el esquema con `EnsureCreated()` y tragarse los errores. Al hacerlo apareció
que las migraciones no describían el modelo: **la tabla `EntityPermissions` nunca estuvo en
ninguna migración** y se mantenía con siete `ALTER TABLE` crudos en cada arranque, uno de
los cuales había dejado `TagIds` nullable contra lo que dice el modelo. Cerrado con una
migración de puesta al día y retirados los parches.

Las bases creadas con el mecanismo anterior se ponen al día una sola vez con
[`scripts/db/sellar-historial-migraciones.sql`](../scripts/db/sellar-historial-migraciones.sql),
que además corrige la desviación de `TagIds`. Una base vacía no necesita nada.

Verificado con base desde cero, con clon del esquema real sellado, comparando ambos esquemas
columna a columna, y arrancando la aplicación contra los dos. Detalle en
[`CONTINUACION.md`](CONTINUACION.md) §1.

### Bloque 4A — Enriquecer el modelo de tarea (semanas 7-10)

Es el trabajo de mayor retorno: sin esto el producto no entra en una comparativa.

- ✅ **Prioridad** (`Urgent` / `High` / `Normal` / `Low`) en `WorkTask` *(hecho 2026-08-12)*.
  Dominio, migración con relleno de las filas existentes, API con filtro y orden de negocio
  —no alfabético—, interfaz y 26 pruebas nuevas. Al verificarla contra la API real
  aparecieron y se arreglaron dos defectos que la hacían inservible: el tenant del filtro
  global era siempre vacío y las escrituras por handler no llegaban a la base. Detalle en
  [`CONTINUACION.md`](CONTINUACION.md) §4.
- ✅ **Subtareas** *(hecho 2026-08-12)*: `ParentTaskId` autorreferencial con el anidamiento
  limitado a **un solo nivel** —lo que hace que el progreso del padre sea una cuenta y no un
  recorrido de árbol, y descarta los ciclos de raíz—, progreso agregado calculado en SQL sin
  denormalizar, y listas que por defecto devuelven sólo tareas de primer nivel. Las tres
  reglas de anidamiento viven juntas en `WorkTask.ReglasDeAnidamiento`.
- ✅ **Dependencias** *(hecho 2026-08-12)*: entidad `TaskDependency` (bloquea / bloqueada por),
  con la detección de ciclos como **función pura** en el dominio —`DetectorDeCiclos`— para poder
  probarla exhaustivamente sin base de datos, y recorrido iterativo para que un grafo que ya
  tuviera un ciclo no cuelgue la petición. La unicidad la garantiza la base, no sólo el handler.
  Prerrequisito del Gantt de la 4C, ya cubierto.
- **Múltiples responsables**: `AssigneeId` pasa a colección `TaskAssignee`.
- **Checklists** dentro de la tarea.
- **Tareas recurrentes** apoyadas en el worker que ya existe.

Migración de datos: los campos actuales se conservan; `AssigneeId` se migra a una
colección de un elemento.

### Bloque 4B — Campos personalizados (semanas 11-13)

La feature que define a Monday.com y bloqueante para enterprise. Nuevo módulo
`CustomFields` siguiendo el patrón de los 13 existentes: definición por tenant y por
tipo de entidad (texto, número, fecha, selección, selección múltiple, usuario, fórmula),
con valores tipados y aplicables a tareas y proyectos.

### Bloque 4C — Vistas (semanas 14-16)

- **Vista tabla/hoja de cálculo** con edición en línea. Es la vista por defecto de Monday
  y la que más se echa en falta. Reutiliza `data-table` y los campos personalizados de 4B.
- **Vista Gantt/timeline**, apoyada en las dependencias de 4A.
- **Vista carga de trabajo** por persona.
- Completar `SavedView`, que ya existe a medias, para que las vistas se guarden y compartan.

### Bloque 4D — Automatizaciones (semanas 17-18)

Motor de reglas «cuando X, entonces Y» sobre los domain events que ya se emiten. La
infraestructura está: Outbox e integration events funcionan. Falta el motor de reglas y
la interfaz de construcción.

### Backlog priorizado

1. **Time tracking** con temporizador y partes de horas
2. **Ticket → tarea**: la costura de soporte que ni ClickUp ni Monday tienen (§3)
3. Formularios de captura de trabajo
4. Portal de cliente
5. Metas / OKRs
6. API pública documentada
7. SSO / SAML *(bloqueante para enterprise)*
8. Audit log
9. Capa de IA: resumen de hilos, redacción asistida, detección de riesgo en proyectos

---

## 8. Equilibrio desarrollo / documentación / testing

| Actividad | % del esfuerzo | Cómo se hace cumplir |
|---|---|---|
| Desarrollo | 60% | — |
| Testing | 25% | Ningún PR entra sin tests. La cobertura no puede bajar. E2E para todo flujo crítico nuevo. |
| Documentación | 15% | ADR por decisión arquitectónica. README actualizado en el mismo PR. Componente nuevo → entrada en el design system. |

**Definition of Done:**
- [ ] Compila sin advertencias nuevas
- [ ] Tests unitarios de la lógica nueva
- [ ] Test de integración si toca API
- [ ] E2E si toca un flujo crítico
- [ ] `npm run lint` sin errores y sin avisos nuevos
- [ ] Sin clases de paleta cruda de Tailwind
- [ ] Atributos `aria-*` en todo control interactivo
- [ ] Textos vía i18n
- [ ] Documentación actualizada
- [ ] CI en verde

---

## 9. Métricas de seguimiento

| Métrica | Auditoría | Hoy | Fin F2 | Fin F3 | Fin F4 |
|---|---|---|---|---|---|
| Tests backend ejecutándose | 0 | **42** | 90 | 120 | 180 |
| Cobertura backend | 0% | ~15% | 40% | 55% | 70% |
| Specs frontend | 1 | 1 | 15 | 40 | 60 |
| Flujos E2E | 0 | 0 | 3 | 10 | 20 |
| Avisos de lint frontend | sin medir | **253** | 253 | 0 | 0 |
| Avisos de a11y | sin medir | **64** | 64 | 0 | 0 |
| Advertencias de compilación | sin medir | **42** | 0 | 0 | 0 |
| Clases de paleta cruda | 913 | 913 | 913 | 0 | 0 |
| Endpoints sin auth | 1 | **0** | 0 | 0 | 0 |
| Secretos versionados | 3 ficheros | **0** | 0 | 0 | 0 |
| CI en verde | ❌ | **✅** | ✅ | ✅ | ✅ |

---

## 10. Riesgos

| Riesgo | Prob. | Impacto | Mitigación |
|---|---|---|---|
| Fuga de datos entre tenants por un `where` olvidado | **Alta** | **Crítico** | Tarea 2.1. Es la tarea individual más importante del plan. |
| Los secretos retirados ya estaban comprometidos | Baja | Medio | Reevaluado: la clave JWT **nunca llegó al repositorio público**. Lo único publicado fue el password del seed, que coincidía con el de MySQL; ese acoplamiento ya está roto y queda **cambiar el password de MySQL**. Ver `CONTINUACION.md` §3. |
| La migración de paleta (3.1) rompe la UI visualmente | Alta | Medio | Feature por feature con revisión visual; los E2E de la Fase 2 dan la red. |
| Perseguir paridad total con ClickUp | Media | Alto | La Fase 4 está deliberadamente acotada. Todo lo demás va a backlog. |
| La deuda de test crece más rápido de lo que se paga | Media | Alto | El 25% de testing no es negociable; bloquear PRs sin tests en CI. |

---

## 11. Siguiente paso

*Actualizado 2026-08-12, al cerrar el bloque 4.0. Los pasos anteriores —rotación de
secretos, push de la Fase 1, arranque de la Fase 2— están hechos o reevaluados; el detalle
de lo que quedó vivo está en `CONTINUACION.md`.*

1. **Cambiar el password de MySQL.** Es lo único que sigue pendiente de la revisión de
   secretos, y es una acción manual fuera del repositorio.
2. **Mergear el PR #3** (Fases 2 y 3) o decidir explícitamente seguir sobre `fase-3-ux-ui`.
   Cuanto más crezca la Fase 4 encima, más caro es el merge.
3. Seguir el **bloque 4A** por los **múltiples responsables** (`AssigneeId` pasa a colección
   `TaskAssignee`), y después checklists y tareas recurrentes. Prioridad, subtareas y
   dependencias ya están, y dejaron el patrón hecho: dominio → migración → API → interfaz →
   pruebas, y **verificación contra la API real levantada**, que es lo que destapó los dos
   defectos de §4 de `CONTINUACION.md`.
