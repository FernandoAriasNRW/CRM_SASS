# Continuación — punto de partida para la Fase 4

**Escrito:** 2026-08-12 · Al cerrar la Fase 3 y antes de empezar la 4.
**Actualizado:** 2026-08-12 · Resuelto el bloqueo de migraciones (§1) y separado el password del seed (§3).

Este documento existe para retomar el trabajo sin releer el historial. Lo que aquí se
recoge no está en el código ni se deduce de él: son hallazgos, decisiones y trampas que
costaron tiempo descubrir.

---

## 1. El bloqueo de migraciones ✅ RESUELTO

**Ya se aplican migraciones.** `Program.cs` llama a `Migrate()` en los trece contextos y,
si falla, **no arranca**: servir con el esquema a medias es peor que no servir. Antes
llamaba a `EnsureCreated()` y a `CreateTables()` tragándose el error 1050, de modo que el
esquema se creaba pero `__EFMigrationsHistory` quedaba vacía y un campo nuevo no llegaba
nunca a una base ya existente.

### Lo que apareció al mirarlo de cerca

Las migraciones **no describían el modelo**. De los trece contextos, doce estaban al día;
**Identity tenía deriva y era la tabla `EntityPermissions` entera**: nació fuera de las
migraciones, la creaba `EnsureCreated` y crecía a base de siete `ALTER TABLE` crudos
ejecutados en cada arranque (`try { … } catch { }`, líneas 368-374 de entonces). Cerrada con
la migración `20260812140324_AddEntityPermissionTargets`, y retirados los `ALTER`.

Uno de esos parches había dejado un defecto latente: creaba `TagIds` como **NULL** cuando el
modelo la declara **NOT NULL** —es una colección primitiva serializada a JSON, y una lista
vacía es `'[]'`, no `NULL`—. Las filas anteriores al parche quedaron con `NULL`, y a esas el
`JsonContains` de los filtros por etiqueta las descarta **sin error**. El sellado lo corrige
en vez de fijar la desviación para siempre.

### Cómo se pone al día una base ya existente

Las creadas con el mecanismo anterior tienen el esquema completo y el historial vacío, así
que `Migrate()` intentaría crear tablas que ya existen. Hay que **sellar** una vez:

```
mysql -u root -p CrmDb < scripts/db/sellar-historial-migraciones.sql
```

El script es idempotente: crea las dos tablas de historial, normaliza las columnas que antes
se parcheaban al arrancar y declara como aplicadas las 24 migraciones de módulo más la de
`CrmDbContext`. Su lista es una foto de la transición y **no hay que actualizarla** al añadir
migraciones nuevas. La base de desarrollo (`CrmDb`) **ya está sellada**.

Una base vacía no necesita nada: `Migrate()` la construye entera al arrancar.

### Cómo se verificó

| Prueba | Resultado |
|---|---|
| Base vacía, las 13 migraciones desde cero | 25 tablas + 2 de historial (24 y 1 filas) |
| Clon del esquema real, sellado, y después `Migrate()` | No aplica nada; el historial sigue en 24 y 1 |
| Esquema sellado **vs** esquema desde cero | Sin una sola diferencia de columna, tipo ni nullabilidad |
| Arranque real de `ApiHost` contra base vacía y contra la sellada | Ambos levantan y sirven |
| Suite de backend | 120 tests (112 + 8), 0 fallos |

### Dos cosas que conviene saber antes de la 4A

**Los doce contextos de módulo comparten la tabla `__EFMigrationsHistory`**; `CrmDbContext`
usa la suya (`__ef_migrations_history`, configurada en `DatabaseExtensions.cs`). Funciona
porque cada contexto sólo compara contra sus propias migraciones. El riesgo residual es
remoto pero silencioso: dos migraciones de módulos distintos con el mismo identificador
—mismo segundo y mismo nombre— se pisarían. Si en la 4A se van a generar migraciones de
varios módulos de golpe, **darles nombres distintos** basta para evitarlo.

