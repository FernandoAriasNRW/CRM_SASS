# syntax=docker/dockerfile:1.7
#
# Imagen del frontend. Se construye desde `web/`:
#
#     docker build -t crm-web ./web
#
# ══════════════════════════════════════════════════════════════════════ CONSTRUCCIÓN ═══
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./

# `npm ci` y no `npm install --legacy-peer-deps`.
#
# Lo que había instalaba resolviendo el árbol de nuevo, así que la imagen podía llevar
# versiones distintas de las que probó el CI —el lockfile quedaba de adorno—, y
# `--legacy-peer-deps` además escondía los conflictos de dependencias entre pares en lugar
# de mostrarlos. Una imagen que no se puede reproducir a partir del repositorio no sirve
# para diagnosticar nada cuando falla en producción.
#
# `npm ci` exige que el lockfile esté al día y borra node_modules antes de instalar. Si
# falla aquí, el lockfile está desincronizado y eso es justo lo que hay que arreglar.
RUN --mount=type=cache,target=/root/.npm \
    npm ci

COPY . .

# Compila los dos idiomas a la vez: dist/web/browser/{es,en}.
RUN npm run build

# Que estén los dos. Sin esta comprobación, un fallo de localización produce una imagen que
# arranca, sirve el idioma que sí salió y devuelve 404 en el otro — y eso se descubre en
# producción, contado por un usuario.
RUN for idioma in es en; do \
      test -f "dist/web/browser/$idioma/index.html" \
        || { echo "ERROR: falta el bundle del idioma $idioma"; exit 1; }; \
    done

# ═══════════════════════════════════════════════════════════════════════ EJECUCIÓN ═════
#
# `nginx-unprivileged` en vez de `nginx`: la imagen oficial arranca su proceso maestro como
# root, y hay plataformas —OpenShift, y cualquier clúster con una política que lo exija— que
# sencillamente no ejecutan una imagen así. Escucha en el 8080 porque un proceso sin
# privilegios no puede abrir el puerto 80.
FROM nginxinc/nginx-unprivileged:1.27-alpine

USER root
RUN rm -rf /usr/share/nginx/html/*
USER nginx

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/web/browser /usr/share/nginx/html

EXPOSE 8080

# `wget` viene en la imagen de Alpine; no hace falta instalar nada.
#
# Se pide `/es/` y no `/`: la raíz responde con un 302 al idioma preferido, y un 302 es
# «vivo» para casi cualquier comprobación aunque los ficheros del idioma no existieran. Al
# pedir una página real se comprueba que hay algo servible detrás.
#
# `127.0.0.1` y no `localhost`, que es lo que se escribe por costumbre y aquí no funciona:
# nginx escucha en `0.0.0.0:8080` —sólo IPv4— y dentro del contenedor `localhost` resuelve
# primero a `::1`, así que la comprobación moría con «Connection refused» mientras el sitio
# se servía perfectamente desde fuera. Un contenedor eternamente «unhealthy» que en realidad
# funciona: el mismo enredo que ya costó 1.590 fallos en la API, por otro motivo.
HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://127.0.0.1:8080/es/ || exit 1
