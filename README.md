# CRM SaaS Suite

Plataforma de **gestión de trabajo** (work management) para equipos: espacios, proyectos,
tareas en tablero, tickets de soporte, documentos colaborativos, calendario y chat.
Posicionamiento de producto: competir con ClickUp y Monday.com.

Backend en .NET 9 (monolito modular con Clean Architecture por bounded context),
frontend en Angular 21.

> Estado actual del proyecto y plan de trabajo: [`docs/ESTADO-Y-ROADMAP.md`](docs/ESTADO-Y-ROADMAP.md)

---

## Requisitos

| Herramienta | Versión |
|---|---|
| .NET SDK | 9.0 |
| Node.js | 20 o superior |
| npm | **11 o superior** (ver nota abajo) |
| MySQL | 8.0 |
| Docker | opcional, para `docker compose` y para los tests de integración |

El gestor de paquetes del frontend es **npm**. No usar pnpm ni yarn: el lockfile
de referencia es `web/package-lock.json`.

> **npm 11 o superior es un requisito, no una recomendación.** Las versiones
> anteriores podan las dependencias opcionales a la plataforma en la que se ejecutan
> ([npm/cli#4828](https://github.com/npm/cli/issues/4828)): un `npm install` desde
> Windows con npm 10 deja fuera del lockfile los binarios de Linux de rollup y
> esbuild, y el CI deja de poder construir. El fallo aparece lejos de su causa, al
> arrancar el servidor de los E2E.
>
> Node 20 trae npm 10. Actualiza con `npm i -g npm@11`. Si el lockfile ya se hubiera
> podado, se regenera completo con:
>
> ```bash
> cd web && npx npm@11 install --package-lock-only
> ```
>
> El CI comprueba esto antes de instalar y falla con un mensaje explícito.

---

## Puesta en marcha

### Opción A — Docker (todo el stack)

```bash
cp .env.example .env
```

Editar `.env` con credenciales propias y levantar:

```bash
docker compose up --build
```

- API: `http://localhost:8080`
- Web: `http://localhost:4200`

### Opción B — Local

**1. Base de datos**

MySQL 8.0 escuchando en `localhost:3306`.

**2. Configuración del backend**

Las credenciales **no se versionan**. Configurarlas en `appsettings.Development.json`
(ignorado por git) o en user-secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=CrmDb;Uid=<usuario>;Pwd=<password>;" --project src/Host/ApiHost
```

```bash
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)" --project src/Host/ApiHost
```

La aplicación **no arranca** si falta `Jwt:Key`, si tiene menos de 32 caracteres,
o si falta la cadena de conexión. Es intencional: es preferible fallar al arrancar
que servir tokens que cualquiera puede falsificar.

**3. Backend**

```bash
dotnet run --project src/Host/ApiHost/ApiHost.csproj
```

- API: `http://localhost:5239`
- Documentación (Scalar, sólo en Development): `http://localhost:5239/scalar/v1`
- Health: `/health/live` y `/health/ready`

**4. Frontend**

```bash
cd web && npm install && npm start
```

App: `http://localhost:4200`

**5. Datos de demostración**

Autenticarse como Admin y llamar a `POST /api/v1/admin/seed-database`.
El endpoint exige rol Admin y **no existe en producción**.

---

## Desarrollo

### Backend

```bash
dotnet build CrmSaaS.sln
```

```bash
dotnet test CrmSaaS.sln
```

### Frontend

```bash
cd web && npm run lint
```

```bash
cd web && npm run build
```

La línea base de lint es **0 errores**. Hay ~253 avisos de deuda heredada
(`any`, variables sin usar, accesibilidad) que se reducen por fases; están
documentados en `web/eslint.config.js`.

---

## Estructura

```
src/
  BuildingBlocks/       Primitivas compartidas: dominio, outbox, seguridad, behaviors de MediatR
  Host/ApiHost/         Composición de la aplicación, pipeline HTTP, hubs de SignalR
  Modules/              13 módulos, cada uno con Domain / Application / Infrastructure / Presentation
tests/
  UnitTests/            Tests de dominio y handlers (xUnit + NSubstitute + FluentAssertions)
  IntegrationTests/     Tests de API con WebApplicationFactory
web/                    Frontend Angular 21 (standalone components, Tailwind, Spartan-ng, NgRx + Signals)
docs/                   Estado del proyecto, roadmap y ADRs
```

### Módulos

`Identity` · `Projects` · `WorkItems` · `Ticketing` · `Notifications` · `Calendar`
`Communication` · `Docs` · `Reporting` · `Tags` · `Teams` · `Webhook`

Cada módulo tiene su propio `DbContext`. La comunicación entre módulos es por
integration events despachados con el patrón Outbox, nunca por referencia directa
entre dominios.

---

## Arquitectura

- **Monolito modular.** Separación física por bounded context, preparada para
  extraer servicios si la carga lo justifica. No se empieza por microservicios.
- **CQRS con MediatR.** Pipeline: `ValidationBehavior` → `WebhookDispatchBehavior`
  → `AuthorizationBehavior` → handler.
- **Outbox.** Los eventos de integración se persisten en la misma transacción que
  el cambio de estado y los despacha `OutboxDispatcherWorker`.
- **Multi-tenant.** Todas las entidades llevan `TenantId`.
- **Tiempo real.** SignalR en `/hubs/notifications`, `/hubs/board`, `/hubs/tickets`.

Las decisiones y sus alternativas descartadas están en `docs/adr/`.

---

## Endpoints principales

| Método | Ruta |
|---|---|
| POST | `/api/v1/auth/login` |
| POST | `/api/v1/auth/refresh` |
| GET | `/api/v1/auth/users/me` |
| GET · POST | `/api/v1/projects` |
| GET · POST | `/api/v1/tasks` |
| PATCH | `/api/v1/tasks/{id}/move` |
| POST | `/api/public/v1/tickets` (público, con rate limit) |
| GET | `/api/v1/tickets` |
| PATCH | `/api/v1/tickets/{id}/assign` |
| GET | `/api/v1/notifications` |

La referencia completa está en Scalar, en el entorno de desarrollo.

---

## Seguridad

- Access token de 15 minutos, refresh token de 7 días. Se distinguen por el claim
  `token_type` y el middleware rechaza un refresh token usado como credencial de acceso.
- Rate limiting global, y políticas estrictas en login y en el alta pública de tickets.
- RBAC (`Admin` / `Member`) y ABAC para el cambio de estado de tareas.
- Errores en formato RFC 7807; en producción nunca se expone el detalle interno.
