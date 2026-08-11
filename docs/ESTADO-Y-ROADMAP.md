# CRM SaaS Suite — Estado y Ruta de Trabajo

**Auditoría:** 2026-08-11 · **Última actualización:** 2026-08-11 (Fases 1 y 2 completadas; Fase 3 en curso)
**Posicionamiento:** plataforma de **work management**, compitiendo con ClickUp y Monday.com
**Alcance:** backend (.NET 9), frontend (Angular 21), testing, CI/CD, UI/UX

---

## 1. Resumen ejecutivo

La arquitectura backend está bien planteada —monolito modular, Clean Architecture por
bounded context, CQRS, Outbox— y es una base sólida sobre la que escalar. El problema
no era el diseño, era la salud operativa: al auditar se encontraron cero tests
ejecutándose, credenciales versionadas y un CI que no podía pasar.

**Las Fases 1 y 2 ya están implementadas** (ver §4 y §5). Estado tras ellas:

| | Antes | Ahora |
|---|---|---|
| Repositorio git | sólo `web/` | backend + frontend, historia preservada |
| Tests ejecutándose | 0 | **79 backend** (74 + 5) + **7 frontend** + **8 E2E** |
| Secretos versionados | 3 ficheros | 0 |
| Endpoints sin autenticación | 1 | 0 |
| Validación de entrada | escrita pero nunca ejecutada | activa en el pipeline |
| CI | imposible de pasar | funcional, con E2E y contenedores |
| Lint frontend | inexistente | 0 errores, 213 avisos medidos |

El aislamiento multi-tenant ya está blindado y verificado al arrancar (§5). El frente que
queda es de producto: **no compite todavía en features** con ClickUp o Monday (§3), y la
interfaz sigue siendo de MVP (§6).

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
| Accesibilidad: elementos sin teclado y etiquetas sueltas | eran 55 avisos | ✅ Fase 3.2 |
| Imports muertos | eran 28 | ✅ Fase 3.6 |
| `any` sin tipar | 84 avisos | 🟠 Fase 3.6 |
| Sintaxis de control antigua (`*ngIf`) | 26 avisos | 🟡 Fase 3.6 |
| Funciones vacías | 20 avisos | 🟡 Fase 3.6 |
| Sin i18n (español e inglés mezclados, textos hardcodeados) | — | 🟠 Fase 3.9 |
| Sin virtualización de listas (`@angular/cdk` instalado pero sin usar) | 0 usos | 🟠 Fase 3.4 |
| Componentes obesos (`docs.component.html` 709 líneas) | — | 🟡 Fase 3.7 |
| Specs frontend unitarias | 7 (6 de la directiva + 1 del CLI) | 🟠 Fase 3 |

### 2.3 Testing

