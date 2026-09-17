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

### 9.2 Un desajuste de vocabulario en los permisos — resuelto

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

**Resuelto en septiembre de 2026.** La tabla tiene un solo vocabulario, en singular
(`TiposDePermiso`), y se normaliza al crear cada permiso y al consultarlo, no sólo en la
migración: una pestaña abierta desde antes del cambio sigue mandando «Tasks» y no debe volver a
crear una fila huérfana. La migración `PermisosEnSingular` convierte las filas existentes y, si una
persona o un rol ya tenía las dos, conserva la del singular, que era la única que se aplicaba.

De paso, el servicio no ordenaba: con un permiso sobre una tarea y otro sobre todas las tareas,
MySQL devolvía cualquiera de los dos. Ahora gana el concreto.

**Cambia lo que puede hacer la gente.** Los niveles por rol empiezan a aplicarse tal como están
guardados. Con los sembrados, un miembro sigue igual (edita tareas, proyectos y documentos) y un
invitado pasa de no poder editar tareas a poder hacerlo, que es lo que la pantalla ya decía.

### 9.2 bis Personas y permisos, abiertos a cualquier miembro — resuelto

Encontrado al preparar las pruebas de lo anterior, con un miembro contra la aplicación levantada.
La pantalla de administración tenía guarda; **la API no**. `POST /users` con rol «Admin»
respondía 201 a un miembro, igual que cambiarse el rol con `PUT /users/{id}`, borrar personas o
leer y cambiar `/permissions`. Cualquiera de la organización podía hacerse administrador en dos
peticiones.

Esos endpoints exigen ahora el rol «Admin». El directorio (`/users/tenant`) y el perfil propio
siguen abiertos, porque los usan las menciones y los selectores de responsable.

Salieron dos fallos más al limpiar los datos de la sonda: **no se podía borrar a ningún
administrador** —la cuenta que protege al último no se traducía a SQL— y **se podía quitar el rol
al último administrador**, dejando la organización sin nadie que la gestione.

**El token de invitado se ha quitado.** `POST /auth/guest-token` emitía, a cualquiera y sin
credenciales, un token con rol «Guest» que pasaba todas las comprobaciones de sesión de la API. No
llegó a funcionar —su política de límite de peticiones no existía y respondía 409—, así que se
quitó antes de que alguien arreglara esa política y abriera la API entera.

**En su lugar, la entrada de tickets.** Los clientes de una organización abren tickets desde su
propia web o su backend con `POST /api/v1/ticket-intake` y una **clave de entrada** en la cabecera
`X-Api-Key`:

- La clave **sólo sirve para crear tickets** y **fija la organización**: no hace falta identificador
  público de organización y quien llama no puede elegir dónde cae el ticket. Se guarda su hash, se
  enseña una vez, se puede revocar y la gestiona un administrador (pestaña «Entrada de tickets»).
- **Obligatorio**: asunto, mensaje, nombre, email, teléfono y empresa. **Opcional**: adjuntos
  (hasta 10 imágenes o vídeos de 50 MB), clasificación, etiquetas, equipo, estado y prioridad.
- Acepta JSON y multipart. CORS abierto sólo en ese endpoint, sin credenciales, y límite de
  peticiones por clave.
- La página `/support` se quitó: la sustituyen los ejemplos que da la pantalla de claves, con la
  URL y la clave ya puestas.

Probado desde un navegador en otro origen con una imagen y un vídeo reales, guardados en
Cloudinary. Salió un fallo: un vídeo corrupto hacía responder 500; ahora es un 400 con el nombre del
fichero, y lo ya subido se borra.

De paso, dos cosas de la ficha del ticket: **las etiquetas no se guardaban nunca** (la pantalla las
mandaba y el comando no tenía dónde recibirlas) y **el botón «Adjuntar» no hacía nada**. El botón
«Nuevo ticket» sólo lo veían los administradores por una directiva que no consultaba permisos de
verdad; ahora lo ve todo el mundo, como el de tareas, y decide el servidor.

**Proyectos, tickets y documentos piden autorización**, como ya hacían las tareas. Sus comandos de
escritura implementan `IAuthorizeEntity`, y el inquilino se toma de la sesión, no de la petición.
Los comandos de páginas de Documentos no llevan el documento, así que comprueban el nivel sobre los
documentos en general: compartir un documento concreto con permiso de edición no basta para editar
sus páginas si el rol tiene sólo lectura. La pantalla gana la fila de tickets, y la migración
`PermisosDeTickets` siembra «Edit» para miembros e invitados, que es lo que ya podían hacer.

### 9.3 El sembrador falla al arrancar sobre una base ya sembrada

Levantando la API contra la base de desarrollo con datos, la siembra falla en dos módulos:
`Tags` por clave duplicada (`IX_Tags_TenantId_Name`) y `Projects` por un índice fuera de rango.
La aplicación arranca igual y avisa —eso funciona— pero conviene saber que el sembrador no es
idempotente. No bloquea nada y no se ha tocado en esta fase.

---

## 10. Fase 5D — la exportación de informes

### 10.1 El informe que constaba generado sin haberse generado

`POST /reports/{id}/generate` respondía 200, marcaba el informe como generado y guardaba
`GeneratedFileUrl = "/reports/{id}/{nombre}.pdf"`. **Esa ruta no la servía ningún endpoint y ese
fichero no existía.** En la base de desarrollo había tres informes con URLs así.

Es el mismo patrón que el borrado que no borraba y que la cobertura que no se medía, con un
agravante: aquí ya se había arreglado *una* capa del problema —el DTO no copiaba esos campos, así
que ni siquiera llegaban a la pantalla— y al arreglarla quedó a la vista que lo que llegaba
tampoco era cierto.

Los cuatro campos (`IsGenerated`, `GeneratedFileUrl`, `GeneratedAt`, `ErrorMessage`) se han
quitado del informe, con su migración. Además de mentir, eran **un solo juego de campos para
muchas exportaciones**: el mismo informe se exporta en PDF y en Excel, por dos personas a la vez,
y la segunda pisaba a la primera.

### 10.2 El trabajador de segundo plano exportaba ficheros vacíos

