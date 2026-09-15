# El editor de Documentos: qué falta y en qué orden

Investigación pedida el 10 de septiembre de 2026: «el editor está muy simple y es complicado
organizarlo; Notion y ClickUp son ejemplos de editores potentes».

Todo lo que se afirma aquí está medido sobre el código de la rama, y lo que dice «comprobado» se
verificó contra la API levantada. No hay estimaciones sacadas de la impresión general.

---

## 1. Lo primero: no es que el editor sea simple, es que la mitad no está conectada

Antes de hablar de funciones nuevas hay que decir lo que hay. El editor **ya carga** trece
extensiones de TipTap: tablas redimensionables, listas de tareas anidadas, imágenes, YouTube,
enlaces, menciones, adjuntos y el menú `/`. En papel no está tan lejos de lo que se pide.

El problema es que buena parte no tiene por dónde usarse. Es el mismo patrón que este proyecto
lleva persiguiendo desde la Fase 4: **código que existe y no hace nada**.

### 1.1 Guardar puede fallar y nadie se entera — el más grave

`docs.component.ts:313` guarda el contenido con `debounceTime(1000)` y `.subscribe()` **sin
manejador de error**. Si la petición falla —red, sesión caducada, error del servidor— no ocurre
absolutamente nada: no hay aviso, no hay reintento, no se marca el documento como sucio.

Y arriba, en la cabecera, hay un texto fijo que dice **«Saved just now»**. No está atado a nada:
es una cadena escrita a mano en `docs.component.html:41`, sin marca de traducción, que se pinta
siempre. Diga lo que diga el servidor.

O sea: alguien puede escribir media hora con la sesión caducada, leyendo «guardado» todo el rato,
y perderlo entero al recargar. **Esto se arregla antes que cualquier función nueva.**

### 1.2 No se pueden crear páginas — por eso «es complicado organizarlo»

El dominio tiene `Page.ParentPageId` y `Page.Order`. La API tiene `POST /docs/{id}/pages`. El
componente tiene `crearPagina()`. Y **la plantilla no lo llama desde ningún sitio**: `createPage`
tiene cero apariciones en `docs.component.html`.

Cada documento se queda para siempre con la única página que le creó la plantilla. No hay árbol de
páginas, no hay subpáginas, no hay forma de reordenar. Toda la jerarquía existe en la base de datos
y es inalcanzable.

Ésa es la respuesta literal a «es complicado organizarlo»: **no se puede organizar porque no hay
con qué**.

### 1.3 El título del documento edita el título de la página

El campo grande de arriba muestra `activeDocument()?.title` y al escribir llama a
`updateDocumentTitle()`, que escribe en `activePage()`. Dos campos distintos —el del documento y el
de la miga de pan— comparten el mismo manejador, y en uno de los dos está mal.

Peor: **no existe endpoint para renombrar un documento**. Los únicos verbos de escritura del módulo
son `DELETE /docs/{id}`, `DELETE /docs/pages/{id}` y `PUT /docs/pages/{id}`. `Document.Update(title,
description)` está escrito en el dominio y no lo llama nadie. Así que el campo de título del
documento es un campo que no puede funcionar de ninguna manera.

### 1.4 Exportar no exporta

- **Export HTML** hace `window.open('/api/v1/docs/{id}/export')`. Una pestaña nueva no lleva la
  cabecera `Authorization`, y el endpoint la exige. **Comprobado contra la API: 401.**
- **Export PDF** busca `window.html2pdf`. `html2pdf.js` está en `package.json` y **no se importa en
  ningún sitio**, así que siempre cae al `window.print()` de reserva, que imprime la aplicación
  entera con su barra lateral, no el documento.

Dos botones en la cabecera del editor, ninguno de los dos hace lo que promete.

### 1.5 El menú `/` no se puede usar con el teclado

`suggestion.ts:196` — el `onKeyDown` sólo atiende `Escape`. Las flechas y el Enter no hacen nada, y
no hay ningún elemento resaltado. Escribes `/`, aparece el menú, **y tienes que soltar el teclado e
ir al ratón**.

En Notion el menú `/` es el editor entero: todo se hace sin quitar las manos del teclado. Aquí, la
única función que se le parece obliga a hacer justo lo contrario.

