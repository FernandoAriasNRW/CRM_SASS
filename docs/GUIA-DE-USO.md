# Guía de uso

Cómo se usa CRM SaaS Suite. Está escrita para quien va a trabajar con la herramienta, no para
quien la programa.

**Una advertencia por delante:** aquí sólo se cuenta lo que funciona. Lo que está a medias tiene
su apartado al final, [Lo que todavía no hace](#lo-que-todavía-no-hace), y se dice sin rodeos.
Una guía que describe botones que no responden hace perder más tiempo que no tener guía.

---

## Índice

1. [Entrar y moverse](#1-entrar-y-moverse)
2. [Tareas](#2-tareas)
3. [Proyectos](#3-proyectos)
4. [Tickets: el buzón de soporte](#4-tickets-el-buzón-de-soporte)
5. [Campos personalizados](#5-campos-personalizados)
6. [Automatizaciones](#6-automatizaciones)
7. [Documentos, chat, calendario y equipos](#7-documentos-chat-calendario-y-equipos)
8. [Panel e informes](#8-panel-e-informes)
9. [Avisos y preferencias](#9-avisos-y-preferencias)
10. [Administración](#10-administración)
11. [Lo que todavía no hace](#lo-que-todavía-no-hace)

---

## 1. Entrar y moverse

Se entra con correo y contraseña. La sesión se renueva sola mientras trabajas; si cierras el
navegador, la próxima vez habrá que volver a entrar.

**Hay dos papeles.** *Administrador* configura la organización —usuarios, campos,
automatizaciones, integraciones— y *miembro* trabaja. Lo que un miembro no puede hacer no le
aparece en el menú.

El menú de la izquierda se personaliza: puedes **anclar hasta seis secciones** arriba y dejar el
resto abajo, arrastrando dentro del personalizador. Lo que ordenes **se guarda en tu cuenta**, no
en el navegador, así que lo encontrarás igual desde otro ordenador.

En cualquier pantalla, el **buscador de comandos** (`Ctrl` + `K`) va directo a lo que escribas
sin pasar por el menú.

### El panel de vistas

En **Tareas**, **Proyectos** y **Tickets** hay un panel lateral con las vistas de esa sección.
Viene fijado; el alfiler de su cabecera lo suelta, y entonces se esconde y vuelve a salir al
acercar el ratón al borde izquierdo.

| Vista | Qué enseña |
|---|---|
| Ver todo | La lista completa |
| Asignado a mí | Lo que respondes tú. En un ticket, los que llevas como agente |
| Creado por mí | Lo que abriste tú, aunque ahora responda otro |
| Favoritos | Lo que marcaste con la estrella |
| Compartido conmigo | Lo que alguien te dio a ti en concreto |
| Privado | Lo tuyo que no has compartido con nadie |
| Archivado | Lo que apartaste de la vista sin borrarlo |
| Papelera | Lo borrado, que se puede recuperar |

**La vista queda en la dirección**, así que puedes copiar el enlace de una lista filtrada y
mandárselo a alguien, y el botón de atrás del navegador funciona como esperas.

**Lo que hay en este panel filtra de verdad.** Si una vista te devuelve una lista vacía, es que
no hay nada; no es que se haya quedado a medias.

**Archivado y papelera no son lo mismo.** Archivar es «esto ya no me estorba»: sale de las
listas y se queda ahí. La papelera es «esto lo he borrado», y lleva implícito que algún día se
vacía. Algo que estaba archivado y se borra, al restaurarlo **sigue archivado**, que es donde su
dueño lo había dejado.

### La aplicación está en dos idiomas

Español e inglés. El idioma se elige en el navegador y la dirección lo refleja: `/es/…` o
`/en/…`. Si entras a la raíz, te lleva al que tengas configurado.

---

## 2. Tareas

Es el centro de la herramienta. La misma lista de tareas se mira de **cuatro maneras**, y el
selector de arriba cambia entre ellas sin recargar.

### Tablero

Columnas por estado —*Por hacer*, *En progreso*, *En revisión*, *Completado*, *En espera*— y
tarjetas que se arrastran de una a otra.

Cada columna carga por tandas. **El número que ves junto al título de la columna es el total, no
lo que hay pintado**: si pone 47 y ves 20 tarjetas, las otras 27 están ahí, se cargan al pulsar
«mostrar más».

### Lista

Una tabla. Su gracia es que **se edita en la propia celda**: pulsa sobre un valor, escribe y sal
del campo. `Esc` cancela sin guardar.

Si el servidor rechaza el cambio, la celda **vuelve sola a lo que había** y sale un aviso con el
motivo. No se queda enseñando algo que no se guardó.

### Gantt

Las tareas en una línea de tiempo, con sus dependencias dibujadas como flechas.

Una tarea con fecha de inicio se dibuja como una barra; **una tarea sin fecha de inicio se dibuja
como un hito** en su vencimiento. No es un fallo: es que sólo se sabe cuándo termina, y pintar
una barra inventada sería dibujar una decisión que nadie tomó. Si quieres barra, ponle fecha de
inicio.

Los fines de semana salen sombreados.

### Carga

Cuánto trabajo tiene cada persona por día, repartiendo las horas estimadas entre los días
laborables de la tarea.

Dos cosas que conviene saber para leerlo bien:

- **Una tarea con varias personas cuenta entera para cada una.** No se divide entre ellas: si
  tres personas comparten una tarea de 9 horas, no son 3 horas cada una. Repartirlas sería
  inventarse quién hace qué parte.
- **Las tareas sin fecha de vencimiento aparecen aparte**, en «sin fecha». No se pueden colocar en
  ningún día, y esconderlas haría que la carga pareciera menor de lo que es.

### El detalle de una tarea

Al abrir una tarea tienes:

| | Para qué |
|---|---|
| **Subtareas** | Un solo nivel. Una subtarea no puede tener subtareas. |
| **Dependencias** | «Esta tarea espera a esta otra». Se dibujan en el Gantt. |
| **Checklist** | Puntos sueltos dentro de la tarea, más ligeros que una subtarea. |
| **Responsables** | Varios, con uno principal. |
| **Recurrencia** | La tarea se repite sola cada X días, semanas o meses. |
| **Comentarios** | Con respuestas de un nivel. |
| **Campos personalizados** | Los que haya definido tu organización. |

**Sobre las dependencias:** el sistema no deja crear un ciclo. Si A espera a B y B espera a C, no
podrás hacer que C espere a A — no habría por dónde empezar.

**Sobre la recurrencia:** las ocurrencias que genera **no heredan la recurrencia**. Si no, cada
copia empezaría a generar las suyas y la serie se multiplicaría sola.

**Sobre los comentarios:** sólo su autor puede editar uno, ni siquiera un administrador — un
comentario es de quien lo firma. Borrar sí puede el administrador, porque moderar es su trabajo.
Un comentario editado se marca como «(editado)».

---

## 3. Proyectos

Los proyectos agrupan tareas y se organizan en **espacios** y **carpetas**.

Cada proyecto tiene responsable, fechas y estado. Desde su detalle se ve el avance —cuántas
tareas hay y cuántas están terminadas— y se puede comentar.

---

## 4. Tickets: el buzón de soporte

Es lo que distingue a esta herramienta de un gestor de tareas normal: **el soporte vive dentro,
no en otra aplicación**.

Un ticket tiene solicitante, asunto, prioridad, estado y responsable, y se comenta igual que una
tarea.

### El formulario público

En `/support` hay un formulario **que no exige entrar**. Quien tenga el enlace puede abrir un
ticket dejando su correo, y aparece en la bandeja del equipo. Es la vía para clientes que no
tienen cuenta.

---

## 5. Campos personalizados

Si a tu organización le faltan datos que la herramienta no trae —«Cliente facturable», «Coste por
hora», «Canal de entrada»— los define un administrador y aparecen en el formulario de todas las
tareas o proyectos.

Tipos disponibles: **texto, número, fecha, selección, selección múltiple, usuario** y
**calculado**.

### Campos calculados

Un campo que no se rellena: se calcula a partir de otros. Se escribe una fórmula:

```
[Horas] * [Precio por hora]
```

- Los nombres de campo van **entre corchetes**, y no distingue mayúsculas.
- Operaciones: `+ - * /` y paréntesis.
- Funciones: `SI`, `REDONDEAR`, `MIN`, `MAX`, `ABS`.
- Los argumentos se separan con **punto y coma**, no con coma: la coma es el separador decimal.
  `REDONDEAR(1,5; 0)`.
- Una fórmula sólo puede usar campos **numéricos** u otros calculados.

Ejemplos que se usan de verdad:

```
[Horas] * [Precio hora]                        Importe
SI([Horas] > 0; [Coste] / [Horas]; 0)          Coste por hora, sin dividir entre cero
REDONDEAR([Importe] * 1,21; 2)                 Con IVA
MIN([Presupuesto]; [Importe])                  Lo que se puede facturar
```

**Dos cosas que sorprenden al principio, y las dos son a propósito:**

**Si falta un dato, el resultado no sale.** No sale cero: sale «faltan datos para calcularlo». Es
distinto de una hoja de cálculo, donde una celda vacía vale cero. El motivo es que un total que
dice «1.200 €» con la mitad de sus sumandos en blanco se lee como un dato y se usa para decidir;
uno que no dice nada se ve que falta rellenarlo.

La excepción es `SI`: sólo mira la rama que toma, así que
`SI([Horas] > 0; [Coste] / [Horas]; 0)` funciona aunque las horas estén vacías.

**Dividir entre cero es un error, no un hueco.** Sale en rojo y con su motivo, porque ahí los
datos están y lo que hay que arreglar es la fórmula, no rellenar una casilla.

Un campo calculado **no se puede marcar obligatorio** — no habría a quién exigírselo — y **no se
puede rellenar a mano**.

---

## 6. Automatizaciones

«Cuando pase X, si se cumple Y, haz Z». Las configura un administrador.

### Cuándo salta

| Disparador | Cuándo |
|---|---|
| Tarea creada | Al crear una tarea |
| Tarea cambia de estado | Al moverla de columna |
| Tarea cambia de prioridad | Al subirla o bajarla |
| **Tarea por vencer** | Se revisa sola: cuántos días faltan para el vencimiento |

El último es distinto de los otros tres. **Los demás reaccionan a algo que alguien hizo; éste
reacciona a que no ha pasado nada**, que es justo lo que no se nota solo.

### Qué se puede mirar

Estado, estado anterior, prioridad, prioridad anterior, proyecto, responsable, título y **días
para vencer**.

«Días para vencer» es negativo si ya venció: `-3` significa que lleva tres días de retraso. Se
compara con `menor o igual` y `mayor o igual`, que sólo valen sobre este campo.

Si pones varias condiciones se cumplen **todas** (Y). Para un «o», haz dos reglas — además se
leen mejor en la lista.

### Qué puede hacer

Cambiar el estado, cambiar la prioridad, asignar a alguien, y **avisar a una persona**. Para
avisar puedes elegir una persona concreta o «Responsable», que avisa a quien tenga la tarea en
ese momento — así la regla sigue valiendo cuando la tarea cambia de manos.

### Ejemplos

| Quiero | Disparador | Condición | Acción |
|---|---|---|---|
| Avisar dos días antes de vencer | Tarea por vencer | Días para vencer ≤ 2 | Notificar → Responsable |
| Marcar urgente lo atrasado | Tarea por vencer | Días para vencer ≤ -1 | Cambiar prioridad → Urgente |
| Bajar prioridad al cerrar | Tarea cambia de estado | Estado = Done | Cambiar prioridad → Baja |

### Cuando una automatización «no funciona»

Es la pregunta más habitual, y tiene respuesta a la vista. Cada regla guarda su **historial de
ejecuciones** con uno de estos tres resultados:

- **Aplicada** — saltó, se cumplieron las condiciones e hizo lo suyo.
- **No cumplió condiciones** — saltó, pero alguna condición no se cumplió. *Es el caso más
  frecuente cuando alguien cree que la regla está rota.* El disparador funciona; lo que hay que
  revisar es la condición.
- **Fallida** — se cumplió todo y la acción no se pudo aplicar. El motivo va escrito.

Si no aparece ninguna línea, entonces sí: el disparador no está saltando.

### Dos límites que conviene conocer

**Una automatización no dispara otra.** Si una regla cambia el estado de una tarea, eso no
activará las reglas de «tarea cambia de estado». Es deliberado: dos reglas que se deshacen la una
a la otra se llamarían para siempre.

**El aviso respeta las preferencias de quien lo recibe.** Si esa persona apagó los avisos de
vencimiento, no le llegará. Configurar una automatización no es un permiso para saltarse lo que
alguien ya decidió.

---

## 7. Documentos, chat, calendario y equipos

- **Documentos** — Editor de texto enriquecido, con páginas dentro de cada documento y
  plantillas.
- **Chat** — Canales de conversación, en tiempo real.
- **Calendario** — Eventos con fecha, enlazables a proyectos, tareas y tickets.
- **Equipos** — Grupos de personas, para asignar y filtrar por equipo.

---

### El calendario

Se abre en el mes. **Pulsa un día y el día se despliega**: sus horas, los eventos colocados en la
suya, y arriba lo que vence ese día aunque no ocurra a una hora concreta —tareas que hay que
entregar, proyectos que terminan—. Pulsando una franja horaria creas ahí mismo, con esa hora ya
puesta.

**El botón derecho abre un menú**, y ofrece cosas distintas según dónde pulses:

- Sobre un día: crear un evento, ver los eventos, ver la agenda, las tareas que se entregan ese
  día, los tickets del día, los proyectos que terminan, los ajustes de aviso, y enviar a la
  papelera todos los eventos del día.
- Sobre un evento: modificarlo, enlazarlo, cancelarlo o mandarlo a la papelera.

**Cancelar y mandar a la papelera no son lo mismo, y la diferencia importa.** Un evento cancelado
**se queda en el calendario, tachado**, con el motivo: quien mire el jueves tiene que ver que la
reunión se anuló, porque si desaparece la gente se presenta igual. La papelera es para lo que no
debería estar ahí —un evento creado por error— y tiene vuelta: el botón «Papelera» de arriba lista
lo borrado y lo recupera.

Un evento puede **enlazarse con una tarea, un ticket y un proyecto a la vez**. En el formulario
escribes dos letras y busca en todo el inquilino, no en lo que haya cargado. Quitar el enlace es
pulsar «Quitar»; no hace falta borrar el evento y rehacerlo.

Un aviso sobre las horas: los eventos se guardan en UTC y se enseñan en la hora de tu ordenador.
Todavía no hay zona horaria por inquilino, así que un equipo repartido por varios husos verá cada
uno la suya.

---

### Escribir un documento

El editor funciona como esperas de uno moderno: escribes `/` y se abre la lista de bloques
—encabezados, listas, tablas, citas, código, imágenes, checklists—, y al seleccionar texto aparece
la barra de negrita, cursiva y enlace.

**A la derecha tienes el índice de la página**, con sus encabezados. Pulsando uno vas ahí y el
cursor se queda listo para escribir. En pantallas estrechas se oculta, para no robarle ancho al
texto.

### Mencionar cosas: `@` y `#`

Escribe **`@`** para mencionar a una persona, o **`#`** para mencionar una tarea, un ticket o un
proyecto. Aparece un desplegable donde eliges; se navega con las flechas y se acepta con Enter.

Ojo a un detalle: el `@` o el `#` tienen que ir **después de un espacio** o al principio de la
línea. Escrito pegado a otra palabra no se dispara, para que «viernes.#» no se convierta en una
mención sin querer.

**Busca en todo, no en lo que tengas abierto.** Escribe dos o tres letras y el desplegable
consulta el servidor: encuentra la tarea aunque sea la número mil doscientos y nunca la hayas
visto. Da igual las tildes y las mayúsculas —«diseno» encuentra «Diseño»— y de personas puedes
escribir el nombre o el correo, que es lo único que distingue a dos Ana García.

**Y aquí está lo que hace distinto a este producto:** cuando mencionas una tarea en un documento,
**la tarea se entera**. Al abrirla verás un apartado **«Mencionado en»** con los documentos que
hablan de ella, con el texto tal como lo escribiste. Lo mismo con los tickets.

Eso significa que el acta donde se decidió algo, o la especificación que explica el porqué, dejan
de estar perdidas: aparecen justo donde alguien las va a necesitar.

Si borras la mención del texto, desaparece también de la tarea. El índice se rehace cada vez que
se guarda la página, así que no puede quedarse diciendo algo que el documento ya no dice.

---

## 8. Panel e informes

El **panel** muestra las cifras de la organización: proyectos, tareas, cuántas están terminadas,
tickets abiertos y en progreso, el reparto de tareas por estado y el avance de cada proyecto.

**Cuando una cifra no se puede calcular, sale un guion y no un cero.** Un cero se lee como un
dato —«se entrega en el acto»— y sería mentira. Verás el guion en el *tiempo de ciclo*, que
necesita saber cuándo empezó realmente cada tarea, y eso todavía no se guarda.

El *tiempo de entrega* sí sale: se calcula desde que se crea una tarea hasta que se cierra, sobre
las que se hayan cerrado desde que se empezó a guardar esa fecha. **Las primeras semanas la media
sale corta**, porque las tareas anteriores figuran como recién creadas.

En **Informes** se pide un informe eligiendo tipo y formato, y queda registrado. Ojo con lo que
viene a continuación.

---

### Construir un informe a tu medida

El botón **«Construir»** de cada informe abre el constructor. Se eligen cuatro cosas:

| | |
|---|---|
| **Datos de** | Tareas, tickets o proyectos |
| **Agrupado por** | El campo que forma las filas: estado, prioridad, responsable, una fecha… |
| **Midiendo** | Cuántos hay, o una suma o media de un campo numérico |
| **Pintado como** | Tabla, barras, líneas o tarta |

Si agrupas por una fecha, se te pregunta además si quieres verlo **por día, semana, mes o año**.

**Los filtros** se añaden uno a uno. Sólo se te ofrecen las combinaciones que tienen sentido: no
puedes pedir «prioridad mayor que», porque una prioridad no se ordena así, ni «está vacío» sobre
un campo que siempre tiene valor. Y cuando el campo es una lista cerrada —un estado, una
prioridad— eliges de un desplegable en vez de escribirlo: escribir «Open» a mano es como se acaba
filtrando por algo que no existe y viendo un informe vacío que parece un informe sin datos.

**«Ver resultado» te lo enseña antes de guardar.** Si la combinación no se puede calcular, se te
dice **cuál** de las piezas falla, ahí mismo, junto a los desplegables.

Debajo de la tabla verás una línea con el origen, los filtros aplicados y cuántas filas la
componen. **Esa línea viaja con el fichero exportado**, para que quien lo reciba en un correo sepa
qué está mirando sin tener que preguntar.

Un detalle: cuando no hay nada que promediar —los días medios hasta resolver de unos tickets que
nadie ha resuelto— verás una raya, no un cero. Un cero ahí diría que se resuelven al instante.

---

### Que un informe llegue solo

Un informe se puede **programar** para que se genere solo: **cada día**, **cada semana** en el día
que elijas, o **cada mes**. Se indica la hora, y es tu hora local.

Cuando toca, el informe se genera y te llega un correo con el aviso; el fichero está en la
pantalla de informes, como cualquier otro. **El correo lleva un enlace, no el fichero adjunto**:
un informe de varios megas rebota en la mitad de los servidores de correo, y el enlace además
comprueba que quien lo abre tenga permiso.

Dos cosas que **no** se pueden hacer, a propósito:

- **No hay frecuencia «cada hora».** Un informe que llega cada hora se deja de leer el segundo día
  y acaba escondiendo los que sí importan.
- **No se puede programar el día 31.** No existiría en febrero ni en los meses de treinta días, y
  fallaría cuatro meses al año sin que nadie supiera por qué. El día 28 es el último que se admite.

---

### Llevarte un informe a un fichero

En la lista de informes, cada uno tiene tres botones: **Pdf**, **Excel** y **Csv**. Se ofrecen los
tres siempre, independientemente del formato con el que se creó el informe, porque lo normal es
querer el PDF para mandarlo y el Excel para trabajarlo.

**El fichero lo prepara el servidor, no tu navegador.** Al pulsar recuperas el control
inmediatamente y se te avisa cuando esté: no hay que quedarse mirando la pantalla. Ese aviso se
puede desactivar como cualquier otro, en las preferencias de notificaciones.

Si tarda más de lo normal, se te dice **y el informe sigue generándose**; no se ha perdido. Y si
algo falla, se te dice **qué** ha fallado, no sólo que falló.

Sobre los ficheros:

- El **CSV** se separa por punto y coma, no por comas, para que los decimales españoles —«1,5»—
  no partan las columnas. Y lleva la marca que hace que Excel abra bien los acentos.
- El **Excel** viene con la cabecera fija y el autofiltro puesto, listo para ordenar y filtrar.
- El **PDF** se gira a horizontal solo cuando el informe tiene muchas columnas.

Un informe muy grande se recorta, **y el propio fichero lo dice** en su línea de cabecera. Nunca
se recorta en silencio.

---

### Tu panel de inicio

El **Dashboard** tiene dos partes. Arriba, unas cifras del espacio de trabajo que no se tocan.
Debajo, **«Mis gráficas»**: tu panel, y es **tuyo**. Lo que coloques ahí no cambia lo que ven los
demás, y lo que ellos coloquen no cambia el tuyo.

La primera vez que entras se monta solo con seis gráficas: tickets por estado y por prioridad,
tareas por estado, por responsable y por proyecto, y tickets abiertos por mes.

**Cada recuadro es un informe de verdad**, no una gráfica fija. Eso significa que puedes abrirlo en
el constructor de informes y cambiar lo que enseña —el origen, los filtros, la agrupación— y el
recuadro cambia con él. Y cualquier informe que construyas se puede poner en el panel.

La **✕** de cada recuadro lo quita del panel. **No borra el informe**: sigue en tu lista de
reportes y lo puedes volver a poner.

Debajo de cada gráfica hay una línea pequeña que dice de qué está hecha —origen, filtros y cuántas
filas la componen—. Está ahí para que no haya que adivinar qué se está mirando.

Si un recuadro no puede pintarse, **lo dice en su sitio y los demás siguen funcionando**. Un solo
informe mal configurado no deja la pantalla en blanco.

---

## 9. Avisos y preferencias

En el icono de la campana están los avisos. En sus preferencias se decide qué llega y por qué
vía.

**Todo llega activado salvo lo que hace ruido.** Lo que esperas —que te asignen algo, que te
mencionen, que termine una exportación que pediste, que algo tuyo esté por vencer— viene
encendido. Lo que informa de actividad ajena —una tarea que completó otro, un ticket que tocó
otro— viene apagado. Se puede cambiar todo.

**Horas de silencio.** Puedes fijar un tramo en el que no quieres recibir nada. El tramo puede
cruzar la medianoche: de 22:00 a 08:00 funciona como esperas.

---

## 10. Administración

Sólo para administradores:

- **Usuarios** — Alta, baja y cambio de papel.
- **Campos personalizados** — Ver [apartado 5](#5-campos-personalizados).
- **Automatizaciones** — Ver [apartado 6](#6-automatizaciones).
- **Webhooks** — Avisar a otro sistema cuando pasa algo aquí, firmado con HMAC-SHA256.
- **Etiquetas** — Vocabulario común para clasificar.

---

## Lo que todavía no hace

Esto es tan parte de la guía como lo anterior.

### En el editor faltan dos cosas

**Los bloques no se arrastran**: no hay una manija a la izquierda de cada párrafo para reordenarlos
con el ratón. Se reordenan cortando y pegando, como en cualquier editor.

**No hay comentarios dentro del texto**: se puede comentar una tarea o un ticket entero, pero no
señalar un párrafo concreto de un documento y comentar sobre él.

### Mencionar a alguien no le avisa

Si escribes `@` y mencionas a una persona, la mención queda guardada y se puede consultar, pero
**no le llega ninguna notificación**. Por ahora hay que decírselo por otro medio.

### Los recuadros del panel no se arrastran todavía

Las gráficas se colocan solas y se pueden quitar, pero **no se pueden mover ni redimensionar con
el ratón**. La colocación se guarda —el servidor la respeta— pero falta el gesto en la pantalla.

### La vista previa del constructor enseña una tabla

El constructor deja elegir cómo quieres ver el informe —barras, líneas, tarta— y el panel lo pinta
así. Pero **su propia vista previa sigue enseñando una tabla**. El dato es el mismo; lo que falta
es reutilizar ahí la gráfica.

Y **«barras apiladas» se pinta como barras normales**: apilar necesita agrupar por dos campos a la
vez, y de momento se agrupa por uno.

### El tiempo de ciclo no se calcula

Sale como un guion. Necesita saber cuándo una tarea entró realmente en «En progreso», y hoy sólo
se guarda su estado actual, no el historial de cambios.

### Archivar, borrar y compartir todavía no tienen botón

Las vistas de **Archivado**, **Papelera**, **Compartido conmigo** y **Privado** funcionan y
enseñan lo que les toca, pero **en la pantalla no hay aún un botón para archivar algo, mandarlo
a la papelera o compartirlo con alguien**. Todo eso existe en la API y está probado; lo que
falta es el control en la interfaz.

En la práctica: hoy verás esas cuatro vistas casi siempre vacías, no porque estén rotas, sino
porque todavía no hay forma cómoda de llenarlas.

La única excepción es **borrar**, que sí tiene botón donde ya lo tenía —y que hasta ahora decía
haber borrado sin borrar nada—. Ahora manda a la papelera de verdad.

### El historial de automatizaciones no tiene pantalla

El registro existe y se puede consultar por la API, pero la pantalla de automatizaciones todavía
no lo enseña.

### Los campos calculados no se pueden ordenar ni filtrar

Su valor se calcula al mostrarlo, no se guarda. La ventaja es que **nunca está desfasado**; el
precio es que la base de datos no tiene una columna por la que ordenar.

### Los campos personalizados sólo aplican a tareas y proyectos

Todavía no a tickets.
