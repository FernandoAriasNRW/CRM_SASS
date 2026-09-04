# Auditoría del proyecto — 3 de septiembre de 2026

Este documento recoge lo que se midió, no lo que se supone. Cada número de aquí
salió de ejecutar algo; los que no se pudieron medir se dicen como tales.

## 1. Lo que está verde

| Suite | Resultado |
|---|---|
| Unitarias backend (xUnit) | 312 pasan, 0 fallan |
| Integración (Testcontainers + MySQL) | 130 pasan |
| Unitarias frontend (Karma) | 136 pasan |
| E2E (Playwright) | 76 pasan |
| Lint frontend | 0 errores, 167 avisos |
| Build de los dos idiomas | correcto, sin cadenas sin traducir |

La compilación de la solución es correcta.

## 2. Defectos encontrados

### 2.1 La cobertura del CI no medía nada — resuelto

`ci.yml` ejecuta las pruebas con la opción de recolección `XPlat Code Coverage` y
después sube `./coverage` como artefacto. Ninguno de los dos proyectos de test
referenciaba `coverlet.collector`, que es quien implementa ese recolector.

El resultado: `dotnet test` escribía por consola

> No se encuentra ningún objeto datacollector con el nombre descriptivo "XPlat Code Coverage"

…y **devolvía código 0**. El CI seguía verde y publicaba un artefacto vacío. La
cobertura llevaba desde su creación sin medirse, y el aviso estaba a la vista de
cualquiera que leyera el log entero.

Es el mismo patrón que ya mordió en la Fase 4 con `dotnet ef --no-build`: una
herramienta que informa del problema por consola pero no lo señala en el código de
salida. **Un paso de CI que no puede fallar no es un paso de CI.**

Resuelto: `coverlet.collector` en ambos proyectos, `coverage.runsettings` para
decidir qué se mide, `scripts/cobertura.sh` para fusionar las dos suites, y un
umbral que **sí** rompe el build. Se comprobó en las dos direcciones: con umbral 60
devuelve 0, con umbral 95 devuelve 1 y escribe un `::error::`.

### 2.2 Dos referencias de proyecto rotas — resuelto

`tests/IntegrationTests/IntegrationTests.csproj` contenía:

```xml
<ProjectReference Include="....srcModulesCustomFieldsCustomFields.Infrastructure..." />
```

Las barras invertidas se habían perdido en algún reemplazo automático. MSBuild lo
degradaba a aviso (MSB9008) y compilaba igual, porque `ApiHost` arrastra esos dos
proyectos de forma transitiva. Es decir: la referencia estaba rota **y era
redundante**. Se eliminaron las dos líneas; el proyecto compila ahora con 0 avisos.

### 2.3 Un `TypeError` que se lanzaba en cada ejecución sin romper nada — resuelto

Al subir a propósito el umbral del frontend para comprobar que el paso podía
ponerse rojo, el log dejó ver esto:

> `TypeError: this.authStore.getTokenExpiresAt is not a function`

`app.component.spec.ts` sustituía `AuthSignalStore` por un objeto literal con dos
miembros. El efecto del constructor de `SessionManagerService` llama a
`getTokenExpiresAt()`, que el doble no tenía, y **Angular se traga los errores que
se lanzan dentro de un `effect`**: salían por consola y las 136 pruebas seguían en
verde.

El doble llevaba además tiempo desviado de la clase real: exponía `user` donde
`AuthSignalStore` expone `userInfo`.

Se arregló usando el store de verdad —sólo depende de `Router`, ya provisto por
`provideRouter([])`— en lugar de completar el doble a mano. Completarlo sería jugar
al ratón y el gato: el siguiente miembro que se añada a la clase volvería a
romperlo en silencio.

No queda ninguna línea con `ERROR` en el log de la suite.

### 2.4 Un árbol de trabajo de git abandonado

`.claude/worktrees/goofy-khorana-d7c41d` sigue registrado, apuntando a la rama
`claude/goofy-khorana-d7c41d` en el commit `d0650fa`. Contiene una copia completa
del repositorio, incluidos duplicados de los `Dockerfile`. Conviene revisar si tiene
algo que rescatar y, si no, retirarlo.

### 2.5 Escritura entre inquilinos — resuelto

Varios `POST` enlazaban el comando **directamente del cuerpo de la petición**, y esos comandos
llevan `TenantId` dentro:

