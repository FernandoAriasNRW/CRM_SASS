# Auditoría del proyecto — 3 de septiembre de 2026

Este documento recoge lo que se midió, no lo que se supone. Cada número de aquí
salió de ejecutar algo; los que no se pudieron medir se dicen como tales.

## 1. Lo que está verde

| Suite | Resultado |
|---|---|
| Unitarias backend (xUnit) | 281 pasan, 0 fallan |
| Integración (Testcontainers + MySQL) | 97 pasan |
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
Cobertura de líneas ................. 68,8 %   (3.710 / 5.390)
Cobertura de ramas .................. 55,3 %   (381 / 688)
Cobertura de métodos ................ 56,9 %   (579 / 1.017)
Frontend, líneas (Karma) ............ 47,7 %   (562 / 1.179)
```

64 ensamblados, 509 clases, 349 archivos.

**El objetivo del 60 % ya estaba cumplido; nadie lo había calculado nunca.** Conviene
decirlo sin adornos: no es mérito de este trabajo, es que la medición faltaba. El
mérito, si hay alguno, es que ahora la cifra existe y no puede bajar en silencio.

Queda por debajo del 60 % la **cobertura de ramas** (55,3 %), que es la métrica más
exigente y la más informativa: mide si se probaron los dos lados de cada `if`, no
sólo si la línea se ejecutó. Ahí sí hay trabajo.

### El reparto, y lo que delata

Bien cubierto, donde se aplicó la disciplina de funciones puras:

| Ensamblado | % |
|---|---:|
| Comments.Domain | 100 |
| WorkItems.Infrastructure | 100 |
| Automations.Domain | 95,2 |
| WorkItems.Domain | 96,7 |
| WorkItems.Application | 93,1 |
| CustomFields.Domain | 88,9 |
| Ticketing.Domain | 84,8 |

Y lo que falta, ordenado por lo que más importa:

| Ensamblado | % | Por qué importa |
|---|---:|---|
| Reporting.Domain | **0** | ver más abajo |
| Reporting.Application | **0** | ver más abajo |
| Tags.Application | **0** | |
| Teams.Application | **0** | |
| Communication.Application | **0** | |
| Notifications.Application | **0** | las notificaciones son el canal del que depende la exportación asíncrona de la Fase 5 |
| Docs.Application | 9,3 | el editor es el bloque 5B |
| Identity.Application | 23,6 | autorización: un fallo aquí no lo ve nadie hasta producción |
| Identity.Domain | 38,4 | |
| Notifications.Domain | 38,9 | |
| Docs.Domain | 41,2 | |
| Communication.Domain | 43,4 | |
| Calendar.Domain | 46,2 | |
| BuildingBlocks.Infrastructure | 47,7 | lo comparten todos los módulos |

### El patrón que hay que investigar

Seis módulos repiten la misma forma: **la capa de presentación muy cubierta y la de
aplicación a cero.**

| Módulo | Presentation | Application | Domain |
|---|---:|---:|---:|
| Reporting | 95,2 % | **0 %** | **0 %** |
| Tags | 100 % | **0 %** | 50 % |
| Teams | 100 % | **0 %** | 67,8 % |
| Notifications | 100 % | **0 %** | 38,9 % |
| Communication | 88,7 % | **0 %** | 43,4 % |
| Docs | 100 % | 9,3 % | 41,2 % |

Los endpoints se ejecutan —las pruebas de integración los llaman y responden— pero
el dominio y los manejadores **no se ejecutan nunca**. Eso no es falta de pruebas:
es que la petición no llega a la lógica. O los endpoints resuelven por su cuenta
contra la base de datos saltándose la capa de aplicación, o devuelven algo fijo.

Es el mismo olor que tenían los comentarios en la Fase 4, donde la interfaz llamaba
a un endpoint que nunca existió y las pruebas no lo veían porque miraban el código
de estado. **Una prueba que sólo mira el código de estado no ve un guardado que no
guarda.** Investigarlo es el bloque 2 del plan, y es imprescindible antes de
construir el dashboard encima de Reporting.

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

`src/Modules/Reporting` ya existe con 1.348 líneas: entidades `Report` y `Dashboard`,
modelos de lectura de tareas, proyectos y tickets, consumidores que los alimentan,
repositorios y dos familias de endpoints. El plan de la Fase 5 se escribió como si
5C y 5D empezaran en blanco, y no es así. Hay que auditar qué de eso funciona de
verdad contra la API levantada antes de decidir qué se reescribe — con el precedente
de los comentarios muy presente, donde la interfaz llamaba a un endpoint que nunca
existió.
