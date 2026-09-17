# Herramientas del estándar de código

Las usa la skill `.claude/skills/estandar-de-codigo`. Ninguna forma parte de `CrmSaaS.sln`, así que
no se compilan en el CI ni entran en la aplicación.

| Herramienta | Para qué |
|---|---|
| `rename-symbols` | Renombra símbolos de C# con Roslyn: la declaración y todas sus referencias, sin tocar textos ni comentarios. |
| `contract-snapshot` | Lista los campos que la API puede sacar en el JSON. Comparando dos ramas se ven los contratos que cambian sin que el compilador avise. |
| `scripts/split-types.py` | Divide un fichero C# en un fichero por tipo, conservando los comentarios de cada uno. |
| `scripts/spanish-identifiers.py` | Dice qué identificadores siguen en español, midiendo la frecuencia real de cada palabra en los dos idiomas. |

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

# Qué queda en español
pip install wordfreq
python tools/scripts/spanish-identifiers.py src/Modules/Docs web/src/app/features/docs
```

## Requisitos

- `rename-symbols` necesita el **SDK 10** (los paquetes de Roslyn con el espacio de trabajo de
  MSBuild sólo publican para `net10.0`). El resto del repositorio sigue en .NET 9.
- Los scripts de Python necesitan `wordfreq` sólo en el caso de `spanish-identifiers.py`.
- En Windows, pasa las rutas como `C:/...` y no como `/c/...`.