```csharp
group.MapPost("", async (CreateProjectCommand command, IMediator mediator) =>
{
  var result = await mediator.Send(command);   // el cuerpo manda
```

El `PATCH` y el `DELETE` del mismo archivo sí sacaban el inquilino del token. Sólo el alta se
fiaba del cliente. Comprobado mandando un inquilino ajeno:

```
MI TENANT:       8351c5d2-8655-4400-ac95-64a14c29f360
TENANT AJENO:    a7fc1171-9f81-44d1-a759-e5582c9a22e7
RESPUESTA (201): {"tenantId":"a7fc1171-9f81-44d1-a759-e5582c9a22e7", ...}
```

**Cualquier usuario autenticado podía escribir en los datos de otra organización.** Y tenía un
segundo efecto, el que se veía: quien no mandaba el campo creaba la entidad con `Guid.Empty`,
así que el filtro global no volvía a encontrarla. El alta respondía 201 y la entidad era
invisible en el listado inmediatamente después — de ahí que el panel contara siempre cero.

Corregido en Projects (proyectos, espacios y carpetas), WorkItems, Calendar, Notifications,
Communication y Webhook, usando `IUserContext`, que es la abstracción que ya existía y busca el
claim sin distinguir mayúsculas. Los módulos nuevos —CustomFields, Comments, Automations,
Teams, Docs, Reporting— ya lo hacían bien; era deuda de los antiguos.

### 2.6 Escalada de privilegios en «mover una tarea» — resuelto

`PATCH /api/v1/tasks/{id}/move` recibía `actorId` y `actorRole` **por la cadena de consulta**, y
el manejador autoriza con:

```csharp
if (request.ActorRole != "Admin" && task.AssigneeId != request.ActorId)
    return Result<bool>.Failure("No tiene permisos para mover esta tarea");
```

Añadir `&actorRole=Admin` a la URL saltaba la comprobación entera. Los otros nueve endpoints del
mismo archivo ya leían el actor de las reclamaciones; éste se había quedado atrás. También pasaba
en la edición y el borrado de mensajes de Communication, donde bastaba con poner el
identificador de otra persona para editar sus mensajes.

### 2.7 El hash de la contraseña salía por la API — resuelto

`GET /api/v1/auth/users/me` devolvía:

```json
{"id":"…","email":"admin@acme.com","role":"Admin",
 "passwordHash":"$2a$12$SYuz87JtCjqF04J4LcM1l.nYgqTHtIzWcAcyw1BXnpOHZ1fQFZdBi", …}
```

El `UserDto` llevaba el campo dentro. Nadie lo necesitaba: quien verifica la contraseña al
iniciar sesión trabaja con la entidad de dominio del repositorio, y el emisor de tokens no lo
leía nunca. Es bcrypt con coste 12, así que no es catastrófico, pero es un hash de contraseña
viajando al navegador y quedándose en cachés y registros.

Se quitó el campo del DTO en lugar de filtrarlo en el endpoint: **un dato que no sale de ahí no
se puede filtrar por descuido más adelante.**

### 2.8 Arrastrar en el tablero no funcionaba — resuelto

El tablero llama con `POST /tasks/{id}/move` y cuerpo `{ newStatus }`. La API sólo tenía un
`PATCH` que leía `?status=`. Cada arrastre chocaba con un 405, la tarjeta volvía a su columna
por el camino de revertir y salía un aviso.

Ninguna prueba lo cubría: las de extremo a extremo simulan la respuesta de la API, así que
comprobaban que la tarjeta se movía en la pantalla contra un servidor que no existía. Es el
mismo agujero que dejó pasar lo de los comentarios.

### 2.9 El sembrado se tragaba sus errores — resuelto

`SeedAllAsync` atrapa la excepción de cada módulo por separado —correcto, para que el fallo de
uno no impida sembrar los demás— pero terminaba escribiendo «completed successfully» pasara lo
que pasara, y `Program.cs` registraba el fallo global como **aviso**.

La siembra de Projects llevaba fallando en silencio: la aplicación arrancaba sin un solo
proyecto ni tarea. Ahora se acumulan los módulos que fallaron, se lanza al final con la lista, y
el host lo registra como error.

## 3. Cobertura: el punto de partida real

### Lo que se midió primero, y por qué engañaba

Con el recolector ya funcionando, la primera cifra —sólo pruebas unitarias, todo
incluido— fue **18,0 %**. Engaña en dos direcciones a la vez:

