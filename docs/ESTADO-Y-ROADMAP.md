# CRM SaaS Suite — Estado Actual y Ruta de Trabajo

**Fecha de auditoría:** 2026-08-11
**Alcance:** backend (.NET 9 modular monolith), frontend (Angular 21 standalone), testing, CI/CD, UI/UX
**Método:** inspección estática del repo + compilación real de ambos stacks + ejecución de la suite de tests

---

## 1. Resumen ejecutivo

El proyecto tiene **buenos huesos y mala salud operativa**. La arquitectura backend (modular monolith, Clean Architecture por bounded context, CQRS con MediatR, Outbox) está bien planteada y **compila limpia: 0 errores, 0 advertencias**. El frontend compila y produce un bundle de 2.4 MB.

Pero por debajo hay tres problemas que bloquean cualquier ambición de "CRM competitivo":

| # | Hallazgo | Severidad |
|---|----------|-----------|
| 1 | **Cero tests ejecutándose.** Los proyectos de test no están en la solución y `UnitTests` no compila (13 errores CS7036). `IntegrationTests` es un `.csproj` vacío. | 🔴 Crítico |
| 2 | **Secretos reales versionados** en `appsettings.json` (password de BD, clave JWT). Endpoint `/api/v1/admin/seed-database` **sin autenticación**. | 🔴 Crítico |
| 3 | **No es un CRM.** El dominio no tiene Contacto, Cuenta, Lead, Oportunidad ni Pipeline. Es una suite de gestión de trabajo tipo ClickUp con nombre de CRM. | 🔴 Estratégico |

Además: la validación FluentValidation existe pero **nunca se ejecuta** (no hay `ValidationBehavior` en el pipeline MediatR), el CI está roto de raíz, y ~51% del styling ignora el design system.

**Veredicto:** producto en estado MVP avanzado con deuda de calidad severa. Antes de añadir features hay que estabilizar. La ruta propuesta invierte 4 semanas en cimientos y luego 12 en diferenciación de producto.

---

## 2. Inventario del estado actual

### 2.1 Backend — .NET 9

**Estructura.** 452 archivos `.cs`. 13 módulos, cada uno con separación física estricta en 4 proyectos (`Domain` / `Application` / `Infrastructure` / `Presentation`):

`Identity` · `Projects` · `WorkItems` · `Ticketing` · `Notifications` · `Calendar` · `Communication` · `Docs` · `Reporting` · `Tags` · `Teams` · `Webhook`

**Lo que está bien:**
- Separación de contextos limpia y consistente. Cada módulo tiene su propio `DbContext` — buena base para extraer microservicios más adelante si hiciera falta.
- Outbox pattern implementado con `outbox_messages` + `OutboxDispatcherWorker`.
- Domain events y integration events separados (`BuildingBlocks.Contracts/IntegrationEvents`).
- Soft delete vía `HasQueryFilter(e => !e.IsDeleted)` aplicado consistentemente.
- Endpoints agrupados con `RequireAuthorization()` a nivel de grupo — no hay endpoints huérfanos sin proteger salvo los señalados abajo.
- 3 hubs SignalR reales (notifications, board, tickets), no simulados.

**Lo que está mal:**

| Problema | Evidencia | Impacto |
|---|---|---|
| Validación muerta | Hay validators FluentValidation en Identity, Projects, etc. Pero el pipeline MediatR sólo registra `AuthorizationBehavior` y `WebhookDispatchBehavior`. No existe `ValidationBehavior` en ningún archivo del repo. | Entrada sin validar llega al dominio. Errores 500 donde debería haber 400. |
| Secretos en repo | `appsettings.json`: `Password=<redactado>`, `Jwt:Key = "<clave-redactada>"` | Cualquiera con acceso al repo puede firmar JWTs válidos. |
| Seed sin auth | `Program.cs:290` — `MapPost("/api/v1/admin/seed-database")` sin `.RequireAuthorization()` | Un POST anónimo puede reinicializar datos. |
| Sin manejo global de errores | No hay `UseExceptionHandler` ni `ProblemDetails` en el pipeline. | Stack traces potencialmente expuestos; respuestas de error inconsistentes. |
| Sin rate limiting | No hay `AddRateLimiter`. El endpoint público de tickets es abusable. | Vector de DoS y spam. |
| Sin health checks | No hay `AddHealthChecks`. | No se puede orquestar (K8s liveness/readiness). |
| Sin compresión ni caché | No hay `UseResponseCompression` ni `OutputCache`. | Latencia y ancho de banda innecesarios. |
| Multi-tenancy manual | `TenantId` aparece en 244 archivos, filtrado a mano en cada query. No hay query filter global de tenant. | **Un solo `where` olvidado = fuga de datos entre tenants.** Riesgo sistémico. |
| Deriva de documentación | README dice PostgreSQL; `appsettings.json` configura MySQL (`Provider: "MySql"`, puerto 3306). | Onboarding roto. |