Encontrado **verificando contra la aplicación levantada**, no por las pruebas.

El generador de exportaciones corre sin petición HTTP, así que no hay usuario, así que
`TenantDbContext.CurrentTenantId` vale `Guid.Empty` y el filtro global no casa con ninguna fila.
Resultado:

| | API en pantalla | Fichero exportado |
|---|---|---|
| Proyectos | 5 | **0** |
| Tareas | 15 | **0** |
| Tickets abiertos | 215 | **0** |

El fichero salía bien formado, con su nombre, su tamaño y su aviso de «ya está listo». **Nada
fallaba.**

**Y mis propias pruebas de integración no lo cazaron**, porque comprobaban que el CSV contuviera
la palabra «Proyectos» —el encabezado— y no lo que ponía al lado. Comprobar que un informe tiene
la forma correcta no es comprobar que dice la verdad. Se añadieron dos pruebas que comparan el
fichero **contra los números que devuelve la API**.

El arreglo es `TenantDbContext.ComoInquilino(tenantId)`: declara el inquilino para las consultas
de un trabajo de segundo plano sin apagar los demás filtros. La alternativa,
`IgnoreQueryFilters()`, habría hecho salir en los informes lo archivado y lo borrado, que es peor
porque no se nota.

### 10.3 El sembrador estaba roto sobre una base nueva, por la misma causa

Anotado en §9.3 como «no es idempotente». La causa real es la de 10.2: el sembrador tampoco tiene
petición, así que insertaba tres espacios, los releía a través del filtro, obtenía cero filas y
lanzaba en `existingSpaces[0]`. Con el paso de Projects caído, **las tareas tampoco se creaban**,
porque dependen de que haya proyectos.

Se veía en cualquier ejecución filtrada de las pruebas de integración, que levantan un MySQL
limpio: fallaban las que necesitan tareas. En la ejecución completa pasaban, que es la clase de
intermitencia que se acaba culpando al azar.

Arreglado declarando el inquilino en los diez contextos que el sembrador usa.

---

## 11. Fase 5D — el constructor y la programación

### 11.1 Un `with` de record que dejaba los filtros sin validar

`DefinicionDeInforme` tenía la lista de filtros expuesta así:

```csharp
public IReadOnlyList<FiltroDeInforme> FiltrosAplicados { get; } = Filtros ?? [];
```

Parece correcto y no lo es: **el `with` de los records copia el campo de respaldo del original en
lugar de volver a ejecutar el inicializador**. Una definición modificada con
`with { Filtros = ... }` conservaba la lista vieja —vacía—, así que sus filtros **no se validaban
ni se aplicaban**: el informe salía con todas las filas y sin dar ningún error.

Lo cazó una prueba, no la lectura del código. La forma correcta es una propiedad calculada
(`=> Filtros ?? []`), que no tiene campo que copiar.

### 11.2 Lo que se decidió no admitir, y por qué

Tres decisiones que reducen lo que el producto ofrece, a propósito:

- **El día 31 en una programación mensual se rechaza.** Un informe programado el 31 no se
  generaría en febrero ni en los meses de treinta días: cuatro meses al año fallando en silencio.
  Se admite hasta el 28 y el mensaje explica el motivo.
- **No hay frecuencia «cada hora».** Un informe que llega cada hora se deja de leer el segundo día
  y se convierte en ruido que además esconde los que sí importan.
- **La definición no guarda SQL ni opciones de ECharts.** Lo primero convertiría el constructor en
  una vía de ejecución de consultas arbitrarias; lo segundo ataría los informes que construyan los
  usuarios a la librería de gráficas, y cambiarla algún día invalidaría su trabajo.

### 11.3 La hora de los informes programados es la del servidor

La programación guarda **hora local**, no UTC, porque quien pide un informe «cada lunes a las 8»
lo quiere a las 8 de su mañana y guardar UTC obliga a una conversión que se rompe dos veces al año
con el cambio de hora.

Pero **no hay zona horaria por inquilino**, así que el planificador usa la del servidor. Mientras
todos los clientes estén en el mismo huso no se nota; en cuanto haya uno fuera, sus informes
llegarán con las horas de diferencia que corresponda. Hace falta una zona horaria por inquilino
—o por persona— y convertir en el planificador.

---

## 12. Fase 5C — el panel

### 12.1 El gestor de paneles que no gestionaba nada

Se podían crear paneles, ponerles nombre y marcarlos como públicos, y **pulsar uno no hacía
nada**: `selectDashboard` guardaba la selección en una señal que ninguna plantilla leía, y la
columna `WidgetsJson` no la escribía ni la leía ningún código. La tabla tenía **cero filas**: la
funcionalidad nunca llegó a usarse.

Es el mismo patrón que el borrado que no borraba y el informe que constaba generado. La diferencia
es que aquí no había nada que arreglar: se le ha dado significado a la columna y a la pantalla.

### 12.2 Una serie temporal ordenada por cantidad

El motor de informes ordenaba **siempre** de mayor a menor. Parece razonable —lo grande primero—
y destroza cualquier serie temporal: «tickets abiertos por mes» salía `2026-08, 2026-09, 2026-07`,
y una gráfica de líneas con el eje de tiempo desordenado no dice nada.

**No lo cazó ninguna prueba**: se vio mirando la salida real del panel. Ahora las fechas se ordenan
por fecha y hay una prueba que lo vigila.

### 12.3 Un `Guid` en el sitio equivocado, que compilaba

El endpoint de guardar la disposición pasaba el identificador del panel donde va el de la persona:

```csharp
new GuardarDisposicionCommand(tenantId, id, userId, cuerpo.Widgets)
//                                      ↑ panel   ↑ persona   — el comando espera (tenant, user, panel)
```

Tres `Guid` seguidos compilan en cualquier orden. La comprobación de dueño habría rechazado al
propio dueño de su panel. Los tres comandos con varios `Guid` pasan ahora sus argumentos **con
nombre**.

### 12.4 El coste de ECharts, medido y acotado