- **Hacia abajo:** de las 16.278 líneas del informe, **7.366 eran migraciones de
  Entity Framework**. Código generado por `dotnet ef`, que nadie escribió y que
  ninguna prueba puede ni debe ejecutar. El 45 % del total, todo a cero.
- **Hacia arriba:** faltaban diez módulos enteros. `tests/UnitTests` referencia los
  proyectos uno a uno, así que `Reporting`, `Docs`, `Tags`, `Teams`,
  `Notifications`, `Communication` y casi todo `Automations` y `Comments` fuera del
  dominio **no aparecían en el informe**. Una métrica que omite lo no probado
  siempre miente a favor.

### Cómo se mide ahora

`coverage.runsettings` decide explícitamente qué cuenta: fuera migraciones,
`*.Designer.cs`, `ModelSnapshot`, `Program.cs` y lo marcado como generado por el
compilador.

Y se miden **las dos suites juntas**, fusionadas con ReportGenerator. Ninguna basta
sola: las unitarias cubren el dominio, y las de integración son las únicas que
cargan `Infrastructure` y los endpoints —y las únicas que, al levantar la API
entera vía `ApiHost`, hacen visibles los diez módulos que faltaban, sin tener que
añadir sesenta referencias de proyecto a mano—.

Todo ello en `scripts/cobertura.sh`, que es lo que ejecuta el CI.

### La cifra

```
Cobertura de líneas ................. 70,8 %
  descontando Presentation .......... 63,0 %
Cobertura de ramas .................. 58,3 %
Frontend, líneas (Karma) ............ 47,7 %   (562 / 1.179)

Al empezar la auditoría era 68,8 / 60,4 / 55,3. La subida no viene de escribir pruebas para
subir el número, sino de cubrir los siete módulos que no tenían ninguna.
```

64 ensamblados, 509 clases, 349 archivos.

**El objetivo del 60 % ya estaba cumplido; nadie lo había calculado nunca.** Conviene
decirlo sin adornos: no es mérito de este trabajo, es que la medición faltaba.

### Por qué el 68,8 % está inflado, y en cuánto

La primera lectura de este informe fue que los endpoints de todos los módulos
estaban bien cubiertos. **Era falsa**, y merece explicarse porque el mismo error se
puede repetir.

Reparto por capa:

| Capa | Cubierto | Total | % | Peso |
|---|---:|---:|---:|---:|
| Domain | 2.018 | 3.218 | 62,7 | 29,7 % |
| Application | 1.446 | 2.990 | 48,4 | 27,6 % |
| **Presentation** | **2.358** | **2.396** | **98,4** | **22,1 %** |
| Infrastructure | 1.522 | 2.114 | 72,0 | 19,5 % |

`Presentation` pesa casi una cuarta parte de todo lo medido y sale al 98,4 %. No es
porque las pruebas llamen a los endpoints. Es porque en una API mínima las líneas

```csharp
group.MapGet("/kpi", async (...) => { ... });
```

**se ejecutan al registrar la ruta**, o sea, en cuanto la aplicación arranca. Basta
con que las pruebas de integración levanten el host una vez para que el registro de
rutas de los quince módulos cuente como cubierto, aunque nadie llame nunca a
ninguna.

Descontando esa capa, la cobertura de líneas real es **60,4 %** en lugar de 68,8 %.

Y hay una forma directa de comprobarlo: las rutas que las 97 pruebas de integración
llegan a tocar son sólo estas seis familias.

```
/api/v1/auth/…          /api/v1/projects
/api/v1/tasks/…         /api/v1/comments/…
/api/v1/automations/…   /api/v1/custom-fields/…
```

**Ninguna prueba llama a Reporting, Tags, Teams, Notifications, Communication,
Docs ni Ticketing.** Siete módulos sin una sola prueba de extremo a extremo.

Por eso ahora hay un **segundo umbral sobre la cobertura de ramas**: el registro de
rutas no tiene ramas, así que esa cifra no admite el engaño. Mide si se probaron los
dos lados de cada decisión, y está en 55,3 % — por debajo del 60 %. Ahí está el
trabajo de verdad.

### El reparto por ensamblado

Bien cubierto, donde se aplicó la disciplina de funciones puras en el dominio:

| Ensamblado | % |
|---|---:|
| Comments.Domain | 100 |
| WorkItems.Domain | 96,7 |
| Automations.Domain | 95,2 |
| WorkItems.Application | 93,1 |
| CustomFields.Domain | 88,9 |
| Ticketing.Domain | 84,8 |

