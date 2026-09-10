# syntax=docker/dockerfile:1.7
#
# Imagen de la API. Se construye desde la raíz del repositorio:
#
#     docker build -f src/Host/ApiHost/Dockerfile -t crm-api .
#
# ══════════════════════════════════════════════════════════════════════ CONSTRUCCIÓN ═══
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .

# Restaurar y publicar en un solo paso, con la caché de NuGet montada.
#
# El montaje de caché es lo que hace que una reconstrucción no vuelva a descargar los
# paquetes. La alternativa clásica —copiar primero los `.csproj` para que la capa de
# restauración se reutilice— aquí no es viable: son sesenta y ocho proyectos y `COPY` no
# conserva la estructura de carpetas con comodines, así que habría que enumerarlos a mano y
# alguien se olvidaría de añadir el siguiente.
#
# Van juntos en un solo `RUN` porque el montaje sólo existe durante su instrucción: separar
# restauración y publicación dejaría a la segunda sin los paquetes que la primera bajó.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Host/ApiHost/ApiHost.csproj \
      -c Release \
      -o /app/publish \
      /p:UseAppHost=false

# ═══════════════════════════════════════════════════════════════════════ EJECUCIÓN ═════
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app
COPY --from=build /app/publish .

# Sin privilegios. `APP_UID` (1654) viene definido en la imagen base desde .NET 8; se usa esa
# en vez de crear un usuario propio para no divergir de lo que espera la plataforma.
# Plataformas como OpenShift rechazan de plano una imagen que declare root.
USER $APP_UID

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

# La comprobación va en la imagen, no sólo en el compose, para que la herede cualquier
# plataforma que la respete: quien despliegue esta imagen en otro sitio no va a heredar el
# `docker-compose.yml`.
#
# Se invoca la propia aplicación con `--health-check` en lugar de `curl`. La imagen de
# ASP.NET no trae `curl` ni `wget` —el healthcheck anterior lo llamaba igualmente y llevaba
# 1.590 fallos seguidos—, e instalarlo con apt añade una descarga de red a cada
# construcción, engorda la imagen y suma superficie de CVE para pedir una URL. El motivo
# está desarrollado en Program.cs, junto a la sonda.
HEALTHCHECK --interval=15s --timeout=10s --start-period=40s --retries=5 \
    CMD ["dotnet", "ApiHost.dll", "--health-check"]

ENTRYPOINT ["dotnet", "ApiHost.dll"]
