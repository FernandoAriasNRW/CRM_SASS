# Despliegue

Qué produce el CI, cómo se despliega en cada tipo de plataforma y qué hay que saber antes.

## Lo que produce el CI

Cada push a `main` o `develop`, y cada etiqueta `v*`, publica dos imágenes en el registro de
contenedores de GitHub:

```
ghcr.io/<owner>/<repo>/api
ghcr.io/<owner>/<repo>/web
```

Son **multiarquitectura** (`linux/amd64` y `linux/arm64`), llevan SBOM y atestación de
procedencia, y se etiquetan así:

| Etiqueta | Cuándo | Para qué |
|---|---|---|
| `sha-<commit completo>` | siempre | **la que debe usar producción** |
| `main`, `develop` | según la rama | lo último de esa rama |
| `v1.2.3`, `1.2`, `1` | al empujar una etiqueta `v*` | versiones publicadas |
| `latest` | sólo en la rama por defecto | conveniencia local |

**Desplegar por `latest` o por nombre de rama es la forma más rápida de no saber qué hay en
producción.** Dos servidores que arrancaron con un día de diferencia acabarían ejecutando
binarios distintos con el mismo nombre. Usa `sha-…`, o mejor la digestión.

Para obtener la digestión exacta de lo que vas a desplegar:

```bash
docker buildx imagetools inspect ghcr.io/<owner>/<repo>/api:sha-<commit> --format '{{.Manifest.Digest}}'
```

## Antes de desplegar, cuatro cosas

**1. Las migraciones se aplican solas al arrancar.** `Program.cs` ejecuta `Database.Migrate()`
para todos los contextos antes de servir nada, y **si falla no arranca**: un esquema a medias
es peor que un servicio caído, porque atiende peticiones y corrompe datos.

La consecuencia práctica: con varias réplicas, todas intentan migrar a la vez. MySQL serializa
por bloqueo de tabla y las demás se encuentran el trabajo hecho, pero **la primera vez que una
migración sea larga esto se convierte en una carrera**. Cuando llegue ese momento, hay que
sacar la migración a un paso previo del despliegue y arrancar la aplicación después.

**2. Los secretos son obligatorios y no tienen valor por defecto.** `JWT_KEY`, `MYSQL_PASSWORD`
y `RABBITMQ_PASSWORD` hacen que el arranque falle si faltan (`${VAR:?}`). Es deliberado: un
valor por defecto para una clave de firma es una puerta abierta con aspecto de configuración.

**3. La aplicación escucha en el 8080 y el frontend también**, ambos sin privilegios de root.
Ninguna de las dos imágenes puede abrir un puerto por debajo de 1024, y ninguna necesita
hacerlo: eso es cosa del balanceador.

**4. Hay dos comprobaciones de salud distintas y no son intercambiables.**

| Ruta | Responde si | Úsala para |
|---|---|---|
| `/health/live` | el proceso responde | *liveness*: reiniciar si está colgado |
| `/health/ready` | además, la base de datos contesta | *readiness*: enrutar tráfico |

Usar `/health/ready` como *liveness* reinicia la aplicación cada vez que la base de datos
tiene un problema, lo cual no arregla la base y sí tira las conexiones que aún funcionaban.

## Docker Compose

La forma más corta de tener esto en pie en un servidor.

```bash
cp .env.example .env      # y rellenarlo de verdad
export IMAGE_TAG=sha-<commit>
docker compose -f docker-compose.prod.yml up -d
```

`docker-compose.prod.yml` no publica los puertos de MySQL ni de RabbitMQ, no compila nada y
pone límites de memoria. El `docker-compose.yml` de la raíz es para desarrollar: compila desde
el código y abre esos puertos a propósito.

## Kubernetes

Kubernetes **ignora el `HEALTHCHECK` de la imagen** y usa sus propias sondas. Hay que
declararlas, y son las dos:

```yaml
containers:
  - name: api
    image: ghcr.io/<owner>/<repo>/api@sha256:<digestión>
    ports:
      - containerPort: 8080
    livenessProbe:
      httpGet: { path: /health/live, port: 8080 }
      initialDelaySeconds: 40      # las migraciones corren antes de servir
      periodSeconds: 15
    readinessProbe:
      httpGet: { path: /health/ready, port: 8080 }
      periodSeconds: 10
    securityContext:
      runAsNonRoot: true
      runAsUser: 1654              # el usuario `app` de las imágenes de .NET
      allowPrivilegeEscalation: false
      readOnlyRootFilesystem: true
      capabilities: { drop: ["ALL"] }
    resources:
      requests: { memory: 256Mi, cpu: 100m }
      limits:   { memory: 1Gi }
```

`initialDelaySeconds` importa: la aplicación aplica migraciones antes de escuchar, y una sonda
impaciente la mata a mitad y la deja reiniciándose sin fin. Si tus migraciones tardan más,
súbelo — o mejor, usa `startupProbe`, que existe justo para esto.

Para el frontend, `runAsUser: 101` y las sondas contra `/es/`.

## OpenShift

Funciona sin ajustes. Fue parte del motivo para pasar el frontend a
`nginx-unprivileged` y para respetar el usuario `app` en la API: OpenShift rechaza de plano
las imágenes que corren como root, y asigna un UID arbitrario. Ninguna de las dos imágenes
escribe fuera de `/tmp`.

## Cloud Run, App Runner, Container Apps

Todas esperan un contenedor que escuche en un puerto configurable y no corra como root.

- **Cloud Run** inyecta `PORT`. Pasa `ASPNETCORE_HTTP_PORTS=$PORT` a la API. Para el frontend
  hay que cambiar el `listen` de `nginx.conf`, porque nginx no lee variables de entorno.
- **App Runner** y **Container Apps** funcionan con el 8080 tal cual.
- En las tres, el mínimo de instancias no puede ser 0 para la API si te importa la primera
  petición: el arranque incluye las migraciones.

## Lo que este pipeline NO hace

**No despliega.** Publica imágenes; alguien tiene que llevarlas a algún sitio.

Aquí había dos trabajos —«Desplegar a staging» y «Desplegar a producción»— cuyo contenido real
era `echo "Pendiente: integrar con el proveedor cloud"`. Salían en verde. Un despliegue que
siempre tiene éxito porque no hace nada es peor que no tenerlo: el panel decía que producción
estaba actualizada y no era cierto. Se quitaron.

Cuando haya un destino decidido, el sitio donde añadirlo está marcado al final de
`.github/workflows/ci.yml`. Lo que ese trabajo debe hacer:

- usar `environment:` para que GitHub pida aprobación y guarde los secretos del entorno;
- desplegar **por digestión**, no por etiqueta;
- esperar a que `/health/ready` responda antes de dar el despliegue por bueno;
- saber volver atrás — con la digestión anterior, que es un dato que hay que guardar.

## Registro de imágenes distinto de GHCR

Cambia `images:` en el trabajo `imagenes` y el paso de autenticación. El resto —etiquetas,
multiarquitectura, caché, SBOM— es igual en cualquier registro que hable OCI.