Sin cubrir, ordenado por lo que más importa:

| Ensamblado | % | Por qué importa |
|---|---:|---|
| Reporting.Domain | **0** | el dashboard de la Fase 5 se apoya aquí |
| Reporting.Application | **0** | idem |
| Tags.Application | **0** | |
| Teams.Application | **0** | |
| Communication.Application | **0** | |
| Notifications.Application | **0** | es el canal del que depende la exportación asíncrona |
| Docs.Application | 9,3 | el editor es el bloque 5B |
| Identity.Application | 23,6 | autorización: un fallo aquí no se ve hasta producción |
| Identity.Domain | 38,4 | |
| Notifications.Domain | 38,9 | |
| Docs.Domain | 41,2 | |
| Communication.Domain | 43,4 | |
| Calendar.Domain | 46,2 | |
| BuildingBlocks.Infrastructure | 47,7 | lo comparten todos los módulos |

Los seis módulos con `Application` a cero y `Presentation` al 100 % son
exactamente los que ninguna prueba llama. No es que la petición se salte la capa de
aplicación, como sugería la primera lectura de este documento: es que no hay
petición.

## 4. Sobre dos encargos que ya existen

### El módulo de automatizaciones ya está construido

Se entregó en el bloque 4D. `src/Modules/Automations` tiene 908 líneas repartidas en
dominio, aplicación, infraestructura y presentación: el agregado `AutomationRule` con
condiciones y acciones, el `EvaluadorDeCondiciones` como función pura, el
`MotorDeAutomatizaciones`, endpoints con vocabulario, la pantalla de administración y
el `PuenteDeAutomatizaciones` en el host que traduce eventos de `WorkItems` a
disparos. Su dominio está al 94,1 % de cobertura.

No hay que crearlo. Lo que sí tiene sentido es **ampliarlo** hacia lo que hoy ofrece
el mercado, que es un encargo distinto y está en el plan como bloque propio.

### Reporting tampoco parte de cero

`src/Modules/Reporting` ya existía con 1.348 líneas y ocho rutas. Al ejecutarlas por primera vez
aparecieron cuatro cosas:

**Los KPI eran en parte inventados.** `AvgLeadTimeDays: 2.5` y `AvgCycleTimeDays: 1.4` estaban
escritos a mano en el código, porque `WorkTask` no guardaba ninguna marca de tiempo y no había
con qué calcularlos.

**El diagrama de quemado era enteramente ficticio:**

```csharp
int remaining = Math.Max(0, totalTasks - (i / 2));
```

Bajaba una tarea cada dos días pasara lo que pasara, sin mirar nunca cuándo se completó nada, y
rellenaba el total a un mínimo de diez tareas para que la línea quedara bien en proyectos
pequeños. Un gráfico que no depende de los datos es una decoración con aspecto de medida, y es
peor que no tener gráfico: se toman decisiones mirándolo.

Ambos resueltos. Se añadieron `CreatedAtUtc` y `CompletedAtUtc` a `WorkTask` —migración
`AddTimestampsToWorkTask`, aplicada y verificada en MySQL—, con la marca de cierre puesta al
entrar en «Done» y **borrada al salir**: una tarea reabierta no está terminada, y conservar la
fecha del primer cierre mediría un trabajo que luego se deshizo. El tiempo de entrega y el
quemado salen ya de esas fechas.

El **tiempo de ciclo sigue sin poder calcularse** y por eso viaja como `null`: mide desde que el
trabajo empieza de verdad, y eso exige saber cuándo la tarea entró en «En Progreso». Sólo se
guarda el estado actual, no su historial. Un hueco visible es mejor que un número inventado —
quien lee el panel puede desconfiar de lo que no está, pero no de lo que parece medido. Tenerlo
exigiría una tabla de historial de estados; es la decisión pendiente.

**Reporting viola el aislamiento entre módulos.** `Reporting.Infrastructure` referencia
`Projects.Infrastructure`, `WorkItems.Infrastructure` y `Ticketing.Infrastructure`, y consulta
sus `DbContext` directamente. Es la deuda arquitectónica más grande del repositorio y sigue ahí:
tocarla es rehacer el módulo, no un arreglo.