**El orden importa al añadir campos.** Generar la migración (`dotnet ef migrations add`) es
ahora parte de la tarea, no un paso posterior. Comprobación rápida de que nada quedó fuera:

```
dotnet ef migrations has-pending-model-changes --context WorkItemsDbContext --project src/Modules/WorkItems/WorkItems.Infrastructure --startup-project src/Host/ApiHost
```

---

## 2. Estado al cerrar la Fase 3

Todo el trabajo está en la rama `fase-3-ux-ui`, en el **PR #3**, sin mergear.

| | Valor |
|---|---|
| Backend | 0 errores, 0 advertencias · **120 tests** (112 unitarios + 8 integración) |
| Frontend | 27 unitarios · **35 E2E** · lint 0 errores, 171 avisos |
| Idiomas | 329 cadenas, español (origen) e inglés |

Fases 1, 2 y 3 completas. El detalle está en `ESTADO-Y-ROADMAP.md`; las decisiones de
arquitectura, en `adr/`.

---

## 3. La verdad sobre los secretos

Durante varias sesiones repetí que la clave JWT estaba comprometida. **Al verificarlo,
no lo estaba.** El estado real:

| | Estado |
|---|---|
| Clave JWT `YourSuperSecret...` | **Nunca llegó al repositorio público.** No aparece en `origin/main`. |
| `appsettings.Development.json` | **No versionado**, ignorado por git. Sólo local. |
| Commits que llegaron a citarla | Reescritos y **huérfanos**: nunca se subieron. |
| `DataSeederService.cs:112` | ⚠️ Fue público y **coincidía con el password de MySQL**. Ya no: el seed usa una cadena propia sin relación con la infraestructura. |

**Lo único realmente expuesto** era esa cadena del seed, y sólo era un problema porque
coincidía con el password de MySQL de desarrollo. Ese acoplamiento ya está roto.

Pendiente, por orden:

1. **Cambiar el password de MySQL** (queda de tu lado). Sigue siendo necesario: la cadena
   está en el historial público de git para siempre, aunque el código ya no la use.
2. ~~Separar el password del seed del de la base.~~ Hecho. Los usuarios de demostración ya
   sembrados **conservan el hash antiguo** hasta que se borren y se vuelvan a sembrar; son
   cuentas de demo, así que no corre prisa.
3. La clave JWT **no necesita rotación por exposición**. En producción debe ser distinta
   de la de desarrollo, generada con `openssl rand -base64 48`.

---

## 4. Trampas que costaron tiempo (no repetirlas)

**El puerto 4200 es de Docker, no de los tests.** `docker-compose` publica ahí el
frontend. Los E2E usan el **4300** con `reuseExistingServer: false`. Cuando compartían
puerto, la suite se ejecutaba contra el contenedor —una build de producción, sin los
cambios en curso— y fallaba por código correcto. Si el 4300 aparece ocupado,
**identificar el proceso antes de matarlo**: una vez resultaron ser procesos de Docker y
tumbé Docker Desktop entero.

**`ng extract-i18n` da resultados distintos según los finales de línea**, y eso tuvo el CI
en rojo. El catálogo guarda la línea de cada cadena, y el mismo botón sale como `122`
leyendo un template CRLF y como `122,123` leyendo el mismo template en LF. Con
`core.autocrlf=true` en Windows, un catálogo generado en local **nunca** coincide con el que
genera el CI en Linux, así que la comprobación fallaba en cualquier PR aunque nadie hubiera
tocado una cadena. Resuelto forzando LF en el working tree para `*.html`, `*.ts` y `*.xlf`
en `.gitattributes`. Si vuelve a aparecer: comprobar los finales de línea **antes** de
sospechar del catálogo, porque el diff sólo muestra `linenumber` y engaña.

**`GET /api/v1/docs` devuelve un array plano**, no `{items, totalCount}` como el resto de
módulos. Simular la forma equivocada deja la vista vacía **sin ningún error en consola**.

