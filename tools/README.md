# Herramientas del estándar de código

Las usa la skill `.claude/skills/estandar-de-codigo`. Ninguna forma parte de `CrmSaaS.sln`, así que
no se compilan en el CI ni entran en la aplicación.

| Herramienta | Para qué |
|---|---|
| `rename-symbols` | Renombra símbolos de C# con Roslyn: la declaración y todas sus referencias, sin tocar textos ni comentarios. |
| `contract-snapshot` | Lista los campos que la API puede sacar en el JSON. Comparando dos ramas se ven los contratos que cambian sin que el compilador avise. |
| `scripts/split-types.py` | Divide un fichero C# en un fichero por tipo, conservando los comentarios de cada uno. |
| `scripts/spanish-identifiers.py` | Dice qué identificadores siguen en español, midiendo la frecuencia real de cada palabra en los dos idiomas. |
| `scripts/rename-frontend.py` | Renombra identificadores en TypeScript y plantillas de Angular sin tocar comentarios, cadenas ni textos de la interfaz. No entiende tipos: ver sus límites abajo. |

## Uso

```bash
# Renombrar (el mapa es: fichero <tab> nombre viejo <tab> nombre nuevo)
printf 'src/Modules/X/X.Domain/Entities/Cosa.cs\tCosa\tThing\n' > /tmp/mapa.tsv
dotnet run --project tools/rename-symbols -- CrmSaaS.sln /tmp/mapa.tsv --dry-run
dotnet run --project tools/rename-symbols -- CrmSaaS.sln /tmp/mapa.tsv

# Comparar contratos JSON entre main y la rama
git worktree add -q /tmp/main-wt origin/main
dotnet build /tmp/main-wt/src/Host/ApiHost/ApiHost.csproj -o /tmp/bin-main
dotnet build src/Host/ApiHost/ApiHost.csproj -o /tmp/bin-rama
dotnet run --project tools/contract-snapshot -- /tmp/bin-main  > /tmp/main.txt
dotnet run --project tools/contract-snapshot -- /tmp/bin-rama  > /tmp/rama.txt
diff /tmp/main.txt /tmp/rama.txt

# Un tipo por fichero
python tools/scripts/split-types.py src/Modules/X/X.Application/XCqrs.cs \
    src/Modules/X/X.Application/Things --namespace X.Application.Things

# Renombrar en el frontend (el mapa es: nombre viejo <tab> nombre nuevo, para todo lo que se le pase)
python tools/scripts/rename-frontend.py C:/tmp/mapa.tsv web/src/app/features/tasks --dry-run

# Qué queda en español
pip install wordfreq
python tools/scripts/spanish-identifiers.py src/Modules/Docs web/src/app/features/docs
```

## Requisitos

- `rename-symbols` necesita el **SDK 10** (los paquetes de Roslyn con el espacio de trabajo de
  MSBuild sólo publican para `net10.0`). El resto del repositorio sigue en .NET 9.
- Los scripts de Python necesitan `wordfreq` sólo en el caso de `spanish-identifiers.py`.
- En Windows, pasa las rutas como `C:/...` y no como `/c/...`.

## Límites de `rename-frontend.py`

No es Roslyn: trabaja por tokens, así que **renombra un nombre en todos los sitios del fichero**,
sea del tipo que sea. Si un campo de un tipo compartido (`VistaIntegrada.clave`,
`CellEdit.valor`, `OpcionesDeLlamada.sinAviso`) se llama igual que una variable local del mapa, lo
cambia también y el build falla. Por eso, después: `npx ng build`, restaurar a mano lo que sea de
otro bloque, y comparar los atributos y las cadenas de las plantillas con `main`.

Tampoco renombra los **nombres** de los atributos (`[tareas]="..."`, `app-carga`): sólo los
valores. Los inputs, outputs y selectores de un componente renombrado se cambian a mano en quien
lo usa, y en las pruebas e2e que lo busquen.