**Los modelos de lectura son código muerto.** `TaskReadModel`, `ProjectReadModel`,
`TicketReadModel` y sus consumidores existen, pero no hay ningún `AddMassTransit` ni
`AddConsumer` en el host: nunca se registran ni se pueblan. Por eso `Reporting.Domain` estaba al
0 %. O se conectan y el repositorio pasa a leer de ellos —que es lo que resolvería también la
violación de aislamiento—, o se borran; mantenerlos como están sólo confunde a quien los lea.

## 5. La deuda de Reporting — resuelta

### El problema

`Reporting.Infrastructure` referenciaba **seis proyectos de otros tres módulos** —Projects,
WorkItems y Ticketing, con sus capas de infraestructura incluidas— para que `DashboardRepository`
consultara sus `DbContext` directamente. Es la regla que sostiene el monolito modular, rota de
lleno: cualquier cambio en el esquema de otro módulo llegaba hasta aquí sin pasar por ningún
contrato.

Y había una segunda parte. El módulo tenía tres modelos de lectura —`ProjectReadModel`,
`TaskReadModel`, `TicketReadModel`— alimentados por consumidores de MassTransit. **Corrijo aquí
lo que dije antes en este mismo documento**: no eran código muerto. Se registran, por
`Assembly.Load` con el nombre en texto desde `BuildingBlocks.Infrastructure`, y las tablas tenían
filas: 6 proyectos y 113 tareas.

Lo que sí eran es inservibles. Los tres consumidores atendían **sólo a eventos de creación** —
ninguno de cambio de estado, actualización ni borrado—, así que una tarea se quedaba en «To Do»
para siempre y un proyecto al 0 % de avance. Y nadie los leía: el repositorio del panel los
ignoraba y consultaba las tablas de los otros módulos. Una proyección que no se actualiza no es
una caché, es una trampa para quien la conecte después creyendo que está al día.

### Lo que se hizo

Las consultas del panel viven ahora en `ApiHost/Reporting/ConsultasDelPanel.cs`. **El host es
donde este proyecto compone lo que cruza módulos** —el mismo sitio y el mismo motivo que
`PuenteDeAutomatizaciones`—. Reporting sigue declarando el contrato `IDashboardRepository`; sólo
cambia quién lo implementa: el módulo dice qué necesita, el host sabe de quién sacarlo.

Los modelos de lectura y sus consumidores se eliminaron, con migración
`QuitarModelosDeLecturaMuertos` que retira las tres tablas, aplicada y verificada.

Se descartó la alternativa de alimentar el panel desde esas proyecciones: habría exigido
consumidores para todos los eventos que cambian el estado, un relleno inicial para lo que ya
existe, y habría metido consistencia eventual justo en las cifras que se acababan de hacer
honestas. Queda anotado como la evolución natural si algún día el panel necesita no consultar
tres módulos en caliente.

### Dos infracciones más, encontradas por la prueba que lo vigila

Al escribir el guardián apareció lo que no se veía a simple vista: **`Tags.Application`
referenciaba `Teams.Domain` y `Projects.Domain`** para que dos manejadores crearan una etiqueta
al nacer un equipo o un proyecto. Esos manejadores están ahora en
`ApiHost/Tags/EtiquetasAutomaticas.cs`.

`AislamientoEntreModulosTests` recorre los `.csproj` en disco —no los ensamblados cargados, para
ver también los módulos que el proyecto de pruebas no referencia, que es donde nadie mira— y
falla si un módulo referencia a otro o al host. **Hoy: cero infracciones en toda la solución.**

Una regla de arquitectura que sólo vive en la cabeza de quien la escribió se rompe en cuanto
entra alguien nuevo, o en cuanto pasan unos meses.

### De camino

El registro de consumidores buscaba los ensamblados por nombre en texto y se tragaba el fallo
con un `catch { // Ignore if not found }`. Un módulo renombrado dejaba de recibir sus mensajes y
el sistema arrancaba como si nada. Ahora un nombre que no carga revienta el arranque: es un
error de configuración, no una circunstancia.

## 6. Las preferencias de notificación — resueltas

`GET /api/v1/notifications/preferences` devolvía un objeto con valores fijos escritos en el
propio endpoint, y `PUT` era literalmente esto:

```csharp
group.MapPut("/preferences", (object prefs) => Results.Ok(prefs));
```

Devolvía el cuerpo recibido. **No guardaba nada.** La pantalla funcionaba entera —los
interruptores se movían, salía el aviso de «Preferencias guardadas»— y al recargar todo volvía a
su sitio. Prometer y no cumplir es peor que no ofrecerlo.