```
tests/UnitTests/         74 tests en verde ✅
tests/IntegrationTests/   5 tests contra MySQL en contenedor ✅
web/e2e/                  8 flujos con Playwright, incluida auditoría axe ✅
web/ (unitarios)          7 specs — cobertura aún mínima, pendiente Fase 3
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

## 6. Fase 3 — UX/UI de nivel producto (en curso) 🎨

*Objetivo: que la interfaz deje de parecer un MVP. Es donde se gana contra ClickUp,
cuya queja número uno es la lentitud y la sobrecarga.*

### Completado

| # | Tarea | Resultado |
|---|---|---|
| 3.1 | Paleta cruda a tokens | **1141 sustituciones en 25 ficheros; 0 clases crudas** fuera de la exclusión deliberada. El sistema no alcanzaba: faltaban `success`, `warning`, `info`, y sobre todo la distinción entre relleno sólido y fondo tenue, sin la cual no había forma de expresar `bg-green-100 text-green-800` y cada pantalla se inventaba los suyos. Ahora cada color tiene cuatro tokens. Había **tres escalas de neutros compitiendo** (zinc, slate, gray); todas colapsan, y con ellas las variantes `dark:` redundantes. Corregido que en oscuro `--card` valía lo mismo que `--background`, así que las tarjetas no se distinguían del lienzo. **Azul confirmado como marca**: los 157 acentos púrpura pasan a `primary`. |
| 3.2 | Accesibilidad | Directiva `appClickable` para los 20 elementos con `(click)` que no existían para el teclado, y 14 etiquetas asociadas a su control. Auditoría **axe sin violaciones críticas ni graves** en rutas públicas, más pruebas de teclado que axe no puede cubrir: completar el login sin ratón e indicador de foco visible. `ui-card-title` emite encabezado con nivel configurable. |
| 3.6 | Deuda de lint (parcial) | 28 imports muertos eliminados. **De 253 avisos a 213.** |

### Pendiente

| # | Tarea | Criterio de aceptación |
|---|---|---|
| 3.3 | **Command palette (⌘K).** Navegación, búsqueda global y acciones rápidas. Expectativa de mercado en 2026 (Linear, Notion, ClickUp). | Abre en <100 ms; busca en proyectos, tareas, tickets y docs; 10+ acciones. |
| 3.4 | **Rendimiento percibido.** `cdk-virtual-scroll` en data-table y listas; actualizaciones optimistas en el tablero con rollback. | 5.000 filas a 60 fps. Mover una tarjeta se siente instantáneo. |
| 3.5 | Estados vacíos, skeletons y errores consistentes en las 12 features (los componentes existen, falta aplicarlos). | Ninguna vista en blanco durante carga ni ante error. |
| 3.6 | Resto de la deuda de lint: 84 `any`, 26 `*ngIf`, 20 funciones vacías, 13 variables sin usar. | 0 avisos; reglas escaladas a `error`. |
| 3.7 | Refactor de componentes obesos (`docs`, `tasks`, `tickets`) en subcomponentes. | Ningún componente supera 250 líneas. |
| 3.8 | Guía de diseño viva (Storybook o `/design-system`) con los 25 componentes. | Todo componente nuevo se documenta antes de mergear. |
| 3.9 | i18n con `@angular/localize`. Español + inglés. | Cambio de idioma sin recarga; 0 textos hardcodeados. |
| 3.10 | Auditar con axe las rutas autenticadas. Hoy sólo se cubren las públicas, porque el resto exige sesión y datos sembrados. | E2E autenticados + axe en las 12 features. |

### El linter no puede ver la directiva de teclado

`click-events-have-key-events` analiza la plantilla buscando un `(keydown)` escrito allí,
así que no ve el que aporta `appClickable` y sigue avisando en los 20 sitios. Es un límite
de la herramienta, no trabajo pendiente: la verificación real son 6 tests unitarios de la
directiva y la auditoría axe. Conviene tenerlo presente antes de escalar esas reglas a
`error` en la tarea 3.6, porque no podrán escalarse sin suprimirlas caso a caso.

---

## 7. Fase 4 — Cerrar el hueco competitivo (Semanas 7-18) 🚀

*Sólo lo bloqueante para vender. El resto queda en backlog.*

### Bloque 4A — Enriquecer el modelo de tarea (semanas 7-10)

Es el trabajo de mayor retorno: sin esto el producto no entra en una comparativa.

- **Prioridad** (`Urgent` / `High` / `Normal` / `Low`) en `WorkTask`.
- **Subtareas**: `ParentTaskId` autorreferencial, con profundidad limitada y agregación
  de progreso al padre.
- **Dependencias**: entidad `TaskDependency` (bloquea / bloqueada por), con detección
  de ciclos en el dominio. Es el prerrequisito del Gantt.
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
| Los secretos retirados ya estaban comprometidos | Media | Crítico | Se movieron, **falta rotarlos**. Asumir que la clave JWT anterior es pública. |
| La migración de paleta (3.1) rompe la UI visualmente | Alta | Medio | Feature por feature con revisión visual; los E2E de la Fase 2 dan la red. |
| Perseguir paridad total con ClickUp | Media | Alto | La Fase 4 está deliberadamente acotada. Todo lo demás va a backlog. |
| La deuda de test crece más rápido de lo que se paga | Media | Alto | El 25% de testing no es negociable; bloquear PRs sin tests en CI. |

---

## 11. Siguiente paso

1. **Rotar** la clave JWT y el password de base de datos. Retirarlos del código no basta:
   estuvieron versionados y hay que asumirlos comprometidos.
2. Revisar y **hacer push** del commit de Fase 1 (§4). La historia del frontend fue
   reescrita para colgar de `web/`, así que el push a `origin/main` requiere `--force-with-lease`
   y coordinación con cualquiera que tenga el repo clonado.
3. Arrancar la **Fase 2** por la tarea 2.1, el query filter global de tenant.