Además:

- **Los doce comandos están en inglés** («Heading 1», «Bullet List», «Task List»…) y sin marca de
  traducción, dentro de una aplicación en español.
- El filtro es `title.toLowerCase().startsWith(query)`. `/lista` no encuentra nada. `/list` tampoco
  encuentra «Bullet List». `/h1` tampoco. Sólo funciona si aciertas la primera palabra en inglés.
- Los estilos van escritos a mano en JavaScript: `background = 'white'`, `color = '#18181b'`. **El
  menú `/` no respeta el tema oscuro** que se añadió la semana pasada.
- Sin iconos, sin descripciones, sin agrupar, y el bloque que pinta cada elemento está duplicado
  entre `onStart` y `onUpdate`.
- Imagen y vídeo se piden con `window.prompt()`.

### 1.6 El nodo de adjuntos no se puede insertar

`FileAttachment` está registrado en el editor (`docs.component.ts:237`) y **no hay ni un comando,
ni un botón, ni un manejador de pegado o arrastre que lo cree**. Sólo aparecería si el HTML ya lo
trajera de fuera.

Y en el servidor, `POST /docs/upload` con su `UploadFileHandler` y su `IStorageService` funcionan
—y **no tienen ni un solo llamante en el frontend**: `docs.service.ts` no tiene método de subida—.
La cadena de subir un fichero está construida entera por los dos extremos y sin unir por el medio.

### 1.7 La barra flotante tiene cinco botones

Negrita, cursiva, tachado, H1, H2. Nada más. **No hay botón de enlace**, aunque la extensión `Link`
está cargada. No hay código en línea, ni resaltado, ni color, ni «convertir en», ni limpiar
formato.

Cinco botones es, textualmente, lo que hace que el editor «esté muy simple».

---

## 2. Lo que hacen Notion y ClickUp que aquí no está

Dejando aparte lo roto, el hueco de producto se agrupa en cuatro cosas. Las ordeno por cuánto
cambian el día a día, no por dificultad.

### 2.1 Manipular bloques sin escribir

Lo que define esos editores no es la lista de formatos: es que **cada bloque es un objeto que
puedes agarrar**. Al pasar el ratón por la izquierda aparecen dos controles: un `+` que inserta
debajo y un asa `⠿` que arrastra. El asa también abre un menú con duplicar, borrar, convertir en
otro tipo, color y comentar.

Aquí no hay nada de eso. Reordenar dos párrafos es cortar y pegar. Está anotado como pendiente en
`AUDITORIA.md` §14 desde la 5B.

### 2.2 Bloques que estructuran, no sólo que dan formato

Los que faltan y se usan de verdad:

- **Desplegables** (`toggle`): la forma de tener un documento largo que no abruma. Es
  `<details>`/`<summary>` con estilo.
- **Avisos** (`callout`): el recuadro con icono para «ojo con esto». Se usa en cualquier
  procedimiento.
- **Columnas**: dos o tres al lado. Sin esto, todo documento es una tira vertical.
- **Índice como bloque**: distinto del panel lateral que ya existe, porque viaja con el documento
  al exportarlo o compartirlo.
- **Bloques de código con lenguaje y coloreado.** En un producto para equipos de desarrollo, pegar
  código en gris plano es una carencia visible.

### 2.3 Meter contenido sin pensar

- **Arrastrar una imagen al editor.** Y pegarla desde el portapapeles.
- **Pegar Markdown** y que se convierta.
- **Pegar un enlace** y poder elegir entre enlace, marcador o incrustado.
- Atajos de Markdown al escribir: `#`, `-`, `>`, ` ``` `. TipTap ya trae varios en `StarterKit`,
  pero nadie los ha documentado en la interfaz, así que nadie sabe que están.

### 2.4 Escribir con otros

Comentarios en línea sobre un fragmento —anotado como pendiente de la 5B— y edición simultánea con
cursores. Lo segundo es una liga aparte: exige un servidor de sincronización y cambiar cómo se
guarda. Lo trato en el apartado 4.

---

## 3. Lo que hay en Documentos y en esos editores no

Merece decirse, porque marca por dónde no hay que ir copiando:

- **El índice lateral** (`esquema-del-documento.component.ts`) se calcula del documento y no se
  guarda, así que no puede desincronizarse. Está mejor resuelto que muchos.
- **Las menciones con vuelta**: escribir `#tarea` en un documento y que la tarea enseñe «mencionado
  en». Es el diferencial declarado del producto y ClickUp lo hace a medias. **Eso no se toca.**

