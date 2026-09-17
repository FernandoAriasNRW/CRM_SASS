# CRM SaaS Suite

Monolito modular .NET 9 + Angular 21. Doce módulos con su propio `DbContext`, MediatR/CQRS,
MySQL con Pomelo y aislamiento por inquilino mediante filtros globales.

## Antes de tocar código

**Lee `docs/ESTANDAR-DE-CODIGO.md`** y sigue la skill del proyecto
`.claude/skills/estandar-de-codigo/SKILL.md`, que resume lo que hay que hacer y, sobre todo, **qué
hay que comprobar antes de dar un cambio por bueno**.

Lo mínimo que hay que saber:

- **Identificadores en inglés** (también rutas, campos JSON, tablas y columnas), con el glosario
  del documento. **Comentarios, XML docs y textos de interfaz en español.**
- **Un concepto, un nombre.** Si no está en el glosario, se añade ahí en el mismo cambio.
- **El inquilino y el usuario salen de `IUserContext`**, nunca de los claims leídos a mano.
- **Módulos aislados:** ninguno referencia a otro; lo que cruza se declara como puerto en
  BuildingBlocks y lo satisface el host.
- **Migraciones:** renombrar con `RenameTable`/`RenameColumn`, nunca borrar y crear.
- **i18n:** re-extraer el catálogo **al final** del cambio, nunca antes de seguir editando.

## Estructura

| Ruta | Qué hay |
|---|---|
| `src/BuildingBlocks/` | Contratos y utilidades transversales: CQRS, autorización, filtros de inquilino, almacenamiento, outbox |
| `src/Modules/<X>/` | Un módulo: `Domain`, `Application`, `Infrastructure`, `Presentation` |
| `src/Host/ApiHost/` | La API. `Startup/` (arranque por pasos), `Seeding/` (un sembrador por módulo), adaptadores que cruzan módulos |
| `web/` | Angular. `features/`, `core/`, `shared/`; i18n compilado con origen en español |
| `tests/` | `UnitTests` (xUnit) e `IntegrationTests` (Testcontainers, necesitan Docker) |
| `tools/` | Herramientas del estándar: renombrado con Roslyn, comparación de contratos, división de ficheros, detector de identificadores en español |
| `docs/` | Auditoría, estado y roadmap, estándar de código, decisiones (`adr/`) |

## Comandos

```bash
dotnet build CrmSaaS.sln
dotnet test tests/UnitTests
dotnet test tests/IntegrationTests            # necesita Docker
docker compose up -d --build api              # la API en http://localhost:8080
cd web && npm start                           # el frontend en http://localhost:4200
cd web && npm run lint && npx ng build
cd web && npm run i18n:extract                # al final del cambio, nunca antes
```

## Estado

`docs/ESTADO-Y-ROADMAP.md` y `docs/AUDITORIA.md` §14 llevan lo hecho y lo pendiente, con los
defectos medidos que siguen abiertos. El paso de nombres a inglés va por bloques: el plan y lo ya
hecho están en `docs/ESTANDAR-DE-CODIGO.md` §5 y §6.
