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

UMBRAL_POR_DEFECTO=60

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

# El umbral se lee del informe fusionado, no de cada suite por separado: lo que
# importa es cuánto del sistema queda cubierto entre todas, no cuánto cubre cada
# una. Un módulo puede estar al 0 % en unitarias y bien cubierto en integración.
"$PYTHON" - "$UMBRAL" <<'PY'
import sys, xml.etree.ElementTree as ET
umbral = float(sys.argv[1])
raiz = ET.parse('coverage/report/Cobertura.xml').getroot()
linea = float(raiz.get('line-rate')) * 100
rama  = float(raiz.get('branch-rate')) * 100
print(f"\nCobertura de líneas: {linea:.1f}%   (umbral: {umbral:.0f}%)")
print(f"Cobertura de ramas : {rama:.1f}%")
if linea < umbral:
    print(f"\n::error::La cobertura de líneas ({linea:.1f}%) está por debajo del umbral ({umbral:.0f}%).")
    sys.exit(1)
print("Cobertura por encima del umbral.")
PY