### 2.2 Frontend — Angular 21

**Estructura.** 96 archivos `.ts`, 34 templates HTML, ~15k líneas. Standalone components, lazy routing, NgRx + Signals, PWA con service worker.

Features enrutadas: `home` · `dashboard` · `projects` · `tasks` · `tickets` · `chat` · `calendar` · `reports` · `teams` · `docs` · `profile` · `admin` (+ `login` y `support` público).

**Lo que está bien:**
- Stack moderno y coherente: Angular 21, Tailwind, Spartan-ng (ShadCN para Angular), ng-icons/lucide, TipTap para docs.
- Lazy loading en todas las rutas. Bundle inicial razonable.
- Design tokens HSL correctamente definidos en `styles.scss` con modo oscuro (`.dark`), cableados a Tailwind vía `tailwind.config.js`.
- Token de acceso **en memoria, no en localStorage** (`auth-signal.store.ts:40`) — decisión de seguridad correcta y deliberada.
- Interceptores de auth y error ya existen.
- Biblioteca de 25 componentes compartidos en `shared/ui` (button, card, modal, drawer, data-table, skeleton, empty-state, toast...).

**Lo que está mal:**

| Problema | Evidencia | Impacto |
|---|---|---|
| **Design system a medias** | 913 clases de paleta cruda (`text-slate-400`, `bg-blue-500`...) vs 873 usos de tokens semánticos. ~51% del styling ignora el sistema. | El modo oscuro se rompe donde se usó paleta cruda. Rebranding imposible. Inconsistencia visual. |
| **Accesibilidad nula** | **0 atributos `aria-*` en los 34 templates.** Sin focus management en modales/drawers, sin `role`, sin skip links. | Incumple WCAG. Bloquea ventas a enterprise y sector público. |
| Sin i18n | Textos hardcodeados, mezcla de español e inglés en la UI. Sin `@angular/localize` ni librería de traducción. | Bloquea expansión a mercados no hispanohablantes. |
| Sin virtualización | 0 usos de `cdk-virtual-scroll`. `@angular/cdk` ya está instalado pero sin usar para esto. | Listas grandes (>500 filas) congelarán el navegador. |
| Test único | 1 solo `.spec.ts` (`app.component.spec.ts`, el generado por el CLI). `angular.json` tiene `skipTests: true` en todos los schematics. | Cero red de seguridad en refactors. |
| Sin E2E | No hay Playwright, Cypress ni carpeta `e2e`. | Ningún flujo crítico verificado end-to-end. |
| Componentes obesos | `docs.component.html` 709 líneas, `docs.component.ts` 624 líneas. | Difíciles de testear y mantener. |
| Sin linter | No existe script `lint` en `package.json` ni configuración ESLint. | Sin control de calidad automatizado. |

### 2.3 Testing — estado real

```
tests/UnitTests/        7 archivos .cs  → NO COMPILA (13 errores CS7036)
tests/IntegrationTests/ solo el .csproj → VACÍO
Ninguno de los dos está referenciado en CrmSaaS.sln
web/                    1 spec generado por el CLI
E2E                     inexistente
```

Los errores de `UnitTests` son todos de **firma desactualizada**: el dominio evolucionó (`Project.Create` ganó `estimatedEndDate`, `GetTasksQuery` ganó `UserId`, `RefreshTokenCommandHandler` ganó `IUserRepository`) y los tests nunca se actualizaron. Son tests escritos una vez y abandonados. **Cobertura efectiva: 0%.**

### 2.4 CI/CD

`.github/workflows/dotnet.yml` tiene 3 jobs bien intencionados (backend, frontend, security-scan) pero **ninguno puede pasar**:

- `dotnet test tests/UnitTests/UnitTests.csproj --no-build` → falla, el proyecto no compila y además no está en la solución que se construyó.
- `pnpm install --frozen-lockfile` en `web/` → **no existe `pnpm-lock.yaml`**, el repo usa npm (`package-lock.json`).
- `pnpm run lint` → no existe el script (mitigado con `|| true`, lo cual también significa que el lint nunca aporta nada).

El repo tiene 3 commits y 20 archivos modificados sin commitear. No hay ramas ni PRs — el workflow nunca se ha ejercitado de verdad.