Ahora hay entidad de dominio, tabla `NotificationPreferences` con índice único por persona y
organización (migración `AddNotificationPreferences`, aplicada y verificada), y los dos endpoints
enrutados por MediatR.

Decisiones que merecen constar:

- **Todo llega activado salvo lo que molesta.** Lo que la persona espera —le asignan algo, la
  mencionan, su exportación terminó— viene encendido, porque no recibirlo se vive como que el
  sistema falla. Lo que informa de actividad ajena —una tarea que completó otro, un ticket que
  tocó otro— viene apagado: encendido hace ruido, y el ruido acaba con la persona ignorando
  *todos* los avisos, incluidos los que importaban.
- **El aviso de exportación terminada llega encendido y se puede apagar**, que era la condición
  explícita del encargo para la Fase 5.
- **Las horas se guardan como `TimeOnly`, no como texto**, y una hora ilegible se rechaza en vez
  de sustituirse por algo razonable. Guardar en silencio unas horas de silencio distintas de las
  que la persona escribió es de los fallos que se descubren semanas después, al no recibir un
  aviso.
- **El tramo de silencio cruza la medianoche.** De 22:00 a 08:00 es el caso normal, y con la
  comparación ingenua —«después del inicio *y* antes del fin»— no silencia nunca nada. El fallo
  sólo se notaría de madrugada. Es una función pura con la hora como argumento, para poder
  probar la medianoche sin esperar a que sean las doce.
- **Un tipo de aviso que nadie declaró pasa.** Si alguien añade un aviso y olvida ponerlo en la
  lista, el fallo es que se recibe de más —molesto y visible— y no que se pierde en silencio,
  que es el fallo que nadie detecta.
- **Leer no crea la fila.** Consultar no es modificar, y escribir en una petición de lectura
  convierte cada apertura de la pantalla en una escritura. Se materializan al guardar.

La prueba que lo cubre no se queda en el código de estado ni en lo que devuelve el `PUT`:
**vuelve a preguntar en otra petición**. Es la lección del `PATCH` que no guardaba, de la Fase 4.

## 7. Docker y CI/CD

### 7.1 La comprobación de salud llevaba 1.590 fallos seguidos

`docker ps` mostraba `crm_saas_api` como **unhealthy** desde hacía horas. El motivo:

```
exec: "curl": executable file not found in $PATH
```

El `healthcheck` del compose invocaba `curl`, y la imagen `mcr.microsoft.com/dotnet/aspnet:9.0`
no lo trae. Tampoco `wget`. **Nunca había pasado ni una sola vez.**

No es cosmético. Con `depends_on: condition: service_healthy` el arranque se queda colgado
para siempre, y en Swarm o ECS es un servicio al que no se le enruta tráfico jamás. Cualquier
plataforma que respete la comprobación consideraba la API caída.

Se resolvió **sin instalar nada**: la propia aplicación acepta `--health-check`, hace la
petición a `/health/live` y sale con 0 o 1. Instalar `curl` con apt era la alternativa obvia y
se descartó por tres motivos: añade una descarga de red a cada construcción —que de hecho
falló al intentarlo, dejando el build roto por algo ajeno al proyecto—, engorda la imagen, y
suma superficie de CVE para pedir una URL. El proceso que sabe responder es el mismo que se
quiere comprobar.

Comprobado en las dos direcciones: contenedor en marcha → `healthy`, salida 0; sin servidor
detrás → salida 1 y `La sonda de salud falló: Connection refused`.

### 7.2 Y la del frontend tampoco habría pasado

Al escribirla apareció el mismo tipo de fallo por otro motivo: nginx escucha en
`0.0.0.0:8080` —sólo IPv4— y dentro del contenedor `localhost` resuelve primero a `::1`. La
comprobación moría con «Connection refused» mientras el sitio se servía perfectamente desde
fuera. Con `127.0.0.1` funciona.

Se detectó porque se ejecutó la imagen y se miró el estado, no porque se leyera el fichero.

### 7.3 La imagen del frontend ignoraba el lockfile

```dockerfile
RUN npm install --legacy-peer-deps
```

