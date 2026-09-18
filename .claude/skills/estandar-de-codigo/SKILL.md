---
name: estandar-de-codigo
description: Estándar de este repositorio (CRM SaaS Suite) para escribir o cambiar código - nombres en inglés con el glosario del proyecto, comentarios en español, principios SOLID aplicados a los problemas reales que tiene, y las comprobaciones obligatorias antes de dar un cambio por bueno. Úsala al añadir una funcionalidad, refactorizar, renombrar, crear un endpoint, un comando o una entidad, tocar la base de datos o el contrato de la API, y al revisar un cambio de otro.
---

# Estándar de código del CRM SaaS Suite

La referencia completa —glosario español→inglés, convenciones y el plan de bloques— está en
`docs/ESTANDAR-DE-CODIGO.md`. **Lee ese documento antes de renombrar o de crear nombres nuevos.**
Esta skill es lo que hay que hacer y lo que hay que comprobar.

## 1. Las tres reglas de idioma

| | |
|---|---|
| **Identificadores** | **Inglés.** Clases, métodos, propiedades, variables, ficheros, carpetas, rutas de la API, campos JSON, tablas y columnas. |
| **Comentarios, XML docs, `docs/`** | **Español.** Explican el *por qué*; no se traducen. |
| **Textos de la interfaz** | **Español**, que es el idioma origen del i18n (`web/src/locale/messages.xlf`). |

Un concepto tiene **un solo nombre** en todo el repositorio. Si el nombre que necesitas no está en
el glosario de `docs/ESTANDAR-DE-CODIGO.md` §3, **añádelo ahí en el mismo cambio**. Traducir el
mismo concepto de dos maneras en dos módulos es el riesgo real de este trabajo, no traducir mal.

## 2. Convenciones que se dan por supuestas

**Backend (.NET):** tipos y miembros `PascalCase`; parámetros y locales `camelCase`; campos
privados `_camelCase`; interfaces con `I`; asíncronos con sufijo `Async`; `VerbNounCommand`,
`GetNounQuery`, `…Handler`, `…Dto`; **un tipo público por fichero**, con el nombre del tipo.

**Frontend (Angular):** clases `PascalCase`; señales y métodos `camelCase`; constantes de módulo
`UPPER_SNAKE_CASE`; ficheros `kebab-case` con su tipo (`intake-keys.component.ts`); selectores
`app-…`; `data-testid` en inglés.

**API:** rutas en `kebab-case` y plural (`/api/v1/tickets/intake-keys`); JSON en `camelCase`.
**Base de datos:** tablas `PascalCase` y plural.

## 3. Lo que este repositorio ya aprendió (no lo repitas)

- **El inquilino y el usuario salen de `IUserContext`.** Nunca leas los claims a mano en un
  endpoint. Esa repetición ya causó una escritura entre organizaciones.
- **La hora se inyecta con `TimeProvider`**, no con `DateTime.UtcNow`, en todo lo nuevo.
- **Autorización:** un comando de escritura implementa `IAuthorizeEntity` (`EntityType`,
  `EntityId`, `RequiredPermission`). Sin eso, el nivel por rol se guarda y no se aplica.
- **Nada de dos vocabularios para lo mismo.** `EntityTypes` (BuildingBlocks) es el único catálogo
  de tipos de entidad; `PermissionTypes.FromEntityType` el único traductor al de permisos.
- **Módulos aislados:** ningún módulo referencia a otro. Lo que cruza se declara como puerto en
  BuildingBlocks y lo satisface el host (`src/Host/ApiHost/Startup/ModuleRegistration.cs`).
- **Arranque:** cada paso vive en `src/Host/ApiHost/Startup/`. `Program.cs` sólo ordena. El orden
  del pipeline importa: el limitador de peticiones corre **antes** de la autenticación.
- **Siembra de demo:** un `IModuleSeeder` por módulo en `src/Host/ApiHost/Seeding/`, idempotente.
- **Migraciones:** renombrar es `RenameTable` / `RenameColumn` / `RenameIndex`, **nunca** borrar y
  crear. EF propone `DropTable` + `CreateTable` al cambiar un nombre, y eso borra los datos: hay
  que reescribir la migración a mano y comprobarla contra la base de desarrollo con datos.
- **Nombres visibles fuera:** cambiar una ruta, un campo JSON o un valor guardado es un cambio de
  contrato. Busca quién lo consume (`web/`, pruebas, integraciones) antes de cambiarlo, y si hay
  datos guardados con el valor viejo, migra o sigue sirviendo el viejo (ver `/almacen` → `/storage`).

## 4. SOLID, aplicado a este proyecto

No es una reescritura: se corrige lo que tenga un coste concreto.

- **S** — Un fichero con diez tipos, una clase de 700 líneas o un componente que guarda, exporta y
  pinta a la vez: sepáralo. Para los `*Cqrs.cs`, usa `tools/scripts/split-types.py`.
