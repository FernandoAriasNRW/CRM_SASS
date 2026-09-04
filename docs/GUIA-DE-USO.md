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
- **Calendario** — Eventos con fecha, vinculables a proyectos y tareas.
- **Equipos** — Grupos de personas, para asignar y filtrar por equipo.

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

### Los informes no se descargan

Se puede pedir un informe y queda registrado, y el botón **«Generar» marca el informe como
generado**. Pero **no se produce ningún fichero**: no hay nada que descargar, y el enlace no
lleva a ninguna parte.

La exportación de verdad —generarla en el servidor, en segundo plano, y avisar al terminar— está
planificada y todavía no construida. La preferencia para desactivar ese aviso ya existe, pero el
aviso aún no llega porque no hay exportación que lo dispare.

**Mientras tanto:** las cifras del panel están al día y son fiables; lo que no hay es cómo
llevárselas a un fichero.

### El tiempo de ciclo no se calcula

Sale como un guion. Necesita saber cuándo una tarea entró realmente en «En progreso», y hoy sólo
se guarda su estado actual, no el historial de cambios.

### El historial de automatizaciones no tiene pantalla

El registro existe y se puede consultar por la API, pero la pantalla de automatizaciones todavía
no lo enseña.

### Los campos calculados no se pueden ordenar ni filtrar

Su valor se calcula al mostrarlo, no se guarda. La ventaja es que **nunca está desfasado**; el
precio es que la base de datos no tiene una columna por la que ordenar.

### Los campos personalizados sólo aplican a tareas y proyectos

Todavía no a tickets.