`npm install` resuelve el árbol de nuevo en lugar de respetar `package-lock.json`, así que la
imagen podía llevar versiones distintas de las que probó el CI. Y `--legacy-peer-deps` escondía
los conflictos entre pares en vez de mostrarlos. **Una imagen que no se puede reproducir desde
el repositorio no sirve para diagnosticar nada cuando falla en producción.** Ahora es `npm ci`.

### 7.4 No había `.dockerignore`

Ninguno de los dos contextos lo tenía, y ambos Dockerfiles hacen `COPY . .`. Se enviaban al
demonio de Docker el repositorio entero, los `bin/` y `obj/` de sesenta y ocho proyectos,
`web/node_modules` y el historial de git.

Además de lento, es incorrecto en dos sitios concretos: los `obj/` compilados en Windows llevan
rutas absolutas dentro de `*.nuget.g.props`, y el `node_modules` del anfitrión trae los
binarios nativos de Windows de esbuild y rollup, que pisan los de Linux que `npm ci` acaba de
instalar. Los dos fallan con mensajes que no mencionan la causa.

### 7.5 Las dos imágenes corrían como root

Ahora la API usa el usuario `app` (UID 1654) que la imagen base de .NET define desde la
versión 8, y el frontend pasa a `nginxinc/nginx-unprivileged` (UID 101). Verificado con `id`
dentro de los contenedores.

Es lo que permite desplegar en OpenShift y en cualquier clúster con una política que lo exija.
El precio es que nginx escucha en el 8080 en vez del 80 —un proceso sin privilegios no puede
abrir un puerto por debajo de 1024—, así que el compose mapea `4200:8080`.

### 7.6 Las redirecciones llevaban el puerto interno

Al pasar al 8080 salió a la luz algo que ya estaba mal: nginx construye redirecciones
absolutas con el puerto en el que escucha, así que la raíz respondía

```
Location: http://localhost:8080/es/
```

Detrás de un proxy, un balanceador o un ingress —o sea, en cualquier despliegue real— eso manda
al visitante a un puerto que no está publicado. Con `absolute_redirect off` la redirección es
relativa (`/es/`) y el navegador conserva el esquema, el dominio y el puerto por los que llegó.

Con el puerto 80 el fallo estaba igual de presente pero se camuflaba en local.

### 7.7 El CI no construía ninguna imagen

Nada de lo anterior podía salir a la luz: los Dockerfiles sólo se ejercitaban cuando alguien
los usaba a mano.

Hay ahora un trabajo `imagenes` que construye y publica en GHCR, en `linux/amd64` y
`linux/arm64`, con SBOM y atestación de procedencia, caché de capas en Actions, y etiquetado
por commit, rama y versión semántica. En los pull requests construye pero no publica. Y arranca
la imagen del frontend para comprobar que se declara sana y sirve la aplicación de verdad,
porque construir sólo demuestra que compila.

### 7.8 Los despliegues eran teatro

```yaml
- name: Desplegar backend
  run: 'echo "Pendiente: integrar con el proveedor cloud"'
```

Dos trabajos, «Desplegar a staging» y «Desplegar a producción», con ese contenido. **Salían en
verde.** El panel de Actions decía que producción se había actualizado y no era cierto.

Un despliegue que siempre tiene éxito porque no hace nada es peor que no tenerlo: quita las
ganas de mirar. Se quitaron los dos.

Lo que queda en su lugar es lo que hace falta para desplegar en cualquier sitio: imágenes
versionadas y publicadas, un `docker-compose.prod.yml` que las consume sin compilar nada en el
servidor, y `docs/DESPLIEGUE.md` con lo que hay que saber para Kubernetes, OpenShift, Cloud
Run, App Runner y Container Apps. El último paso —qué plataforma, con qué credenciales— es una
decisión que no está tomada, y el sitio donde añadirla está marcado en el workflow.

### Lo que hay que tener presente al desplegar

- **Las migraciones se aplican al arrancar** y, si fallan, la aplicación no sirve nada. Con
  varias réplicas todas lo intentan a la vez; MySQL las serializa, pero la primera migración
  larga convertirá eso en una carrera. Cuando llegue, hay que sacarla a un paso previo.
- **`/health/live` y `/health/ready` no son intercambiables.** Usar «listo» como sonda de
  vida reinicia la aplicación cada vez que la base de datos tose, lo cual no arregla la base y
  sí tira las conexiones que aún funcionaban.
- **Desplegar por digestión, no por etiqueta.** `latest` y los nombres de rama se mueven.

## 9. Fase 5A — el menú de navegación