---

## 4. Propuesta, por fases

Cada fase deja el editor utilizable por su cuenta. El orden no es por dificultad: es por cuánto
duele lo que arregla.

### Fase A — Que no mienta (lo primero, sin discusión)

| Qué | Dónde |
|---|---|
| Manejar el error al guardar y enseñar el estado real | `docs.component.ts:313` |
| Sustituir «Saved just now» por «Guardando… / Guardado a las HH:MM / No se pudo guardar» | `docs.component.html:41` |
| Botones de crear página y subpágina, con árbol navegable | plantilla + `crearPagina()` |
| `PUT /docs/{id}` para renombrar, y separar los dos campos de título | Docs.Presentation + `Document.Update` |
| Export HTML con la cabecera de sesión (descarga por `blob`, no `window.open`) | `docs.component.ts:556` |
| Importar `html2pdf` de verdad, o quitar el botón y la dependencia | `docs.component.ts:544` |

Nada de esto es una función nueva. Es hacer que lo que ya se ofrece cumpla.

### Fase B — El menú `/` a la altura

Reescribir `suggestion.ts` con: navegación por flechas y Enter, elemento resaltado, filtro por
subcadena y por alias (`/lista` y `/list` llegan a «Lista con viñetas»), comandos **en español y
marcados para traducir**, iconos, descripción corta, agrupación por categoría, y estilos con las
variables del tema en vez de colores escritos a mano.

Y quitar los dos `window.prompt()`.

Es el cambio con mejor relación entre esfuerzo y percepción: un fichero, y es la puerta de entrada
a todo lo demás.

### Fase C — Bloques que se agarran y bloques que estructuran

Todas estas extensiones están publicadas en el registro público y son **MIT** (comprobado el 10 de
septiembre de 2026, versión 3.31.3, compatible con el `^3.30.0` del proyecto):

| Extensión | Para qué |
|---|---|
| `@tiptap/extension-drag-handle` | el asa `⠿` y el `+` al pasar el ratón |
| `@tiptap/extension-details` | bloques desplegables |
| `@tiptap/extension-code-block-lowlight` | código con lenguaje y coloreado |
| `@tiptap/extension-highlight` + `-color` | resaltado y color de texto |
| `@tiptap/extension-typography` | comillas, guiones y flechas al escribir |
| `@tiptap/extension-character-count` | contador de palabras |
| `@tiptap/extension-table-of-contents` | índice como bloque |

El bloque de aviso (*callout*) y el de columnas hay que escribirlos: no hay extensión oficial. El
de aviso es un nodo sencillo; el de columnas es el más caro de toda la propuesta y yo lo dejaría
para el final o lo descartaría.

Y ampliar la barra flotante: enlace —que ya está cargado y no tiene botón—, código en línea,
resaltado y «convertir en».

### Fase D — Ficheros, de una vez

`@tiptap/extension-file-handler` (MIT) da el arrastrar y el pegar. Falta unirlo con
`POST /docs/upload`, que ya existe y funciona, y darle a `FileAttachment` un comando para
insertarse. Es unir dos extremos ya construidos.

### Fase E — Comentarios en línea, y la decisión de la edición simultánea

Los comentarios en línea son una marca sobre un rango con un identificador, más una tabla. Encaja
con lo que ya hay: el módulo `Comments` existe.

**La edición simultánea es otra cosa y conviene decidirla explícitamente.** Exige Y.js, un servidor
de sincronización aparte, y guardar el documento como estado CRDT en vez de como HTML —o sea, tocar
cómo persiste todo el módulo—. Es un proyecto en sí mismo, no una extensión más. Mi recomendación:
**no ahora**. Con el guardado automático arreglado y el aviso de «alguien más está editando», se
cubre el 90 % del dolor real por una fracción del coste.

---

## 5. Lo que no recomiendo