---

## 3. El problema estratégico: esto no es un CRM

Revisando **todas** las entidades de dominio del proyecto, no existe ninguna de estas:

`Contact` · `Account` / `Company` · `Lead` · `Deal` / `Opportunity` · `Pipeline` / `Stage` · `Activity` · `Quote` · `Product` · `Campaign`

Lo que sí existe: `Project`, `Space`, `Folder`, `WorkTask`, `Ticket`, `Document`, `Page`, `Team`, `Tag`, `CalendarEvent`, `Conversation`, `Message`.

Eso es un **work management suite** — el commit más reciente lo confirma: *"se ajusta el UI/UX para acercarlo a la fluidez de clickup"*. Es un ClickUp, no un HubSpot.

Esto no es necesariamente malo, pero obliga a una decisión de producto que no puede seguir difiriéndose:

**Opción A — Ser un CRM de verdad.** Añadir el bounded context de ventas (Contactos, Cuentas, Leads, Pipeline de Oportunidades con drag & drop, Actividades, Cotizaciones). Es ~8-10 semanas de trabajo y compite en un mercado saturado (Salesforce, HubSpot, Pipedrive, Zoho).

**Opción B — Ser un PSA / Client Work Platform.** Aceptar la base actual y posicionarse donde ya se es fuerte: proyectos + tickets + docs + tiempo + facturación para agencias y consultoras. Compite con Teamwork, Scoro, Accelo. Mercado menos saturado, y el 70% del producto ya está construido. Faltaría: time tracking, presupuestos/facturación, portal de cliente.

**Opción C — Híbrido "de lead a entrega".** Un pipeline de ventas ligero que al ganarse convierte la oportunidad en proyecto automáticamente. Es el diferenciador real: nadie hace bien la costura entre CRM y ejecución. Coste intermedio (~6 semanas para el módulo Sales mínimo) y narrativa de venta única.

**Recomendación: Opción C.** Aprovecha lo construido, añade la palabra "CRM" con legitimidad, y el handoff automático lead→proyecto es una feature que ni Salesforce ni ClickUp resuelven bien. Pero esta decisión es del negocio, no técnica — y condiciona todo el roadmap de la Fase 3 en adelante.

---

## 4. Ruta de trabajo

Cuatro fases. Las fases 1 y 2 son innegociables y secuenciales. La 3 y la 4 dependen de la decisión estratégica de §3.

### Fase 1 — Estabilización (Semanas 1-2) 🔴

*Objetivo: que el proyecto sea seguro de tocar. Nada de features.*

| # | Tarea | Rol | Criterio de aceptación |
|---|-------|-----|------------------------|
| 1.1 | Rotar la clave JWT y el password de BD. Mover a User Secrets (dev) y variables de entorno / Key Vault (prod). Purgar del historial git. | Backend | `appsettings.json` sin ningún secreto. Arranque falla explícitamente si falta `Jwt:Key`. |
| 1.2 | Proteger `/api/v1/admin/seed-database`: `.RequireAuthorization(policy => policy.RequireRole("Admin"))` **y** compilarlo sólo en Development. | Backend | Un POST anónimo devuelve 401. En Release el endpoint no existe. |
| 1.3 | Reparar `tests/UnitTests`: actualizar las 13 firmas desactualizadas. Añadir ambos proyectos de test a `CrmSaaS.sln`. | Tester | `dotnet test CrmSaaS.sln` verde con los 7 archivos ejecutándose. |
| 1.4 | Arreglar el CI: sustituir pnpm por npm (o generar `pnpm-lock.yaml`), añadir ESLint + script `lint`, quitar el `|| true`. | Tester / DevOps | Push a una rama → los 3 jobs en verde. |
| 1.5 | Añadir `ValidationBehavior<TRequest,TResponse>` a `BuildingBlocks.Application/Behaviors` y registrarlo en el pipeline MediatR **antes** de `AuthorizationBehavior`. | Backend | Un comando inválido devuelve 400 con `ValidationProblemDetails`, no 500. |
| 1.6 | Añadir `UseExceptionHandler` global con `ProblemDetails` (RFC 7807). | Backend | Ninguna excepción no controlada expone stack trace. |
| 1.7 | Corregir el README: MySQL, no PostgreSQL. Documentar el arranque real. | PM / Docs | Un dev nuevo levanta el entorno siguiendo el README sin preguntar nada. |

**Salida de fase:** build verde, tests verdes, CI verde, cero secretos, cero endpoints abiertos.

---

### Fase 2 — Cimientos de calidad (Semanas 3-4) 🟠