**`page.goto` dentro de la aplicación pierde la sesión.** El token vive en memoria por
decisión de seguridad. En los E2E hay que navegar por la paleta de comandos (`Ctrl+K`),
no con `goto`.

**El endpoint de vistas guardadas (`/views/...`) devuelve un array**, no un objeto
paginado. Simularlo mal bloquea la carga del tablero.

**npm 11 es obligatorio.** Las versiones anteriores podan las dependencias opcionales a la
plataforma anfitriona y dejan el lockfile sin los binarios de Linux. El CI lo comprueba.

---

## 5. Decisiones vigentes que conviene no deshacer sin querer

**El filtro de tenant cierra por defecto.** Sin contexto de usuario el tenant es
`Guid.Empty` y no casa con ninguna fila. Su excepción natural es la autenticación: tres
consultas de `EfUserRepository` llevan `IgnoreQueryFilters()` porque ocurren **antes** de
que exista un tenant. Eso ya rompió el login una vez. Ver ADR-0004.

**No hay máquina de estados en tareas ni tickets.** Se retiró a propósito: mover tarjetas
es decisión de quien gestiona el trabajo. Sólo se rechaza un estado inexistente. Los tests
recorren *todas* las combinaciones para detectar si alguien reintroduce una restricción.

**Los tableros revierten si el servidor rechaza el movimiento.** Sigue haciendo falta
aunque ya no haya reglas de transición: cubre permisos, red y cambios concurrentes.

**El linter no puede ver `appClickable`.** `click-events-have-key-events` busca un
`(keydown)` escrito en la plantilla. Esas reglas **no podrán escalarse a `error`** sin
suprimirlas caso a caso. La verificación real son los tests de la directiva y axe.

**i18n en tiempo de compilación.** Cambiar de idioma recarga la página y, como el token
vive en memoria, **obliga a iniciar sesión de nuevo**. Es consecuencia de la opción
elegida, no un fallo.

---

## 6. Deuda medida que queda

| Qué | Cuánto | Nota |
|---|---|---|
| `any` sin tipar | ~84 avisos | Casi todos interop con TipTap. Los de infraestructura (`data-table`) ya son genéricos. |
| Funciones vacías | 20 avisos | Requieren juicio caso a caso: algunas son noop legítimos. |
| `Page` y `TeamMember` sin `TenantId` | 2 entidades | Son hijas y se alcanzan por su padre, que sí filtra. Una consulta directa no estaría aislada. Riesgo residual del ADR-0004. |
| Bundle inicial | 1.51 MB vs 1.00 MB de presupuesto | Preexistente, sin tocar. |
| Cobertura unitaria de frontend | Baja fuera de `shared/` y `core/` | Los E2E compensan en parte. |
| El sembrado de `Projects` falla en cada arranque | 1 defecto | `DataSeederService.cs:227`, `ArgumentOutOfRangeException`. Escribe tres `Space` y no consigue releerlos: durante el sembrado no hay usuario, el filtro de tenant resuelve a `Guid.Empty` y la consulta vuelve vacía (§5). Preexistente, reproducido en base vacía y con datos. Probablemente el mismo patrón esté en más bloques del seeder. |
| El sembrado de `Tags` falla en cada arranque | 1 defecto | `DbUpdateException` al insertar en `Tags`; parece inserción duplicada sobre datos ya existentes. |

---

## 7. Por dónde seguir

La tarea 4.0 (migraciones) está cerrada, así que el camino está despejado: **el bloque 4A del
roadmap en su orden** —prioridad → subtareas → dependencias → múltiples responsables—. La
prioridad es la más simple y establece el patrón para el resto, migración incluida (§1).

El argumento competitivo sigue siendo el de §3 del roadmap: **el helpdesk integrado**
—`Ticketing` ya existe— es el diferenciador más barato, porque ni ClickUp ni Monday lo
traen de serie.