El paquete del panel pasa de 62 kB a **789 kB en bruto / 224 kB por la red**, y sólo se descarga al
abrir el panel. Se importa con los módulos justos —tres tipos de gráfica, cuatro componentes, el
renderizador de canvas— y el mapa vive en un solo fichero.

Se le ha dado **presupuesto propio** (aviso a 850 kB, error a 1 MB) en lugar de subir el de todas
las pantallas, para que el resto siga vigilado con el límite estricto. El aviso de `anyScript` a
700 kB sigue saltando para este paquete y para el de Docs, que ya estaba por encima desde antes.

---

## 13. Fase 5B — menciones y esquema

### 13.1 El contrato invisible entre el editor y el servidor

Las menciones se escriben en el editor como `data-mencion-tipo` y `data-mencion-id`, y el servidor
las extrae leyendo esos dos atributos del HTML guardado. **Si los nombres divergieran, las
menciones dejarían de indexarse sin que nada fallara**: los documentos seguirían guardándose, los
enlaces seguirían viéndose, y ninguna tarea volvería a saber quién habla de ella.

Es la misma familia que la cadena `"Ticket"` escrita a mano entre Ticketing e Identity. Se cuida
igual: las constantes están con nombre en un solo fichero del lado del editor, hay pruebas
unitarias que fijan el formato del lado del servidor, y una de integración que **escribe una
mención por la API y comprueba que la tarea la ve desde el otro lado**.

### 13.2 Dos fallos de EF y uno de pruebas

- **Ordenar por una propiedad del objeto proyectado.** `OrderByDescending` después del `Select`
  hacía que EF no supiera traducir la consulta: fallaba al ejecutarse, con un 409 y un mensaje de
  cien líneas. Se ordena antes de proyectar.
- **La forma real de la API no era la que suponía la prueba.** `POST /api/v1/docs` devuelve el
  identificador **como una cadena suelta**, no envuelto en un objeto. Una prueba escrita contra la
  forma que uno espera comprueba una API imaginaria.
- **Pasaba sola y fallaba acompañada.** Varias pruebas de menciones trabajan sobre la misma tarea
  del sembrador, y una exigía ser la única que la mencionaba. Es el patrón que ya mordió con las
  reglas de automatización sin condiciones.

### 13.3 El buscador de menciones filtraba en el cliente — resuelto

Al escribir `#` se pedían las primeras cincuenta tareas, cincuenta tickets y cincuenta proyectos y
se filtraban en el navegador. **Con miles de filas, lo que se busca puede no estar entre esas
cincuenta** y el desplegable saldría vacío para algo que sí existe: funcionaba en instalaciones
pequeñas y se degradaba en silencio en las grandes.

Ahora hay un parámetro `search` en tareas, tickets, proyectos y personas, y filtra la base de
datos. Se detalla en la sección 15.

---

## 14. Lo que queda anotado y sin resolver

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
- **El equipo de un ticket no se valida**: Ticketing guarda el identificador sin poder consultar el
  módulo de equipos. Un identificador inventado se guarda tal cual.
- **Las etiquetas de los tickets son claves de la pantalla** («billing»), no las entidades del
  módulo de etiquetas. Conviven dos sistemas y habrá que unificarlos.
- **Los adjuntos de un ticket no se pueden quitar**, y no hay nada que los borre del almacenamiento
  si algún día se vacía la papelera de tickets.
- **Espacios, carpetas, anotaciones y subidas no piden autorización por entidad**, y los
  permisos de páginas de documentos se comprueban sobre el módulo, no sobre el documento.
- **Compartir no tiene interfaz todavía.** Los endpoints existen y están probados, y los filtros
  «compartido conmigo» y «privado» funcionan contra ellos, pero no hay ningún botón en la
  aplicación que comparta. Hasta que lo haya, esas dos entradas del menú responden bien y
  devuelven poco, que es honesto pero no útil.
- **Archivar y borrar tampoco tienen botón.** Misma situación: la API responde, el menú enseña
  el archivo y la papelera, y de momento sólo se llenan desde la API.
- **`POST /api/v1/reports` devuelve la entidad de dominio, no el DTO.** Se cuelan `typeValue`,
  `formatValue`, `tenantId` y hasta `domainEvents` en la respuesta. No es explotable, pero expone
  la forma interna del agregado y ata la API a ella.
- **El informe de actividad por persona enseña identificadores, no nombres.** Los nombres viven
  en Identity y este informe ya cruza dos módulos; ponerles nombre exige un puerto nuevo, como el
  de favoritos.
- **No hay zona horaria por inquilino** y los informes programados usan la del servidor (11.3).
- **«Tickets por área» sigue sin poderse hacer.** Es el ejemplo que se pidió, y necesita campos
  personalizados en tickets como dimensión de análisis: hoy los campos personalizados son de
  tareas y proyectos. El catálogo del constructor está preparado para recibirlos —basta añadir
  campos a un origen— pero la extensión a tickets es trabajo del módulo de campos.
- **El constructor sigue enseñando tablas en su vista previa**, aunque el panel ya pinta gráficas
  con la misma definición. Reutilizar ahí el componente de gráfica es un cambio pequeño y
  pendiente.
- **«Barras apiladas» se pinta como barras normales.** Apilar necesita una segunda dimensión
  —agrupar por dos campos— que el catálogo no ofrece todavía. Se pinta en vez de rechazarse
  porque el resultado sigue siendo cierto, sólo que menos rico.
- **Los recuadros no se arrastran.** La disposición se guarda y se respeta, y hay endpoint para
  cambiarla, pero en la pantalla sólo se pueden quitar recuadros: falta el arrastrar y
  redimensionar.
- **`doughnut-chart` se ha quedado sin uso** al pasar la tarta a ser un widget. `line-chart` sigue
  vivo para el burndown.
- **De la Fase 5B faltan los bloques arrastrables y los comentarios en línea** (ver `FASE-5.md`).
- **Las menciones a personas no avisan a la persona mencionada.** Se guardan y se pueden consultar,
  pero mencionar a alguien no le manda una notificación. El tipo de aviso `Mention` ya existe en
  las preferencias, así que es enganchar el evento.
