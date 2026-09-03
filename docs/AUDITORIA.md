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
salida. **Un paso de CI que no puede fallar no es un paso de CI.** Por eso el plan
añade un umbral que sí rompe el build.

Resuelto: `coverlet.collector` en ambos proyectos.

### 2.2 Dos referencias de proyecto rotas — resuelto

`tests/IntegrationTests/IntegrationTests.csproj` contenía:

```xml
<ProjectReference Include="....srcModulesCustomFieldsCustomFields.Infrastructure..." />
```

Las barras invertidas se habían perdido en algún reemplazo automático. MSBuild lo
degradaba a aviso (MSB9008) y compilaba igual, porque `ApiHost` arrastra esos dos
proyectos de forma transitiva. Es decir: la referencia estaba rota **y era
redundante**. Se eliminaron las dos líneas; el proyecto compila ahora con 0 avisos.

### 2.3 Un árbol de trabajo de git abandonado

`.claude/worktrees/goofy-khorana-d7c41d` sigue registrado, apuntando a la rama
`claude/goofy-khorana-d7c41d` en el commit `d0650fa`. Contiene una copia completa
del repositorio, incluidos duplicados de los `Dockerfile`. Conviene revisar si tiene
algo que rescatar y, si no, retirarlo.

## 3. Cobertura: el punto de partida real

Medido, no estimado. Sólo con las pruebas unitarias:

```
Líneas cubiertas globalmente ........ 18,0 %
```

Ese 18 % es engañoso, y en la dirección pesimista. De las 16.278 líneas que cuenta
el informe, **7.366 son migraciones de Entity Framework** — código generado que
nadie escribió y que ninguna prueba unitaria puede ni debe ejecutar. Son el 45 % del
total y están todas a cero.

Descontándolas, la cifra sobre código escrito a mano es:

```
Código propio ....................... 32,9 %  (2.928 / 8.912)
Frontend (Karma, líneas) ............ 47,3 %  (558 / 1.179)
```

Reparto por ensamblado, ordenado por lo que más pesa:

| Ensamblado | Cubierto | Total | % |
|---|---:|---:|---:|
| Comments.Domain | 100 | 102 | 98,0 |
| Automations.Domain | 222 | 236 | 94,1 |
| WorkItems.Domain | 568 | 636 | 89,3 |
| Ticketing.Domain | 140 | 166 | 84,3 |
| Webhook.Domain | 48 | 66 | 72,7 |
| CustomFields.Domain | 176 | 268 | 65,7 |
| Ticketing.Application | 140 | 264 | 53,0 |
| Projects.Domain | 144 | 278 | 51,8 |
| Webhook.Application | 116 | 228 | 50,9 |
| Calendar.Domain | 152 | 310 | 49,0 |
| BuildingBlocks.Domain | 104 | 222 | 46,8 |
| Projects.Application | 148 | 366 | 40,4 |
| Calendar.Application | 170 | 472 | 36,0 |
| Identity.Domain | 130 | 480 | 27,1 |
| WorkItems.Application | 248 | 986 | 25,2 |
| Identity.Application | 142 | 940 | 15,1 |
| BuildingBlocks.Infrastructure | 62 | 1.228 | 5,0 |
| Identity.Infrastructure | 102 | 2.334 | 4,4 |
| Projects.Infrastructure | 16 | 1.326 | 1,2 |
| BuildingBlocks.Application | 0 | 124 | 0,0 |
| Calendar / Ticketing / Webhook / WorkItems .Infrastructure | 0 | 5.230 | 0,0 |

Dos lecturas que cambian el plan:

1. **El dominio ya está bien cubierto.** Donde se aplicó la disciplina de funciones
   puras —`WorkItems`, `Automations`, `Comments`, `Ticketing`— la cobertura va del
   84 % al 98 %. Ahí no hay trabajo que hacer.
2. **Lo que falta es la capa de aplicación**, sobre todo `Identity.Application`
   (15 %) y `WorkItems.Application` (25 %). Son manejadores de comandos: lógica de
   autorización, validación y orquestación. Es exactamente el sitio donde un fallo
   no lo ve nadie hasta producción, y es barato de probar.

**Hay ensamblados que ni siquiera aparecen** en el informe, porque `UnitTests` no
los referencia: `Comments.Application`, `Comments.Infrastructure`, `Automations.*`
salvo el dominio, `Reporting.*`, `Docs`, `Tags`, `Teams`, `Notifications` y
`Communication`. La cobertura real del backend completo es **más baja que 32,9 %**.
Antes de fijar una meta hay que hacerlos visibles: una métrica que omite lo no
probado siempre miente a favor.

### Sobre la meta del 60 %

Es alcanzable, pero el número sólo significa algo si se define primero **sobre qué**
se mide. Propuesta:

- Excluir migraciones de EF y `Program.cs` — código generado o de arranque.
- Incluir **todos** los módulos, también los que hoy no se referencian.
- Medir la unión de unitarias **e integración**; las de integración cubren
  `Infrastructure` y los endpoints, que las unitarias no pueden alcanzar.

Con esa definición, el 60 % es trabajo de verdad pero razonable, y no se llega
inflando la cifra con pruebas que sólo comprueban códigos de estado. Esa lección
ya se pagó en la Fase 4: *una prueba que sólo mira el código de estado no ve un
guardado que no guarda.*

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
