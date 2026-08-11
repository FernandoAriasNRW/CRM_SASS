# CRM SaaS Suite — Estado y Ruta de Trabajo

**Auditoría:** 2026-08-11 · **Última actualización:** 2026-08-11 (Fase 1 completada)
**Posicionamiento:** plataforma de **work management**, compitiendo con ClickUp y Monday.com
**Alcance:** backend (.NET 9), frontend (Angular 21), testing, CI/CD, UI/UX

---

## 1. Resumen ejecutivo

La arquitectura backend está bien planteada —monolito modular, Clean Architecture por
bounded context, CQRS, Outbox— y es una base sólida sobre la que escalar. El problema
no era el diseño, era la salud operativa: al auditar se encontraron cero tests
ejecutándose, credenciales versionadas y un CI que no podía pasar.

**La Fase 1 ya está implementada** (ver §4). Estado tras ella:

| | Antes | Ahora |
|---|---|---|
| Repositorio git | sólo `web/` | backend + frontend, historia preservada |
| Tests ejecutándose | 0 | 42 en verde |
| Secretos versionados | 3 ficheros | 0 |
| Endpoints sin autenticación | 1 | 0 |
| Validación de entrada | escrita pero nunca ejecutada | activa en el pipeline |
| CI | imposible de pasar | funcional |
| Lint frontend | inexistente | 0 errores, 253 avisos medidos |

Quedan dos frentes grandes: **el aislamiento multi-tenant no está blindado** (§5, Fase 2)
y **el producto no compite todavía en features** con ClickUp o Monday (§3).

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
- Soft delete uniforme vía `HasQueryFilter(e => !e.IsDeleted)`.
- 3 hubs SignalR reales: notificaciones, tablero y tickets.
- Plantillas de proyecto ya existen (`/from-template`, `/save-as-template`).

**Pendiente (detalle y plan en §5):**

| Problema | Estado |
|---|---|
| Multi-tenancy filtrada a mano en 244 archivos, sin query filter global | 🔴 Fase 2 |
| `IntegrationTests` vacío (y con Testcontainers de PostgreSQL, no MySQL) | 🟠 Fase 2 |
| 42 advertencias de compilación en `DataSeederService` (`EF1002`, nulabilidad) | 🟡 Fase 2 |
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

| Problema | Medida | Fase |
|---|---|---|
| Paleta cruda de Tailwind en vez de tokens | 913 usos vs 873 tokenizados (~51%) | 3 |
| Accesibilidad | 64 avisos de lint a11y; 0 atributos `aria-*` en 34 templates | 3 |
| `any` sin tipar | 84 avisos | 3 |
| Variables sin usar | 41 avisos | 3 |
| Sintaxis de control antigua (`*ngIf`) | 33 avisos | 3 |
| Sin i18n (español e inglés mezclados, textos hardcodeados) | — | 3 |
| Sin virtualización de listas (`@angular/cdk` instalado pero sin usar) | 0 usos | 3 |
| Componentes obesos (`docs.component.html` 709 líneas) | — | 3 |
| Specs frontend | 1 (el generado por el CLI) | 2-3 |

### 2.3 Testing

```
tests/UnitTests/         42 tests en verde ✅
tests/IntegrationTests/  vacío, pendiente de construir
web/                     1 spec generado por el CLI
E2E                      inexistente
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

## 5. Fase 2 — Cimientos de calidad (Semanas 1-2) 🟠

*Objetivo: blindar el aislamiento entre tenants y construir la red de seguridad.*

| # | Tarea | Rol | Criterio de aceptación |
|---|---|---|---|
| 2.1 | **Query filter global de tenant.** Interfaz `ITenantEntity`, filtro en `OnModelCreating` de cada `DbContext` leyendo de `IUserContext`. Retirar los filtros manuales. | Backend | Test de integración: un usuario del tenant A consulta y recibe 0 registros del tenant B, en las 12 entidades principales. |
| 2.2 | Levantar `IntegrationTests` con `WebApplicationFactory` + Testcontainers. **Cambiar Testcontainers.PostgreSql por MySql**: el proyecto declara el motor equivocado. Cubrir login, CRUD de proyecto, mover tarea y alta de ticket. | Tester | 4 flujos de API en verde en CI. |
| 2.3 | Tests unitarios de dominio para agregados con invariantes: `Project`, `WorkTask`, `Ticket`, `Team`. | Tester | Cobertura de `*.Domain` ≥ 70%. |
| 2.4 | Sanear `DataSeederService`: 32 `EF1002` (SQL interpolado sin parametrizar) y 10 desreferencias posiblemente nulas. | Backend | 0 advertencias en el build. |
| 2.5 | Playwright E2E: login → dashboard, crear proyecto, mover tarea en el tablero. | Tester | 3 specs en verde, integradas al CI. |
| 2.6 | ADRs en `docs/adr/`: monolito modular, `DbContext` por módulo, Outbox, estrategia multi-tenant, estrategia de tokens. | PM / Arquitecto | 5 ADRs. |

---

## 6. Fase 3 — UX/UI de nivel producto (Semanas 3-6) 🎨

*Objetivo: que la interfaz deje de parecer un MVP. Es donde se gana contra ClickUp,
cuya queja número uno es la lentitud y la sobrecarga. Ejecutar con Claude Design sobre
los tokens ya existentes.*

| # | Tarea | Rol | Criterio de aceptación |
|---|---|---|---|
| 3.1 | **Erradicar la paleta cruda.** Migrar 913 clases `text-slate-*` / `bg-blue-*` a tokens semánticos; ampliar tokens donde falten (success, warning, info, superficie elevada). | Frontend / UI | 0 clases de paleta cruda. Modo oscuro correcto en las 12 features. |
| 3.2 | **Accesibilidad WCAG 2.1 AA.** Resolver los 64 avisos de a11y, focus trap en modales y drawers, navegación completa por teclado, skip link, contraste verificado. Escalar las reglas a `error` en ESLint. | Frontend / UI | axe-core sin violaciones críticas ni serias. E2E de navegación por teclado. |
| 3.3 | **Command palette (⌘K).** Navegación, búsqueda global y acciones rápidas. Expectativa de mercado en 2026 (Linear, Notion, ClickUp). | Frontend / UI | Abre en <100 ms; busca en proyectos, tareas, tickets y docs; 10+ acciones. |
| 3.4 | **Rendimiento percibido.** `cdk-virtual-scroll` en data-table y listas; actualizaciones optimistas en el tablero con rollback. | Frontend | 5.000 filas a 60 fps. Mover una tarjeta se siente instantáneo. |
| 3.5 | Estados vacíos, skeletons y errores consistentes en las 12 features (los componentes existen, falta aplicarlos). | UI/UX | Ninguna vista en blanco durante carga ni ante error. |
| 3.6 | Saldar la deuda de lint: 84 `any`, 41 variables sin usar, 33 `*ngIf`. | Frontend | 0 avisos; reglas escaladas a `error`. |
| 3.7 | Refactor de componentes obesos (`docs`, `tasks`, `tickets`) en subcomponentes. | Frontend | Ningún componente supera 250 líneas. |
| 3.8 | Guía de diseño viva (Storybook o `/design-system`) con los 25 componentes. | UI/UX | Todo componente nuevo se documenta antes de mergear. |
| 3.9 | i18n con `@angular/localize`. Español + inglés. | Frontend | Cambio de idioma sin recarga; 0 textos hardcodeados. |

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