- **Un documento borrado deja sus menciones.** Se quitan al reescribir la página, no al borrar el
  documento, así que una tarea podría enseñar un enlace a un documento que ya no está. El enlace
  no rompe nada —lleva a una pantalla que dirá que no existe— pero conviene limpiarlo.
- **Agrupar por persona enseña identificadores.** Igual que el informe de actividad: los nombres
  viven en Identity y hace falta un puerto, como el de favoritos.
- **Documentos tiene la columna de archivado y no la usa.** Se le puso al modelar el concepto
  para no dejar el agregado a medias, pero Docs mantiene su propio panel lateral y no se ha
  enganchado al compartido.

---

## 15. Búsqueda por texto en las APIs

### 15.1 Un solo nombre para lo mismo

`search` en las cuatro listas, y `Buscar`/`TextoBuscado` en el `PaginationRequest` que comparten
tareas, tickets y proyectos. Poner el parámetro en cada módulo por separado habría dado tres
nombres para la misma operación, que es como el frontend acaba llamando `q` en un sitio y `search`
en otro, y alguien probando cuál funciona.

`TextoBuscado` recorta y devuelve nulo si sólo hay espacios: un cuadro de búsqueda que se vacía
mandaría `search=%20`, y sin eso sería un `LIKE '%   %'` —una lista vacía sin motivo aparente.

### 15.2 Las tildes las pone la base de datos

No se normalizan acentos ni mayúsculas en ninguna parte. La colación es `utf8mb4_0900_ai_ci`,
insensible a las dos cosas: comprobado contra los datos reales, `MODULO`, `modulo` y `módulo`
devuelven los mismos 46 tickets. El buscador de menciones tenía su propia función para quitar
tildes y se ha borrado; duplicaba lo que la base ya hace, y dos sitios haciendo lo mismo acaban
discrepando.

Queda una prueba de integración que lo fija. Si algún día cambia la colación, el síntoma sería
«las tildes dejaron de encontrarse» sin nadie sabiendo por qué.

### 15.3 El fallo que enseñó a escribir la prueba

El servicio de menciones pedía las personas a `/auth/users`, que no existe —la lista del inquilino
cuelga de `/users`— y **el `catch` genérico convertía el 404 en una lista vacía**. Las menciones
con `@` no encontraban a nadie nunca, sin un solo error en ninguna parte. Viajó en el commit de la
5B y se descubrió al leer las rutas, no probando.

Dos cambios: el error se registra en la consola en vez de desaparecer, y hay una prueba que fija
**las URL literales** que el servicio pide. Comprobar «se llamó a la API» contra un doble que
responde a cualquier ruta habría dejado pasar el fallo tal cual.

También se vio que devolver siempre `{}` al fallar rompía el desplegable unas líneas más abajo
—`{}.slice` no existe—, así que ahora el valor vacío lo pone quien llama.

### 15.4 Buscar de verdad se comprueba fuera de la primera página

La prueba central elige el último registro de la lista, comprueba que **no** está en la página
pedida y entonces lo busca. Sin esa comprobación previa, la prueba pasaría también con el filtro
en el cliente y no diría nada.

El tamaño de página sale de los datos —uno menos de los que haya— en vez de estar escrito. La
primera versión pedía cinco y suponía que el sembrador crea más: contra la base de desarrollo
pasaba, y contra el contenedor —cinco proyectos justos— fallaba sin que nada estuviera roto.

### 15.5 `/users` devolvía 683 filas para enseñar cinco

Se vio midiendo, no leyendo: la lista de personas no pagina y en la base de desarrollo son 683
filas —con duplicados, del sembrador que no es idempotente—. El desplegable de menciones se las
descargaba todas para quedarse con cinco.

Ahora acepta `pageSize`, **opcional**. No se pone un máximo por defecto a propósito: la pantalla
de administración pide esta misma lista, y recortarla en silencio escondería personas sin que
nadie se enterara. Hay prueba de las dos mitades.

---

## 16. El sembrador, las vistas y el calendario

### 16.1 695 usuarios, y «Mis proyectos» en cero

Dos sitios buscaban al administrador con el filtro de inquilino puesto: el arranque de `Program`
—`if (!identityCtx.User.Any())`— y el propio sembrador. En el arranque no hay petición, así que
`CurrentTenantId` vale `Guid.Empty` y **la comprobación decía «no hay usuarios» teniendo once**.
Cada arranque creaba otro `admin@acme.com` y los diez de demostración: 695 filas con 11 correos
distintos, +12 por arranque.

Y no se quedó en desorden. El inicio de sesión busca por correo y se queda con una fila cualquiera,
así que quien entraba no era el administrador que posee los proyectos: **«Mis proyectos» enseñaba 0
teniendo cinco**, y el desplegable de menciones ofrecía la misma persona cincuenta y siete veces.

Identity es el único contexto que no puede abrir `ComoInquilino` al principio —el inquilino sale
del propio administrador—, así que esas dos búsquedas ignoran los filtros y descartan los borrados
a mano. En cuanto se sabe el inquilino, Identity se comporta como los otros nueve.

### 16.2 El índice es lo que impide que vuelva

Arreglar el código no basta: dos instancias arrancando a la vez volverían a crear el duplicado
entre la lectura y la escritura. El correo pasa a ser único **en la base**, globalmente y no por
inquilino, porque el inicio de sesión busca sólo por correo —no hay dónde escribir el inquilino en
la pantalla de entrada—.

Dos detalles técnicos que costaron un intento cada uno:

- `Email` era `longtext`, y MySQL no lo indexa sin decirle cuántos caracteres mirar. Pasa a
  `varchar(320)`, el máximo de la norma.
- El índice **no puede declararse en el modelo**: `Email` es un tipo complejo y `HasIndex("Email")`
  intenta crear una propiedad sombra con ese nombre. Vive en la migración, en SQL.

La limpieza se queda con el más antiguo de cada correo —los proyectos y las tareas apuntan a ese— y
traslada al superviviente sus vistas, favoritos y permisos antes de borrar. 719 usuarios y 181
vistas quedaron en 11 y 4.

### 16.3 Los mismos duplicados en el calendario

