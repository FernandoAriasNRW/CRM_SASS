# Continuación — punto de partida para la Fase 4

**Escrito:** 2026-08-12 · Al cerrar la Fase 3 y antes de empezar la 4.

Este documento existe para retomar el trabajo sin releer el historial. Lo que aquí se
recoge no está en el código ni se deduce de él: son hallazgos, decisiones y trampas que
costaron tiempo descubrir.

---

## 1. Bloqueo que hay que resolver ANTES de tocar el modelo 🔴

**La aplicación no aplica migraciones. Crea el esquema con `EnsureCreated()`.**

`Program.cs` (~línea 340) llama a `EnsureCreated()` y a `CreateTables()`, no a `Migrate()`.
Existen migraciones iniciales por módulo, pero **nunca se ejecutan**: la tabla
`__EFMigrationsHistory` de la base de desarrollo tiene **0 filas**.

Por qué bloquea la Fase 4:

- `EnsureCreated()` crea el esquema **sólo si la base no existe**. Sobre una base ya
  creada no hace nada. Añadir `Priority` a `WorkTask` no crearía la columna, y la
  aplicación fallaría en ejecución con «unknown column».
- `EnsureCreated` y `Migrate` son **mutuamente excluyentes**. Una base creada con el
  primero no tiene historial, así que cambiar a `Migrate()` intentaría aplicar la
  migración inicial sobre tablas que ya existen y fallaría.

Toda la Fase 4 añade campos y entidades. Sin esto, nada de lo que se construya llega a la
base de datos de nadie que ya tenga el sistema montado.

### Qué hay que decidir

| Opción | Coste | Consecuencia |
|---|---|---|
| **Migrar de verdad**: cambiar a `Migrate()` y sellar el historial con la migración inicial en las bases existentes | Medio | Es lo correcto a partir de aquí. Requiere un `INSERT` en `__EFMigrationsHistory` por base ya creada, o recrearlas. |
| Seguir con `EnsureCreated` y recrear la base en cada cambio | Bajo hoy | Inviable en cuanto haya datos que conservar. |

**Recomendación:** resolverlo como tarea 4.0, antes que cualquier campo nuevo. Es barato
ahora y caro cuando haya tres módulos con cambios pendientes.

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
| `DataSeederService.cs:112` | ⚠️ **Sí es público**: `PasswordHash.Create("Secure123*")` |

**Lo único realmente expuesto** es esa cadena del seed. Y el problema es que coincide con
el password de MySQL de desarrollo.

Pendiente, por orden:

1. Cambiar el password de MySQL, porque la cadena está publicada aunque sea por otro motivo.
2. Separar el password del seed del de la base, que es lo que convirtió un dato de demo en
   un problema. Es un cambio de una línea.
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

---

## 7. Por dónde seguir

**Tarea 4.0 (nueva):** resolver la estrategia de migraciones. Ver §1.

Después, el bloque 4A del roadmap en su orden: prioridad → subtareas → dependencias →
múltiples responsables. La prioridad es la más simple y establece el patrón para el resto.

El argumento competitivo sigue siendo el de §3 del roadmap: **el helpdesk integrado**
—`Ticketing` ya existe— es el diferenciador más barato, porque ni ClickUp ni Monday lo
traen de serie.
