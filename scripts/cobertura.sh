#!/usr/bin/env bash
# Mide la cobertura del backend y falla si baja del umbral.
#
# Ejecuta las dos suites porque ninguna basta sola: las unitarias cubren el
# dominio, y las de integración son las únicas que cargan Infrastructure y los
# endpoints —y, de paso, las únicas que hacen visibles los módulos que
# tests/UnitTests no referencia—. Medir sólo las unitarias omitía diez módulos
# enteros del informe, y una métrica que omite lo no probado siempre miente a
# favor.
#
# Uso:  scripts/cobertura.sh [umbral]     (por defecto, el de UMBRAL_POR_DEFECTO)
set -euo pipefail

UMBRAL_POR_DEFECTO=77

# Un segundo umbral, sobre ramas, y no por afán de rigor: la cobertura de líneas
# está inflada por construcción. La capa Presentation pesa el 22 % de las líneas
# medidas y sale al 98,4 %, pero no porque las pruebas llamen a los endpoints
# —hay módulos enteros a los que ninguna prueba llama—: las líneas
# `group.MapGet(...)` se ejecutan al registrar las rutas, es decir, en cuanto la
# aplicación arranca. Descontando Presentation, la cobertura de líneas real es
# 60,4 % en vez de 68,8 %.
#
# El registro de rutas no tiene ramas, así que la cobertura de ramas no admite
# ese engaño. Es la cifra que de verdad dice si se probaron los dos lados de cada
# decisión, y por eso también tiene umbral.
#
# Los dos son un trinquete: se ponen un par de puntos por debajo de lo medido para que un
# cambio pequeño no ponga el build rojo sin motivo, y se suben conforme se gana terreno. Si no
# se suben nunca, dejan de proteger nada. Medido hoy: 70,8 % de líneas y 58,3 % de ramas
# (se partió de 68,8 y 55,3).
UMBRAL_RAMAS_POR_DEFECTO=69

# En los runners de CI el intérprete es `python3`; en Git Bash sobre Windows sólo
# existe `python`. Y no basta con que el nombre exista: Windows trae un alias
# `python3` de la Microsoft Store que no es un intérprete —imprime un anuncio
# para instalarlo y falla—, así que `command -v python3` lo encuentra y el script
# se rompe justo al final, después de haber pagado toda la suite. Se comprueba
# que cada candidato realmente ejecute algo.
PYTHON=""
for candidato in python3 python py; do
  if command -v "$candidato" >/dev/null 2>&1 && "$candidato" -c "" >/dev/null 2>&1; then
    PYTHON="$candidato"
    break
  fi
done
if [ -z "$PYTHON" ]; then
  echo "::error::No se encontró un Python utilizable (se probó python3, python y py)." >&2
  exit 1
fi
UMBRAL="${1:-${COBERTURA_UMBRAL:-$UMBRAL_POR_DEFECTO}}"
UMBRAL_RAMAS="${2:-${COBERTURA_UMBRAL_RAMAS:-$UMBRAL_RAMAS_POR_DEFECTO}}"
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$RAIZ"

echo "==> Limpiando resultados anteriores"
rm -rf coverage

echo "==> Compilando"
dotnet build CrmSaaS.sln -c Release --nologo -v quiet

# --no-build en los dos: ya se compiló arriba. Repetirlo triplicaría el tiempo.
echo "==> Pruebas unitarias"
dotnet test tests/UnitTests/UnitTests.csproj -c Release --no-build \
  --collect:"XPlat Code Coverage" --settings coverage.runsettings \
  --results-directory ./coverage/unit

echo "==> Pruebas de integración (levantan MySQL con Testcontainers)"
dotnet test tests/IntegrationTests/IntegrationTests.csproj -c Release --no-build \
  --collect:"XPlat Code Coverage" --settings coverage.runsettings \
  --results-directory ./coverage/integration

echo "==> Fusionando informes"
dotnet tool restore
dotnet reportgenerator \
  -reports:"coverage/**/coverage.cobertura.xml" \
  -targetdir:"coverage/report" \
  -reporttypes:"TextSummary;Html;Cobertura;MarkdownSummaryGithub"

cat coverage/report/Summary.txt

# Los umbrales se leen del informe fusionado, no de cada suite por separado: lo
# que importa es cuánto del sistema queda cubierto entre todas, no cuánto cubre
# cada una. Un módulo puede estar al 0 % en unitarias y bien cubierto en
# integración.
"$PYTHON" - "$UMBRAL" "$UMBRAL_RAMAS" <<'PY'
import sys, xml.etree.ElementTree as ET

umbral_linea = float(sys.argv[1])
umbral_rama  = float(sys.argv[2])
raiz = ET.parse('coverage/report/Cobertura.xml').getroot()
linea = float(raiz.get('line-rate')) * 100
rama  = float(raiz.get('branch-rate')) * 100

# Cuánto de la cobertura de líneas viene de la capa Presentation, que se ejecuta
# al arrancar. Se informa siempre: sin este dato, la cifra global se lee como si
# midiera pruebas cuando en parte mide un arranque de la aplicación.
cub = tot = cub_p = tot_p = 0
for paquete in raiz.iter('package'):
    es_presentacion = paquete.get('name', '').endswith('.Presentation')
    for clase in paquete.iter('class'):
        for l in clase.iter('line'):
            golpeada = int(l.get('hits')) > 0
            tot += 1
            cub += golpeada
            if es_presentacion:
                tot_p += 1
                cub_p += golpeada
sin_p = (cub - cub_p) / (tot - tot_p) * 100 if tot > tot_p else 0.0

print()
print(f"Cobertura de líneas ................. {linea:5.1f} %   (umbral: {umbral_linea:.0f} %)")
print(f"  descontando Presentation .......... {sin_p:5.1f} %   (informativo)")
print(f"Cobertura de ramas .................. {rama:5.1f} %   (umbral: {umbral_rama:.0f} %)")

fallos = []
if linea < umbral_linea:
    fallos.append(f"la cobertura de líneas ({linea:.1f} %) está por debajo del umbral ({umbral_linea:.0f} %)")
if rama < umbral_rama:
    fallos.append(f"la cobertura de ramas ({rama:.1f} %) está por debajo del umbral ({umbral_rama:.0f} %)")

if fallos:
    print()
    for f in fallos:
        print("::error::" + f[0].upper() + f[1:] + ".")
    sys.exit(1)

print("Ambos umbrales cumplidos.")
PY