184 eventos con cuatro títulos: cuarenta y seis copias de cada reunión. Misma causa.

La primera versión de la migración que los limpia comparaba también el organizador —dos eventos
homónimos de personas distintas sí son dos eventos— y **no borró ni una fila**: cada copia tenía un
organizador distinto, porque cada arranque creaba también un administrador nuevo. Los dos fallos
eran el mismo, y sólo se vio al medir el resultado en vez de dar por bueno el «Done» de la
migración.

Aquí no se pone índice único: dos reuniones distintas pueden llamarse igual y empezar a la vez en
salas distintas. Se limpia lo que un fallo generó; no se impone una regla que el calendario no
tiene.

### 16.4 Las pestañas de vista que desaparecían

En tickets y en tareas, las vistas de fábrica vivían dentro de un
`@if (savedViews().length === 0)`. Con una vista guardada —y el sembrador crea una— **se iban**. En
tickets eso dejaba sin ninguna forma de ver la lista; en tareas se llevaba además el Gantt y la
carga. Y como nadie llamaba nunca al endpoint de borrar vistas, que existía desde el principio, no
había vuelta atrás.

Las dos pantallas tenían el enredo copiado. Se extrae a `app-barra-de-vistas`, y con él se caen
tres cosas más: el `prompt()` que el navegador bloquea dentro de un marco —crear una vista no hacía
nada y no avisaba—, la aplicación de vistas que sólo entendía «board» y «list» —una vista de Gantt
se abría como tablero— y el desajuste `Tasks`/`WorkItems`, que tenía la vista sembrada guardada y
sin verla nadie.

### 16.5 El calendario hablaba otro idioma

La pantalla pedía `?from=&to=` donde la API lee `startDate` y `endDate`, trataba
`{ items, totalCount }` como si fuera un array, y al crear mandaba `startsAtUtc` donde el comando
espera `startTime`. Los tres fallos caían en el mismo `error: () => {}`: **el calendario salía
vacío siempre** y crear un evento no hacía nada, sin un solo mensaje.

En el servidor, tres más de la misma familia:

- **«Cancelar» era borrar.** `Cancel()` hacía un borrado lógico, así que anular una reunión la
  quitaba del calendario. Ahora son dos cosas: anulado se ve tachado y con motivo, la papelera
  quita de en medio y tiene vuelta.
- **`includeDeleted` no hacía nada.** Sólo se saltaba un `Where` a mano mientras el filtro global
  seguía escondiendo la fila, así que restaurar respondía «Evento no encontrado» teniéndola
  delante: lo que iba a la papelera no salía nunca.
- **El paginado mentía.** `Create(items, totalCount, page, pageSize)` recibía
  `(dtos, page, pageSize, totalCount)`.

### 16.6 Las fechas sin «Z», que corrían todos los eventos

Se descubrió midiendo: se creó una reunión a las 11:00 y la pantalla la enseñaba a las 16:00.

Las fechas se guardan en UTC, pero MySQL las devuelve con `Kind = Unspecified` y el serializador
escribía `"2026-09-17T16:00:00"`, **sin marca de zona**. El navegador lee una fecha sin zona como
local, así que cada evento se corría el desfase horario entero.

Se arregla en el servidor y no en el cliente a propósito: el que sabe que estas fechas son UTC es
quien las guarda, y taparlo en el navegador dejaría el mismo tropiezo puesto para los webhooks, las
exportaciones y cualquier otro consumidor. Al hacerlo apareció que el mapeo a DTO estaba escrito
dos veces y ya había empezado a separarse; ahora hay uno.

Queda dicho lo que esto no arregla: **no hay zona horaria por inquilino**. Cada quien ve la hora de
su ordenador, y los eventos de demostración están sembrados a las 08:00 UTC, que en UTC−5 son las
tres de la madrugada.

---

## 17. La capa visible: dos submenús, iconos que no había y vocabularios cruzados

Seis cosas señaladas mirando la aplicación, no leyendo el código. Cinco resultaron ser defectos y
la sexta no era nuestra.

### 17.1 Iconos declarados y no provistos

`ng-icon` **no falla cuando no encuentra un icono**: deja el hueco y escribe un aviso. Por eso
convivían dos síntomas que parecían de cosas distintas: el paginado enseñaba cuatro cuadros en
blanco donde van «primera / anterior / siguiente / última» —no declaraba ninguno de los cuatro— y
la consola repetía «No icon named lucidePlus» siete veces por carga, del «+» del armazón.

Un cuadro vacío es peor que no tener icono: parece un error de carga y nadie lo pulsa. Queda una
prueba que comprueba que hay `<svg>` dentro de cada botón, no que el botón existe —el botón
existía—.

### 17.2 El error de consola que no era nuestro

`Cannot read properties of undefined (reading 'startTime')`, con la pila entera en `<anonymous>` y
un `reportAllChanges` que no aparece ni en el código ni en las dependencias. Reproducido en un
navegador limpio, sin extensiones: **no sale**. Es de una extensión del navegador, no de la
aplicación.

Lo que sí era nuestro y se ha quitado: la bandera `allowSignalWrites`, que Angular ya ignora y sólo
dejaba un aviso de obsolescencia en cada carga.

### 17.3 Dos submenús, y el que mandaba era el falso

La barra lateral sacaba su propio desplegable al pasar el ratón, y la pantalla enseñaba además el
panel de navegación. Dos listas distintas de lo mismo, a un centímetro una de otra.

El de la barra era el malo:

- «X del Team» mandaba `?filter=team`, **que el servidor no conoce**: devolvía la lista entera.
  Es exactamente el fallo que la Fase 5A vino a eliminar, vivo en otro sitio.
- «Crear / Agregar» iba a `/{módulo}/new`, **que no es ninguna ruta**: la ruta comodín dejaba en
  Home sin decir nada. Igual «Crear Dashboard» y «Añadir o Crear Space», esta última sin ni
  siquiera destino.

Ahora el panel es el único submenú: se asoma al pasar el ratón por la barra, se ancla, y enseña el
módulo que se está señalando —asomarlo sobre Tickets estando en Tareas lleva a los tickets—. Los
módulos sin panel conservan su desplegable, ya sin las entradas que no llevaban a ninguna parte.