- **Bases de datos incrustadas al estilo Notion.** El producto ya tiene tareas, tickets y proyectos
  con sus vistas. Duplicar eso dentro del documento es competir consigo mismo. Lo que sí encaja es
  incrustar una **vista existente** por referencia.
- **Copiar el bloque de columnas antes que el resto.** Es el más caro y el que menos se usa.
- **Cambiar de editor.** TipTap da para todo lo de arriba. El problema nunca fue la librería.

---

## 6. Si sólo se hiciera una cosa

La Fase A. El editor no parece simple: parece que funciona y a veces no guarda. Eso es más grave
que no tener bloques desplegables.

---

## 7. Qué se hizo, y en qué me equivoqué al auditar

Las cinco fases implementadas.

### Una corrección al apartado 1.6

Escribí que `POST /docs/upload` «funciona» y sólo le faltaba quien lo llamara. **Era falso.** El
único almacenamiento registrado subía a Cloudinary, y Cloudinary no estaba configurado en ninguna
parte —ni en desarrollo, ni en producción, ni en el compose—: la respuesta era un 400 con
«Cloud name must be specified in Account!». Nunca había llegado a subir nada.

Lo di por bueno porque leí el código y compilaba. Es exactamente el error que esta auditoría le
reprocha al resto del módulo: dar por funcionando lo que nadie ha ejecutado. Se vio al llamarlo.

Al conectarlo aparecieron dos fallos más que nadie podía haber encontrado antes, porque nadie había
podido llegar a ejecutar ese camino:

- **Todo se subía como imagen.** Cloudinary rechaza un PDF o un CSV por el endpoint de imágenes.
- **Un fallo del almacenamiento devolvía un 500 con la traza dentro**, porque la excepción subía
  hasta el manejador global.

Y uno que introduje yo al arreglarlo: crear la carpeta del almacén con una ruta relativa a `/app`
**tiraba el arranque entero de la API**, porque la imagen corre con un usuario sin privilegios. Que
no se puedan subir ficheros es un problema; que no arranque el servidor es otro mucho mayor. La
carpeta se crea ahora en la imagen con su dueño puesto, y si aun así falla se registra el error y
el servidor arranca igual.

### Lo que quedó de cada fase

- **A.** Estado real del guardado con reintento; árbol de páginas con crear, anidar, mover y
  borrar; endpoint para renombrar documentos; los dos botones de exportar funcionando.
- **B.** Menú `/` con teclado, filtro por subcadena y alias, en español, agrupado y con iconos;
  fuera los dos `window.prompt`; barra flotante con enlace, código, cita y limpiar formato.
- **C.** Asa para arrastrar bloques, desplegables, avisos en cuatro tonos, código con lenguaje
  elegible y coloreado, tipografía automática, resaltado y contador de palabras.
- **D.** Subida de ficheros por arrastre, pegado y selector, con almacenamiento en disco como
  reserva cuando no hay credenciales.
- **E.** Comentarios en línea: una marca sobre el texto, un panel al lado con la cita y el hilo, y
  resolver sin borrar.

### Cómo quedó repartida la Fase E

El trabajo se parte en dos a propósito. **Docs guarda dónde está pegado** el comentario —qué
página, qué texto se citó, si está resuelto— y **Comments guarda la conversación**, con el
identificador de la anotación como entidad comentada.

Comentar ya existía para tareas, tickets y proyectos, con sus reglas de quién edita y quién borra,
y ese módulo dice en su propio código por qué es uno solo para los tres: «triplicarla daría tres
sitios donde arreglar el mismo fallo». Un cuarto para los documentos habría sido el mismo error.
El panel reutiliza el componente del hilo tal cual; lo único nuevo es el anclaje.

**El texto citado se copia** en vez de leerse del documento. Si alguien reescribe el párrafo, el
panel puede seguir diciendo sobre qué se comentó en vez de señalar otra cosa sin avisar.

### Lo que sigue pendiente

- La **edición simultánea**, que sigue sin recomendarse: exige Y.js, un servidor de sincronización
  y cambiar cómo persiste el módulo entero.
- **El contenido de las plantillas predefinidas sigue en inglés** dentro del handler. Son
  documentos enteros, y es un arreglo aparte.
- **El bloque de columnas**, que ya dejé para el final por ser el más caro y el que menos se usa.