### 9.1 Tres borrados que no borraban

Al construir la papelera aparecieron tres endpoints que decían haber hecho algo y no lo hacían.
Es la misma familia que la cobertura que no se medía, el sembrador que decía «completado» y el
`PUT /notifications/preferences` que devolvía lo que le mandabas.

| Endpoint | Qué hacía | Qué devolvía |
|---|---|---|
| `DELETE /api/v1/tickets/{id}` | Leía el ticket, comprobaba que existía y **nada más** | 204 |
| `DELETE /api/v1/tasks/{id}` | Cargaba la tarea y la volvía a guardar **sin tocarla** | 204 |
| `DELETE /api/v1/projects/{id}` | Borraba bien, pero guardaba el **TenantId** en `DeletedBy` | 204 |

Los dos primeros dejaban el elemento en la lista al recargar. El tercero borra de verdad, pero
el registro de quién borró un proyecto decía el identificador de la empresa en todas las filas,
que es tanto como no guardarlo.

Los tres están arreglados y los tres tienen prueba de integración que comprueba **que
desaparece de la lista**, no que responda 204: responder 204 ya lo hacían.

### 9.2 Un desajuste de vocabulario en los permisos, anotado y sin tocar

La tabla `EntityPermissions` guarda hoy 185 filas, todas por rol y de módulo entero, con
`EntityType` en plural: `"Tasks"`, `"Projects"`, `"Docs"`. Los comandos, en cambio, piden
autorización en singular: `EntityType => "Task"`.

**Nunca casan.** Un permiso por rol sobre «Tasks» no llega a consultarse cuando un comando
pregunta por «Task», así que la autorización granular por rol no está haciendo nada; lo que
decide hoy es el atajo de administrador y el permiso por defecto de los miembros.

No se ha cambiado aquí a propósito: unificar el vocabulario altera quién puede hacer qué en toda
la aplicación, y eso merece su propio trabajo con sus propias pruebas, no un arreglo de paso
dentro de otra cosa. La compartición nueva escribe en **singular**, que es el vocabulario que sí
se consulta, para que compartir algo conceda acceso de verdad donde se comprueba.

### 9.3 El sembrador falla al arrancar sobre una base ya sembrada

Levantando la API contra la base de desarrollo con datos, la siembra falla en dos módulos:
`Tags` por clave duplicada (`IX_Tags_TenantId_Name`) y `Projects` por un índice fuera de rango.
La aplicación arranca igual y avisa —eso funciona— pero conviene saber que el sembrador no es
idempotente. No bloquea nada y no se ha tocado en esta fase.

---

## 10. Lo que queda anotado y sin resolver

- **Las páginas de documentos no llevan inquilino.** `CreatePageCommand` y `UpdatePageCommand`
  no tienen `TenantId`, así que el aislamiento de las páginas depende de conocer el
  identificador del documento. No es explotable a ciegas, pero es la misma familia que 2.5.
- **`Reporting.Domain` sigue al 0 %**: sólo quedan `Report` y `Dashboard`, y nada ejercita el
  alta de informes.
- **Los eventos de integración de `BuildingBlocks.Contracts` están declarados y no los usa
  nadie.** Serían la forma natural de que un módulo reaccione a otro sin pasar por el host;
  hoy no existe camino de publicación.
- **El árbol de trabajo de git abandonado** (2.4).
- **167 avisos de lint** en el frontend, heredados.
- **Un paquete del frontend supera el presupuesto** de tamaño en 120 kB.
- **El vocabulario de `EntityType` no casa entre los permisos sembrados y los comandos** (9.2).
  Es lo más serio de esta lista: la autorización granular por rol no llega a aplicarse.
- **El sembrador no es idempotente** (9.3).
- **Compartir no tiene interfaz todavía.** Los endpoints existen y están probados, y los filtros
  «compartido conmigo» y «privado» funcionan contra ellos, pero no hay ningún botón en la
  aplicación que comparta. Hasta que lo haya, esas dos entradas del menú responden bien y
  devuelven poco, que es honesto pero no útil.
- **Archivar y borrar tampoco tienen botón.** Misma situación: la API responde, el menú enseña
  el archivo y la papelera, y de momento sólo se llenan desde la API.
- **Documentos tiene la columna de archivado y no la usa.** Se le puso al modelar el concepto
  para no dejar el agregado a medias, pero Docs mantiene su propio panel lateral y no se ha
  enganchado al compartido.