### 17.4 El panel se encogía con el contenido

Vivía dentro de cada pantalla, así que su alto era el del contenido: en el Gantt o en la carga de
trabajo —vistas cortas— se quedaba en un palmo, flotando. Al subirlo al armazón ocupa la pantalla
entera siempre, y las vistas pasan a `flex-1 min-h-0` en vez de altos fijos de `650px`.

Es el mismo cambio que arregla el punto anterior: un panel por aplicación, no uno por pantalla.

### 17.5 Barras de desplazamiento sin estilo

No había ninguna regla de `scrollbar` en todo el proyecto, así que salía la del sistema: gruesa,
gris, con su canal. En un cajón lateral de fondo claro se ve como un tablón.

Y en los cajones de detalle salían **dos, anidadas**: el cuerpo del cajón se desplazaba y además lo
hacía cada columna. El cajón admite ahora apagar su propio desplazamiento cuando el contenido ya
gestiona el suyo.

### 17.6 Tres vocabularios para un ticket

El tablero decía `Open / InProgress / Resolved / Closed`, el cajón de detalle
`New / In Progress / Resolved / Closed`, el alta `Low / Medium / High / Urgent`. El servidor dice
otra cosa: existe `PendingInfo`, no existe `New`, y la prioridad más alta se llama `Critical`.

Se veía y no se entendía:

- Al abrir un ticket, **los desplegables de estado y prioridad salían en blanco** —«Open» no estaba
  entre las opciones y «Low» tampoco, porque las claves iban en minúscula— mientras la etiqueta de
  arriba decía «Open» y «Low».
- Un ticket en `PendingInfo` **no caía en ninguna columna del tablero**: ni borrado ni archivado,
  simplemente invisible.
- Crear con prioridad «Urgent» guardaba la prioridad por defecto sin avisar.

Un solo sitio, `vocabulario-de-tickets.ts`, con las claves del servidor y su nombre en castellano.
Las insignias enseñan el nombre y no la clave: antes ponía «Open» junto a un desplegable que decía
«Abierto».

---

## 18. Un solo panel para todos los módulos, y el idioma a medias

### 18.1 El panel llegaba a tres módulos de diez

La entrega anterior movió el panel de navegación al armazón, pero sólo para tareas, tickets y
proyectos. El resto —Inicio, Panel, Documentos, Equipos, Chat, Calendario, Informes— se quedaba con
el desplegable viejo, así que **la barra lateral se comportaba de dos maneras según dónde pusieras
el ratón**.

Ahora el vocabulario cubre todos, y un módulo que se añada al menú **recibe panel sin tocar nada**:
`vocabularioDe` le da su nombre y la entrada «Ver todo», que es lo único que se puede afirmar sin
conocerlo. Inventarle «Mis X» o «X del equipo» sería repetir el fallo que se acaba de quitar.

Para que eso fuera posible, una entrada del panel puede llevar a otra ruta o fijar parámetros que
no son `?filter=`: el de Inicio lleva a las tareas y los tickets de uno, y el de Documentos cambia
de pestaña con `?tab=`.

### 18.2 Documentos era la excepción que sostenía el problema

Su panel tenía además de las pestañas una lista de favoritos y otra de páginas recientes, y por eso
era el único módulo con barra lateral propia: nadie de fuera podía pintar esos datos. El resultado
era el doble submenú, el panel compartido y el suyo justo al lado.

Dos cambios lo resuelven: las pestañas pasan de estado interno a `?tab=` —así las mueve el panel
compartido, y de paso una pestaña se comparte por enlace y responde al botón de atrás—, y un
servicio pequeño (`SeccionesDelPanelService`) deja que un módulo entregue secciones propias al
panel. El módulo empuja lo suyo; el panel sólo sabe pintar secciones.

Se ha caído «Popular Wikis», un recuadro fijo que prometía que ahí aparecerían los wikis más vistos.
No aparecían: no hay nada que los cuente.

### 18.3 Desanclar no escondía nada

El anclaje era **una sola bandera para toda la aplicación**, y hacía tres cosas mal: desanclar no
recogía el panel —seguía visible—, anclar en Tareas anclaba también en Documentos, y al cambiar de
módulo no se consultaba nada.

Ahora hay un anclaje por módulo. Sin anclar, el panel se asoma al pasar el ratón por su icono y se
recoge al salir de la barra. Anclado, se queda y empuja el contenido. **Al cambiar de módulo manda
el anclaje del módulo de la pantalla**, no el del que se estaba asomando: si mandara el señalado,
asomar uno anclado dejaría el panel abierto para siempre.

### 18.4 El idioma: no faltaban traducciones, faltaban marcas

La aplicación se ve mitad en español y mitad en inglés **elijas el idioma que elijas**, y no es que
falten traducciones: hay 565 y las plantillas están marcadas casi al completo. Lo que había era:

- **172 etiquetas escritas en TypeScript sin `$localize`**: las columnas de las tablas —«Title»,
  «Status», «Due Date»—, los nombres de los módulos de la barra —«Home», «Dashboard», «Docs»,
  «Teams» en inglés junto a «Proyectos» y «Tareas» en español— y las pestañas de vista. Esas no se
  traducen nunca: salen igual en las dos versiones.
- **18 cadenas marcadas cuyo texto de origen estaba en inglés.** Como el idioma de origen es `es`,
  esas salen en inglés **en la versión española**: «All Docs», «My Docs», «Shared with me».
- **Claves del servidor enseñadas en crudo**: «To Do», «In Progress». Son datos, no texto, y por eso
  no se traducen; hay que enseñar su nombre. El tablero de tareas ya lo hacía y el cajón de detalle
  no, así que la misma tarea decía dos cosas según dónde se mirara.

Los tres se han corregido, y las claves ahora viven junto a su nombre en
`vocabulario-de-tareas.ts` y `vocabulario-de-tickets.ts`, que es lo que impide que alguien traduzca
una clave por descuido y rompa el guardado.