- **O** — Si añadir un caso obliga a tocar un `switch` o un `if` de tipos, mete una estrategia.
- **L** — Una implementación no puede pedir menos ni prometer menos que su interfaz.
- **I** — Las lecturas de pantalla van por `I…Queries`; las escrituras por `I…Repository`.
- **D** — La aplicación no conoce `DbContext`, `HttpContext` ni la hora del sistema: los recibe.

## 5. Comprobaciones obligatorias antes de dar el cambio por bueno

En este orden. Las tres primeras son las que han pillado fallos que compilaban.

```bash
# 1. Compila todo
dotnet build CrmSaaS.sln

# 2. ¿Se ha movido el modelo de datos sin migración? (un renombrado se propaga por interfaces
#    a otros módulos y puede mover una columna). Repetir con los 14 contextos.
dotnet ef migrations has-pending-model-changes --context <XDbContext> \
  --project src/Modules/<X>/<X>.Infrastructure/<X>.Infrastructure.csproj \
  --startup-project src/Host/ApiHost/ApiHost.csproj

# 3. ¿Ha cambiado algún campo del JSON sin querer? Compila main aparte y compara.
dotnet run --project tools/contract-snapshot -- <bin-de-main>  > /tmp/main.txt
dotnet run --project tools/contract-snapshot -- <bin-de-la-rama> > /tmp/rama.txt
diff /tmp/main.txt /tmp/rama.txt     # cada campo que cambie, búscalo en web/ por el nombre viejo

# 4. Pruebas
dotnet test tests/UnitTests
dotnet test tests/IntegrationTests          # necesita Docker levantado
cd web && npm run lint && npx ng build && npx ng test --watch=false --browsers=ChromeHeadless
npx playwright test --workers=4             # ~1 falla intermitente de accesibilidad en paralelo

# 5. i18n, SIEMPRE AL FINAL: re-extraer y comprobar que el catálogo queda estable
cd web && npm run i18n:extract
git diff --quiet src/locale/messages.xlf && echo "catálogo estable"
grep -c 'state="needs-translation"' src/locale/messages.en.xlf   # tiene que dar 0
```

El paso 5 va al final **de verdad**: `ng extract-i18n` guarda el número de línea de cada texto, así
que cualquier edición posterior desfasa el catálogo y el CI lo rechaza. Ya costó una vuelta.

Y si el cambio se ve en la aplicación, compruébalo levantado (`docker compose up -d --build api`),
no sólo con pruebas. La mitad de los fallos de este proyecto —subidas que no subían, columnas que
no existían, exportaciones vacías— aparecieron así.

## 6. Renombrar

Con `tools/rename-symbols`, que usa Roslyn y cambia la declaración y todas sus referencias, nunca
con buscar y reemplazar (toca textos, comentarios y nombres que coinciden por casualidad):

```bash
printf 'ruta/al/Fichero.cs\tNombreViejo\tNewName\n' > /tmp/mapa.tsv
dotnet run --project tools/rename-symbols -- CrmSaaS.sln /tmp/mapa.tsv --dry-run
dotnet run --project tools/rename-symbols -- CrmSaaS.sln /tmp/mapa.tsv
```

Después: los pasos 2 y 3 de la sección 5 **sin excepción**, los comentarios que citaban el nombre
viejo (`<see cref>` los actualiza Roslyn; el texto suelto no), y el nombre del fichero y la carpeta.

**Las plantillas de ruta son cadenas y Roslyn no las toca.** Si se renombra el parámetro de la
lambda de un endpoint (`entidad` → `entityType`), la ruta `"/{entidad}/..."` se queda igual, la API
arranca y la petición da 400. Busca `{nombreViejo}` en los `Map*` después de cada renombrado.

**En el frontend** no hay Roslyn: `python tools/scripts/rename-frontend.py <mapa> <rutas>` (mapa de
dos columnas, viejo y nuevo). Trabaja por tokens, así que después, además del build, **compara con
`main` los atributos y las cadenas de las plantillas**: en el bloque 4b tradujo 18 textos de la
interfaz y ni el build ni las pruebas lo notaron. Los nombres de inputs, outputs y selectores van a
mano. Límites en `tools/README.md`.

Para ver qué queda en español: `python tools/scripts/spanish-identifiers.py <rutas>`
(necesita `pip install wordfreq`).

## 7. Trampas del entorno que hacen perder una vuelta de CI

- **Acentos graves en plantillas en línea:** un ` dentro de un comentario HTML de una plantilla de
  componente corta la cadena y da errores absurdos («role no existe en Component»). Ha pasado tres veces.
- **Finales de línea:** `.gitattributes` fuerza LF en `.ts`, `.html` y `.xlf` porque la extracción
  de i18n calcula líneas. Si git avisa «CRLF will be replaced by LF» en cada commit, la copia de
  trabajo viene de antes: `git rm --cached -r . && git reset --hard`.
- **Rutas de Windows en Python:** usa `C:/...`, no `/c/...`.
- **Cuerpos JSON con acentos por heredoc en la shell:** llegan corruptos; usa un fichero o Python.