*Objetivo: red de seguridad y blindaje multi-tenant antes de escalar features.*

| # | Tarea | Rol | Criterio de aceptación |
|---|-------|-----|------------------------|
| 2.1 | **Query filter global de tenant.** Interfaz `ITenantEntity`, filtro aplicado en `OnModelCreating` de cada `DbContext` leyendo de `IUserContext`. Eliminar los filtros manuales. | Backend | Test de integración: usuario del tenant A consulta y recibe 0 registros del tenant B, en las 12 entidades principales. |
| 2.2 | Levantar `IntegrationTests` con `WebApplicationFactory` + Testcontainers (MySQL). Cubrir: login, CRUD de proyecto, mover tarea, crear ticket público. | Tester | 4 flujos end-to-end de API en verde en CI. |
| 2.3 | Tests unitarios de dominio para los agregados con invariantes: `Project`, `WorkTask`, `Ticket`, `Team`. | Tester | Cobertura de `*.Domain` ≥ 70%. |
| 2.4 | Rate limiting: política global + política estricta en `/api/public/v1/tickets` y `/api/v1/auth/login`. | Backend | Un test verifica 429 tras superar el umbral. |
| 2.5 | Health checks (`/health/live`, `/health/ready`) con chequeo de BD. Response compression. | Backend | `docker-compose up` → healthcheck pasa. |
| 2.6 | Playwright E2E: instalar y cubrir 3 flujos críticos (login → dashboard, crear proyecto, mover tarea en el board). | Tester | 3 specs en verde, integrados al CI. |
| 2.7 | Documentar decisiones arquitectónicas en ADRs (`docs/adr/`): modular monolith, DbContext por módulo, Outbox, estrategia multi-tenant. | PM / Arquitecto | 4 ADRs en formato estándar. |

**Salida de fase:** aislamiento de tenants probado, ~40% de cobertura efectiva, E2E funcionando.

---

### Fase 3 — UX / UI de nivel producto (Semanas 5-8) 🎨

*Objetivo: que la interfaz deje de parecer un MVP. Ejecutar con Claude Design sobre los tokens ya existentes.*

| # | Tarea | Rol | Criterio de aceptación |
|---|-------|-----|------------------------|
| 3.1 | **Erradicar la paleta cruda.** Migrar las 913 clases `text-slate-*` / `bg-blue-*` a tokens semánticos. Ampliar los tokens donde falten (success, warning, info, surface elevado). | Frontend / UI | `grep` de paleta cruda en `src/app` → 0 resultados. Modo oscuro perfecto en las 12 features. |
| 3.2 | **Accesibilidad WCAG 2.1 AA.** `aria-*` en todos los controles, focus trap en modales y drawers, navegación completa por teclado, skip link, contraste verificado. | Frontend / UI | Auditoría axe-core sin violaciones críticas ni serias. Test E2E de navegación por teclado. |
| 3.3 | **Command palette (⌘K).** Navegación, búsqueda global y acciones rápidas. Es la expectativa de mercado en 2026 (Linear, Notion, ClickUp lo tienen todos). | Frontend / UI | Abre en <100ms, busca en proyectos/tareas/tickets/docs, ejecuta 10+ acciones. |
| 3.4 | **Virtualización + optimista.** `cdk-virtual-scroll` en data-table y listas. Actualizaciones optimistas en el board Kanban con rollback. | Frontend | Lista de 5.000 filas scrollea a 60fps. Mover una tarjeta se siente instantáneo. |
| 3.5 | Estados vacíos, skeletons y errores consistentes en las 12 features (los componentes ya existen, falta aplicarlos en todas partes). | UI/UX | Ninguna vista muestra pantalla en blanco durante la carga ni ante un error. |
| 3.6 | Refactor de los componentes obesos (`docs`, `tasks`, `tickets`) en subcomponentes de <200 líneas. | Frontend | Ningún componente supera 250 líneas. |
| 3.7 | Guía de diseño viva: Storybook o página `/design-system` con los 25 componentes documentados. | UI/UX | Todo componente nuevo se documenta antes de mergear. |
| 3.8 | i18n con `@angular/localize`. Extraer todos los textos. Español + inglés. | Frontend | Cambio de idioma sin recarga. 0 textos hardcodeados. |

**Salida de fase:** producto visualmente competitivo, accesible, y con la fluidez que el mercado espera.

---

### Fase 4 — Diferenciación de producto (Semanas 9-20) 🚀

*Condicionado a la decisión de §3. Lo siguiente asume la **Opción C (híbrido)**.*