### 18.5 El cajón de tareas tenía el doble de márgenes

El cuerpo del cajón ponía `p-6` y la columna izquierda añadía `px-8 py-6` encima: el contenido
empezaba a cincuenta y seis píxeles del borde. Además se desplazaban las dos, así que salían dos
barras anidadas. Mismo tratamiento que el de tickets: un solo desplazamiento, separadores finos en
vez de líneas gruesas, y los campos sin recuadro permanente.

### 18.6 El idioma, hasta el final: faltaban 84 traducciones y 121 marcas

La entrega anterior marcó lo que se veía en las pantallas señaladas, y eso **no bastaba**. Al
comparar lo que la aplicación marca contra lo que el fichero inglés traduce salió el número que
explica la mezcla:

| | |
|---|---|
| cadenas que la aplicación marca | 604 |
| con traducción al inglés | 520 |
| **sin traducción al inglés** | **84** |

Una cadena marcada sin traducción **no da error**: Angular usa el texto de origen. Como el idioma de
origen es `es`, esas 84 salían **en español dentro de la versión inglesa**. Marcar sin traducir
mueve el problema, no lo resuelve.

Y quedaban dos patrones que ningún barrido anterior tocaba:

- **Ternarios en plantilla**: `{{ loading() ? 'Ingresando...' : 'Ingresar' }}`. No se pueden marcar
  con `i18n` tal cual —el atributo marca elementos, no expresiones—, así que se convierten en
  `@if`/`@else` con un `<span i18n>` en cada rama. Eran 26.
- **Mensajes en TypeScript**: los avisos y los errores que se enseñan (`error.set('Credenciales
  inválidas')`). Eran 69.

Un aviso sobre el barrido automático: envolvió también los `console.error`, que son para quien
programa y no se traducen; se deshizo. Y en un componente con plantilla en línea metió un
`$localize` —que usa comillas invertidas— **dentro** de la plantilla, que también las usa: la cortó
a media frase y dejó el fichero sin compilar. Por eso el título de ese cajón se calcula ahora en un
método.

También apareció, por el camino, que la aplicación **no compilaba para producción**: un `i18n`
anidado dentro de otro (`NG5002`). `ng serve` no lo ve porque desactiva la traducción, así que el
fallo sólo salía al construir de verdad.

**Cómo se comprueba.** No con un `grep` de palabras españolas en el paquete inglés: ahí conviven
las claves del servidor —«In Progress», «To Do» son datos, no texto— con las traducciones, y el
resultado son falsos positivos. Se comprueba de dos maneras: que no quede ninguna unidad sin
traducir, y que para dieciséis parejas conocidas cada paquete tenga **su** versión y no la otra.

### 18.7 Cómo elegir idioma en el contenedor

No hay nada que ajustar: la imagen compila los dos y `nginx` elige por el `Accept-Language` del
navegador, con el español por defecto. Con el navegador en inglés se entra a `/en/`. Para ver la
otra versión basta con ir a `http://localhost:4200/es/` —la redirección de la raíz es 302, no 301,
precisamente para que no quede fijada—.

---

## 19. El idioma que no se podía elegir, y un perfil que daba error al abrirse

### 19.1 `/es` llevaba al inglés

`location /es/` exige la barra final, así que escribir `/es` caía en el `location /` de más abajo y
se convertía en `/en/es`: la aplicación **inglesa** intentando abrir una ruta llamada «es», que su
enrutador no conoce, y de ahí a la pantalla de inicio. El idioma acababa siendo justo el contrario
del que se pedía.

Se añaden `location = /es` y `= /en` que redirigen con barra.

### 19.2 El idioma lo decidía el navegador y sólo el navegador

`nginx` elegía por `Accept-Language` y no había ninguna otra forma de influir: quien tuviera Chrome
en inglés se quedaba en inglés para siempre, sin nada que tocar dentro de la aplicación.

Ahora la elección de la persona manda sobre la del navegador. Se guarda en una cookie —y no en
`localStorage`— porque **quien decide a qué carpeta enviar la visita es el servidor**, y el servidor
no ve `localStorage`. El selector vive en el perfil y conserva la pantalla al cambiar:
`/en/profile` lleva a `/es/profile`, no al inicio.

### 19.3 Las vistas recientes congelaban el idioma

En la versión inglesa seguían apareciendo «Proyectos» y «Tareas». No era una traducción que
faltara: **`RecentView` guardaba el nombre ya escrito y eso se persiste en el servidor**. Entrabas
una vez a Proyectos en español y esa palabra quedaba grabada en tus preferencias, en el idioma del
día en que se guardó.

Ahora se guarda el identificador del módulo y el nombre se resuelve al pintarlo. Es la misma regla
de siempre: en la base van claves, no texto de pantalla.

### 19.4 El perfil pedía dos rutas que no existen

Al abrirlo salía «El recurso solicitado no existe» encima de un formulario en blanco: pedía
`GET /users/me`, y el usuario actual está en `/auth/users/me`. Cambiar la contraseña llamaba a
`/profile/password`, que tampoco existe.

Y ahí apareció lo importante. **El comando de cambiar contraseña, su handler y su validador
existían desde el principio y ningún endpoint los exponía**; al conectarlo, la primera prueba contra
la API levantada devolvió esto:

    PUT /users/me/password con la contraseña actual equivocada -> 204

El handler recibía `CurrentPassword` y **no la miraba**. Cambiaba la contraseña igual. Nunca estuvo
expuesto —por eso nadie lo sufrió— pero eso es suerte y no diseño: en cuanto la pantalla lo llama,
cualquiera que encuentre una sesión abierta se queda con la cuenta sin conocer la contraseña. Se
verifica con la misma función que el inicio de sesión, porque dos formas de comprobar la misma
contraseña acaban discrepando.

La prueba que queda no es «cambia la contraseña» —la versión rota pasaba esa con nota— sino **«no
la cambia si la anterior no es la buena»**, comprobando además que la de siempre sigue sirviendo.

### 19.5 El tema existía y no se podía encender

