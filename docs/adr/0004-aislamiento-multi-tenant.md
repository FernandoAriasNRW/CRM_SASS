# ADR-0004: Aislamiento multi-tenant por filtro global de consulta

**Estado:** Aceptado
**Fecha:** 2026-08-11
**Decisores:** Equipo de arquitectura

## Contexto

La plataforma es multi-tenant con base de datos compartida: todas las organizaciones
conviven en las mismas tablas, discriminadas por una columna `TenantId`.

Hasta la Fase 2, el filtrado era **manual**: cada consulta añadía su propio
`where TenantId == ...`. La columna aparecía en 244 archivos.

Ese diseño tiene un problema que no es de estilo sino de modo de fallo. Si alguien olvida
el `where`:

- No hay error de compilación.
- No hay excepción en ejecución.
- La consulta devuelve datos de otros clientes y la aplicación los muestra con
  normalidad.

Es decir, el fallo es **silencioso y grave a la vez**: la peor combinación posible. Y la
probabilidad no es despreciable, porque basta un descuido entre cientos de consultas, hoy
o dentro de dos años.

## Decisión

El aislamiento pasa a ser el comportamiento por defecto del `DbContext`, no una
responsabilidad de cada consulta.

- `ITenantEntity` marca qué entidades pertenecen a un tenant.
- `ISoftDeletable` marca las que se borran lógicamente.
- `TenantQueryFilter` recorre el modelo y aplica a cada entidad el filtro que le
  corresponda, **componiendo ambos en una sola expresión**.
- `TenantDbContext` es la base de los 13 `DbContext` de módulo y lee el tenant del
  `IUserContext` en cada consulta.
- `TenantIsolationVerifier` recorre el modelo al arrancar y aborta si alguna entidad
  quedó fuera.

Los filtros manuales de los handlers **se conservan** como defensa en profundidad.

### Por qué una sola expresión

EF Core no acumula filtros: cada llamada a `HasQueryFilter` **reemplaza** la anterior.
Declarar el soft delete por un lado y el tenant por otro dejaría activo sólo el último y
desactivaría el aislamiento sin previo aviso. Es exactamente la clase de fallo que este
ADR trata de eliminar, así que se compone explícitamente.

### Por qué se evalúa por consulta y no al construir el modelo

El modelo de EF se compila y se cachea una vez por tipo de contexto. Si el filtro
capturase el `TenantId` como constante, el primer tenant en llegar quedaría horneado en
el modelo y serviría a todos los demás. Referenciar una propiedad del `DbContext` hace que
EF lo traduzca a un parámetro de consulta, de modo que un solo modelo cacheado sirve
correctamente a todos los tenants.

### Por qué cierra por defecto

Sin contexto de usuario (workers en segundo plano, herramientas de diseño) el tenant es
`Guid.Empty`, que no casa con ninguna fila. Un fallo al resolver el tenant deja **sin
datos**, no da acceso a todos. Los procesos que legítimamente cruzan tenants deben
declararlo con `IgnoreQueryFilters()`, que es explícito y auditable con una búsqueda.

## Opciones consideradas

### Opción A: Seguir filtrando a mano

**A favor:** cero trabajo; explícito en el punto de uso.
**En contra:** el modo de fallo descrito arriba. Ninguna revisión de código sostiene 244
puntos indefinidamente.

### Opción B: Base de datos por tenant

**A favor:** el aislamiento más fuerte posible; imposible cruzar datos por error.
**En contra:** migraciones y coste operativo multiplicados por cada cliente. Desproporcionado
para el número de tenants previsto a corto plazo. Sigue siendo la salida natural si algún
cliente lo exige por contrato.

### Opción C: Row-Level Security en MySQL

**A favor:** el motor lo garantiza, no la aplicación.
**En contra:** MySQL no tiene RLS nativo como PostgreSQL; habría que emularlo con vistas y
usuarios por tenant. Complejidad alta y difícil de probar.

### Opción D: Filtro global en EF Core (elegida)

**A favor:** un solo punto de aplicación, imposible de olvidar por consulta, verificable
al arrancar y comprobable con tests.
**En contra:** vive en la aplicación, no en la base: quien acceda a la base por fuera de EF
lo esquiva. Requiere disciplina con `IgnoreQueryFilters()`.

## Análisis de compromisos

La elección real es entre *garantía del motor* (opciones B y C) y *garantía de la
aplicación* (opción D). La primera es más fuerte pero cuesta bastante más y, en el caso de
MySQL, no viene de serie.

La opción D no es tan fuerte, pero convierte el fallo de "silencioso en producción" a
"ruidoso al arrancar", que es la mejora que de verdad importa. El verificador es lo que
sostiene la decisión: sin él, esto sería sólo otra convención.

## Consecuencias

**Más fácil:** escribir consultas nuevas; el aislamiento sale gratis. Añadir una entidad
nueva es seguro por defecto.

**Más difícil:** las operaciones que cruzan tenants legítimamente exigen
`IgnoreQueryFilters()` explícito. Los accesos a la base que no pasen por EF —scripts,
informes externos— quedan fuera del filtro y son responsabilidad de quien los escriba.

**A revisar:** si un cliente exige aislamiento físico por contrato, o si aparecen accesos a
la base fuera de EF con volumen suficiente para justificar mover el control al motor.

## Riesgo residual conocido

`Page` (de Docs) y `TeamMember` (de Teams) **no tienen `TenantId`**: son entidades hijas y
se alcanzan a través de su padre, que sí está filtrado. Una consulta directa sobre ellas
no estaría aislada. Hoy no existe ninguna, pero nada lo impide. Pendiente de resolver
propagando el `TenantId` a las hijas.

## Acciones

1. [x] `ITenantEntity` / `ISoftDeletable` y filtro compuesto
2. [x] `TenantDbContext` como base de los 13 contextos
3. [x] Verificador de arranque que aborta si falta alguna entidad
4. [x] 6 tests sobre SQLite en memoria, incluido el caso de cierre por defecto
5. [ ] Propagar `TenantId` a `Page` y `TeamMember`
6. [ ] Prueba de integración de aislamiento a nivel HTTP con dos tenants reales