**Bloque 4A — Módulo Sales (semanas 9-14).** Nuevo bounded context `Sales` siguiendo exactamente el patrón de los 13 módulos existentes:
- Agregados: `Contact`, `Account`, `Lead`, `Opportunity`, `Pipeline`, `Stage`, `Activity`.
- Pipeline visual con drag & drop (reutilizar el motor del board Kanban de WorkItems).
- **Conversión Oportunidad→Proyecto**: al marcar `Won`, un integration event vía Outbox crea el proyecto con su plantilla, equipo y tareas iniciales. *Este es el diferenciador.*
- Timeline unificado de actividad por contacto/cuenta.

**Bloque 4B — Motor de automatización (semanas 15-17).** Reglas "cuando X, entonces Y" sobre los domain events que ya se emiten. La infraestructura (Outbox + integration events) ya está: falta el motor de reglas y la UI de constructor. Es la feature #1 en las comparativas de CRM de 2026.

**Bloque 4C — Inteligencia (semanas 18-20).** Sobre los datos ya capturados: lead scoring, resumen de conversaciones e hilos de tickets, redacción asistida de emails, detección de riesgo en proyectos. Diferenciador de precio, no sólo de features.

**Backlog priorizado tras la fase 4:**
1. Time tracking + facturación (crítico para el posicionamiento PSA)
2. Portal de cliente (acceso externo a proyectos y tickets)
3. Campos personalizados por entidad (bloqueante para enterprise)
4. Vistas guardadas y compartidas (`SavedView` ya existe en el dominio — está a medias)
5. API pública + documentación para integraciones
6. SSO / SAML (bloqueante para enterprise)
7. Audit log

---

## 5. Equilibrio desarrollo / documentación / testing

La regla operativa para las 20 semanas, y la razón por la que las fases 1-2 no se saltan:

| Actividad | % del esfuerzo | Cómo se hace cumplir |
|---|---|---|
| Desarrollo | 60% | — |
| Testing | 25% | Ningún PR entra sin tests. Cobertura no puede bajar. E2E para todo flujo crítico nuevo. |
| Documentación | 15% | ADR por decisión arquitectónica. README actualizado en el mismo PR. Componente nuevo → entrada en el design system. |

**Definition of Done** (aplicar desde ya):
- [ ] Código compila sin warnings
- [ ] Tests unitarios de la lógica nueva
- [ ] Test de integración si toca API
- [ ] E2E si toca un flujo crítico
- [ ] Sin clases de paleta cruda de Tailwind
- [ ] Atributos `aria-*` en todo control interactivo
- [ ] Textos vía i18n
- [ ] Documentación actualizada
- [ ] CI verde

---

## 6. Métricas de seguimiento

| Métrica | Hoy | Fin Fase 2 | Fin Fase 3 | Fin Fase 4 |
|---|---|---|---|---|
| Cobertura de tests backend | 0% | 40% | 55% | 70% |
| Specs frontend | 1 | 15 | 40 | 60 |
| Flujos E2E | 0 | 3 | 10 | 20 |
| Violaciones a11y (axe) | sin medir | medido | 0 críticas/serias | 0 |
| Clases de paleta cruda | 913 | 913 | 0 | 0 |
| Endpoints sin auth | 1 | 0 | 0 | 0 |
| Secretos en repo | 2 | 0 | 0 | 0 |
| CI en verde | ❌ | ✅ | ✅ | ✅ |

---

## 7. Riesgos

| Riesgo | Prob. | Impacto | Mitigación |
|---|---|---|---|
| Fuga de datos entre tenants por un `where` olvidado | **Alta** | **Crítico** | Tarea 2.1 (query filter global) — es la tarea individual más importante de todo el plan |
| Los secretos ya están comprometidos | Media | Crítico | Rotar, no sólo mover. Asumir que la clave JWT actual es pública. |
| La migración de paleta (3.1) rompe la UI visualmente | Alta | Medio | Migrar feature por feature con revisión visual; los E2E de la fase 2 dan la red |
| Parálisis por indecisión estratégica (§3) | Media | Alto | Decidir antes de terminar la fase 2. Las fases 1-2 son válidas para cualquier opción. |
| Deuda de test crece más rápido de lo que se paga | Media | Alto | El 25% de testing no es negociable; bloquear PRs sin tests en CI |

---

## 8. Siguiente paso concreto

Empezar por la **Fase 1, tareas 1.1 y 1.2** — son de horas, no de días, y cierran las dos exposiciones de seguridad activas. En paralelo, la decisión de §3 debe entrar en agenda de producto esta semana, porque condiciona todo lo que se construya a partir de la semana 9.