Toda la aplicación tiene variantes `dark:` —cientos— y la única forma de verlas era un atajo de la
paleta de comandos que hacía `classList.toggle('dark')` sin guardar nada: al recargar se perdía.
Medio diseño existía y no se podía usar.

Ahora hay tres opciones y «el del sistema» es la de fábrica: quien tiene el ordenador en oscuro
espera que las aplicaciones lo respeten sin decírselo a cada una. Se guarda en `localStorage` y no
en el servidor a propósito — es una preferencia del aparato: el mismo usuario puede querer oscuro
en el portátil de noche y claro en el monitor de la oficina.

### 19.6 Dos iconos iguales en la barra

«Admin» y «Customizar menú» usaban los dos el engranaje, uno encima del otro y con destinos
distintos. Admin pasa a un escudo.

## 20. Las plantillas repetidas, y el ticket que no se dejaba mover

### 20.1 Cuarenta y cinco copias de cada documento sembrado

«Mis plantillas» era una cuadrícula que no acababa: la misma tarjeta, «Plantilla: Especificación de
Producto (PRD)», cuarenta y cinco veces. Debajo estaba el listado de documentos, fuera de la vista.

Es la cuarta aparición del mismo fallo, con la misma causa que los 695 usuarios y los 184 eventos:
sin petición HTTP el inquilino vale `Guid.Empty`, la comprobación «¿ya hay documentos?» respondía
que no, y los tres documentos de demostración se creaban otra vez en cada arranque. La base de
desarrollo llegó a 162 documentos con nueve títulos.

El sembrador dejó de crearlos hace dos entregas —abre `ComoInquilino` antes de mirar— pero lo ya
creado se queda: 132 filas que ninguna pantalla iba a limpiar sola. La migración
`LimpiarDocumentosSembradosDuplicados` conserva la copia más antigua de cada
(inquilino, título, tipo).

**Sólo toca los tres títulos que siembra el sembrador, y sólo en sus tipos.** Un documento creado
*a partir* de una plantilla se llama igual que ella, y borrar por título a secas se llevaría por
delante el trabajo de alguien. Tampoco se añade un índice único, a diferencia del correo de
usuario: dos documentos con el mismo título son perfectamente legítimos.

### 20.2 Cuatro plantillas, y que sean las que se usan

La galería enseñaba las cuatro predefinidas y, debajo, **todas** las del equipo en otra rejilla
aparte. Dos sitios donde mirar antes de elegir, y ninguno de los dos con un límite.

Ahora las dos clases son la misma lista: cuatro tarjetas a la vista y el resto detrás de «Ver más»,
que abre un cajón —como el resto de los módulos— con buscador y agrupadas por procedencia. Dentro
del cajón sí importa de dónde sale cada una: una plantilla del equipo se puede editar y borrar, y
una del sistema no.

Las cuatro que se ven son las más usadas. Eso necesitaba un contador que no existía, y ahí estaba
la decisión: **guardarlo en el navegador habría sido más barato**, pero entonces cada persona vería
un orden distinto y quien entrara nuevo vería cuatro plantillas al azar. La tabla `UsosDePlantilla`
cuenta por inquilino, con una fila por plantilla y unicidad en la base — sin ella, dos personas
creando a la vez desde la misma plantilla harían dos filas y el contador se repartiría entre ambas
sin subir nunca.

La clave es de texto porque las predefinidas **no son filas**: viven en el código, identificadas
por clave. Una sola columna admite las dos y evita tener dos contadores que se cuentan distinto.

El contador sube dentro de la misma transacción que la creación. Contarlo desde el cliente dejaría
la galería ordenada por «veces que alguien pulsó», no por «documentos que salieron de aquí».

Y una entrada nueva en el panel de Documentos, «Mis plantillas», que lista sólo las del equipo.

### 20.3 El ticket que no se dejaba mover

Arrastrar una tarjeta entre columnas soltaba dos avisos: uno rojo con
`No service for type 'MediatR.IRequestHandler\`2[Ticketing.Application.Commands.UpdateTicketCommand...`
y otro diciendo «No se pudo mover el ticket».

`PATCH /tickets/{id}` estaba publicado, el comando existía y **el handler no**. MediatR no tenía a
quién entregarle el comando, así que cada intento acababa en 500. Guardar la ficha de un ticket
tampoco guardaba nada, por lo mismo. Es el patrón de siempre en este proyecto: la pantalla ofrece
algo que el servidor nunca honró.

La actualización es parcial a propósito. Arrastrar manda sólo el estado, y una actualización total
borraría la descripción del ticket cada vez que alguien mueve una tarjeta.

El estado se valida **antes** de tocar nada. Aplicando los campos uno a uno, un estado inválido
dejaría el ticket con el título nuevo y el estado viejo: medio guardado, con la pantalla enseñando
un error como si no se hubiera guardado nada.

Y el endpoint devolvía 404 ante cualquier fallo, así que un título de tres letras se leía como si el
ticket hubiera desaparecido. Ahora el 404 es sólo para el ticket que no existe.

### 20.4 El cajón no decía que era un diálogo

El modal de plantillas al que sustituye sí llevaba `role="dialog"`. Al pasarlo a cajón se habría
perdido: un lector de pantalla leería un grupo cualquiera en vez de «diálogo, Plantillas». Se
arregla en el cajón compartido, así que lo heredan los de todos los módulos.

### 20.5 Lo que quedaba en inglés en Documentos

«Save as Template», «Sort: Updated», «Filters», «Location», «Date updated», los nombres de las
cuatro plantillas predefinidas. Estaban marcadas para traducir pero **escritas en inglés**, y el
idioma de origen del proyecto es el español: se veían en inglés dentro de la versión española y no
había forma de traducirlas al inglés porque ya lo estaban.

El contenido que genera cada plantilla predefinida sigue en inglés dentro del handler. Eso es un
arreglo aparte y más grande: son documentos enteros.

### 20.6 Lo que queda

**Los tickets sembrados están duplicados igual**: 230 en «Abierto» con cinco títulos. El sembrador
ya no los crea, pero la limpieza no se ha hecho — borrar 225 tickets es una decisión de quien es
dueño de los datos, no una consecuencia de arreglar las plantillas.
