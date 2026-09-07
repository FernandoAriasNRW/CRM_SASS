# Fase 5 — Navegación, editor y analítica

**Escrito:** 2026-08-15, al cerrar la Fase 4.

Cuatro bloques de trabajo. No son un ajuste: cada uno toca varios módulos y los cuatro juntos
son comparables en tamaño a la Fase 4 entera. Se numeran por orden de dependencia, no de
importancia: el 5A da la estructura sobre la que se cuelgan las vistas nuevas del 5C y el 5D.

El objetivo declarado es parecerse a ClickUp **con un diferencial**. El diferencial que este
producto ya tiene y conviene explotar es el **helpdesk integrado** (§3 del roadmap): ni ClickUp
ni Monday traen ticketing de serie. Todo lo que sigue debería reforzarlo, y no limitarse a
copiar pantallas.

---

## 5A — Un solo menú de navegación  ✅ **hecho** (2026-09-04)

> **Estado.** Los cuatro pasos del orden sugerido están construidos y verificados contra la API
> levantada. Lo que queda es interfaz, no cimientos: no hay botones para archivar, borrar ni
> compartir desde la pantalla —sólo desde la API—, y Docs conserva su panel propio. Está anotado
> en `AUDITORIA.md` §10.
>
> Lo que sí se cumple, que era la condición de esta fase: **las ocho entradas del menú filtran de
> verdad**. Comprobado en los tres módulos, con `/tickets?filter=mine` pasando de devolver 190 de
> 190 a devolver 5.

**El problema:** hoy hay dos menús que hacen lo mismo y no se parecen. El desplegable que sale
al pasar el ratón por un icono del sidebar (`app.component.html`, `NavigationSignalStore`) y el
panel lateral propio de Docs (`docs.component.html`, ancho `w-64`, con anclado y `hover`). El de
Docs es el bueno: se ancla, se ve entero y da sitio a un árbol.

**Qué hacer:** extraer el panel de Docs a un componente compartido y que **todas** las vistas lo
usen, alimentado por el vocabulario de cada módulo.

### El vocabulario del menú

Transversal, en todos los módulos:

| Entrada | Qué filtra |
|---|---|
| Ver todo | Sin filtro |
| Asignado a mí | Donde figuro como responsable |
| Creado por mí | Donde figuro como autor |
| Compartido conmigo | Lo que otro me compartió explícitamente |
| Privado | Sólo mío |
| Favoritos | Lo que marqué |
| Archivado | Fuera de la vista normal, sin borrar |
| Papelera | Borrado, recuperable |

Particular de cada módulo:

| Módulo | Entradas propias |
|---|---|
| Docs | Notas de reunión, Plantillas, Publicados |
| Tareas | Vencen esta semana, Bloqueadas, Sin estimar |
| Tickets | Sin asignar, Por prioridad, Vencidos de SLA |
| Proyectos | Activos, En riesgo, Cerrados |
| Reportes | Míos, Programados, Compartidos |

**Lo que hay que decidir antes de escribir código:** «compartido conmigo», «privado»,
«archivado», «papelera» y «favoritos» **no existen en el backend todavía**. Son cuatro conceptos
nuevos que atraviesan todos los módulos:

- **Visibilidad** (privado / equipo / compartido con personas concretas) → tabla de permisos por
  entidad. Ya existe `EntityPermissions` en Identity; hay que ver si sirve o si hace falta otra.
- **Archivado** → una columna `ArchivedAtUtc` en cada agregado, y que **todas** las consultas la
  respeten. Es la parte que se rompe en silencio: una consulta que se olvide de filtrar enseña
  archivado como si estuviera vivo.
- **Papelera** → borrado lógico. Varios módulos ya tienen `IsDeleted`; hay que unificarlo.
- **Favoritos** → tabla por usuario y entidad. Docs ya tiene `starredDocIds` en el cliente; hay
  que ver si se persiste.

**Sin eso, el menú tendría entradas que no filtran nada.** Es lo que hay que evitar: un menú que
promete y devuelve la misma lista es peor que un menú corto.

### Orden sugerido — y cómo quedó

1. ✅ Componente `app-panel-de-navegacion` compartido, en Tareas, Tickets y Proyectos. El
   vocabulario vive en `vocabulario-del-menu.ts` y **sólo admite filtros que el servidor sabe
   aplicar**. El filtro activo vive en la URL, así que una vista filtrada se comparte por enlace
   y el botón de atrás funciona.
2. ✅ Archivado y papelera. La decisión que importa: **van en el filtro global**
   (`TenantQueryFilter`), no en un `Where` por consulta. El plan avisaba de que ésta era «la
   parte que se rompe en silencio», y componerlo en el filtro global significa que **ninguna
   consulta puede olvidarse**: para ver lo escondido hay que pedirlo con `VerTambien`, que no es
   `IgnoreQueryFilters()` —eso apagaría también el aislamiento por inquilino—.
3. ✅ Visibilidad y compartición, sobre la tabla `EntityPermissions` que ya existía. No se creó
   una tabla nueva: dos tablas diciendo quién ve qué acabarían discrepando. «Privado» se calcula
   restando lo compartido en lugar de guardar un campo, que sería una segunda fuente de verdad.
4. ✅ Favoritos (hecho antes, en el commit `9f63a0d`).

**Las entradas propias de cada módulo del cuadro de arriba —«Vencen esta semana», «Sin asignar»,
«Vencidos de SLA», «En riesgo»— no están.** El servidor no las sabe filtrar todavía, y ponerlas
sería justo lo que esta fase viene a quitar.

---

## 5B — Editor tipo Notion

**Lo que hay:** TipTap, y es la causa de casi todos los `any` de la deuda medida (§6).

**Lo que falta para parecerse a Notion**, por orden de valor:

1. **Menú de barra `/`**: escribir `/` abre una lista de bloques (encabezado, lista, tabla, cita,
   código, divisor, imagen, checklist).
2. **Bloques arrastrables**: manija a la izquierda de cada bloque para reordenar.
3. **Menú flotante de selección**: al seleccionar texto, negrita/cursiva/enlace/color.
4. **Barra lateral de esquema**: los encabezados del documento como índice navegable.
5. **Comentarios en línea** anclados a un bloque.
6. **Menciones** `@persona` y `#tarea`, que enlazan con el resto del producto. **Aquí está el
   diferencial**: mencionar un ticket dentro de un documento y que el ticket muestre el
   documento es algo que ClickUp hace a medias.

TipTap tiene extensiones para 1, 2 y 3. Las 4, 5 y 6 son trabajo propio.

---

## 5C — Dashboard  ✅ **hecho** (2026-09-07)

> **Estado.** La rejilla se guarda por persona, cada recuadro es un informe, los datos de todos
> llegan en una sola petición y las gráficas son ECharts con los módulos justos. Los seis
> recuadros de partida enseñan datos reales el primer día; hay una prueba que lo comprueba y que
> falla si alguno viene vacío o roto.
>
> **Lo que se ha quitado:** la tarta fija de «distribución de tareas», que ahora es uno de los
> recuadros. **Lo que se ha conservado fijo:** el avance por proyecto y el burndown, porque el
> catálogo de informes todavía no los sabe expresar —el primero necesita un porcentaje de
> completado, el segundo dos series a la vez— y quitarlos habría sido perder algo que funciona.
>
> **Lo que había antes y no hacía nada:** un gestor de paneles donde se podían crear, nombrar y
> marcar como públicos, y **al pulsarlos no pasaba nada** —la selección iba a una señal que no
> pintaba nada y la columna de widgets no la leía nadie—. Nunca llegó a haber una fila en esa
> tabla.

**Lo que pidió el usuario:** un resumen personalizable de los reportes, donde se ven estadísticas
que faciliten el análisis —tickets por área, por ejemplo—, con unas por defecto y libertad para
añadir y quitar.

**Decisiones de fondo:**

- **La rejilla se guarda por usuario.** Un dashboard es de quien lo mira; si se guarda por
  inquilino, dos personas se pisan la configuración.
- **Cada widget declara de qué consulta vive.** Un widget que trae sus propios datos con su
  propia llamada convierte el dashboard en veinte peticiones; una consulta declarada permite
  pedirlas juntas.
- **Nada de datos de ejemplo.** Un dashboard que enseña una gráfica bonita con datos inventados
  es la peor pantalla posible de todo el producto: se toman decisiones con ella.
- **Los widgets vienen del reporte, no al revés.** Un widget es «este reporte, pintado así». Eso
  evita dos motores de consulta y hace que el 5D dé sentido al 5C.

**Widgets de partida:** tickets por área, tickets por estado, tareas por responsable, carga de
la semana, cumplimiento de fechas, tiempo medio de resolución de tickets.

> **Los que salieron, y por qué no son todos ésos.** El criterio manda: «sólo se ofrece de serie
> lo que los datos de hoy pueden responder». Están tickets por estado, tickets por prioridad,
> tareas por estado, tareas por responsable, tareas por proyecto y tickets abiertos por mes.
>
> Fuera: **tickets por área**, que necesita campos personalizados en tickets; **cumplimiento de
> fechas** y **tiempo medio de resolución**, que el motor sabe calcular pero que sin tickets
> resueltos salen con una raya en todos los meses — estrenar el producto con una gráfica vacía es
> justo lo que el plan prohibía.

**Herramienta de gráficas:** hay dos componentes propios (`doughnut-chart`, `line-chart`). Para
lo que viene —barras apiladas, series temporales, tablas dinámicas— conviene decidir si se
crecen o se adopta una librería. Recomiendo decidirlo con la lista de widgets delante y no
antes.

> **Cómo quedó.** ECharts con `ngx-echarts@21` —la que corresponde a este Angular—, importada con
> los módulos justos: tres tipos de gráfica, cuatro componentes y el renderizador de canvas. El
> mapa de módulos vive en un solo fichero (`echarts-modulos.ts`), que era la condición del estudio.
>
> **El coste, medido:** el paquete del panel pasa de 62 kB a 789 kB en bruto, **224 kB por la
> red**, y sólo se descarga al abrir el panel. Se le ha dado presupuesto propio (aviso a 850 kB,
> error a 1 MB) en vez de subir el de todas las pantallas.
>
> `line-chart` se conserva para el burndown, que no es expresable como informe todavía;
> `doughnut-chart` se ha quedado sin uso y queda anotado.

---

## 5D — Reportes

- [x] **Lista de todos los reportes**, los de serie y los creados. Ya existía.
- [x] **Constructor**: origen de datos, filtros, agrupación, medida, forma de pintarlo.
- [x] **Exportación** a Excel, PDF y CSV — asíncrona, del servidor, con estado y aviso.
- [x] **Programación**: que un reporte se genere y se envíe solo.

> **Estado de la exportación.** Construida entera: se pide y se recupera el control al instante,
> un trabajador de segundo plano genera el fichero, y al terminar avisa respetando las
> preferencias de notificación. Los tres formatos producen ficheros reales —comprobado
> descargándolos y mirando su firma: BOM en el CSV, `PK` en el `.xlsx`, `%PDF-` en el PDF—.
>
> **Lo que había antes no exportaba nada.** `MarkAsGenerated` guardaba una URL construida a mano
> (`/reports/{id}/{nombre}.pdf`) que no apuntaba a ningún fichero y que ningún endpoint servía.
> El informe constaba como generado y no había nada que descargar. Los cuatro campos que
> guardaban ese estado se han quitado del informe: el estado vive ahora en `Exportacion`, una por
> petición y por formato, porque un informe se exporta muchas veces.
>
> **El constructor** guarda una definición neutra —origen, filtros, agrupación, medida, forma— y
> nunca SQL ni opciones de ECharts. Cada pieza se valida contra un catálogo que el servidor
> **sirve a la pantalla**: ninguna lista de opciones está escrita en el frontend, que es lo que
> impide repetir el fallo de ofrecer algo que el motor no sabe hacer. Hay vista previa antes de
> guardar y una prueba que recorre el catálogo entero comprobando que todo lo que ofrece se puede
> calcular.
>
> **La programación** deja la exportación pedida y el generador que ya existía la recoge: un
> informe programado y uno pedido a mano recorren el mismo camino, para que no haya dos motores
> que se separen. Diaria, semanal o mensual; nada de «cada hora», que sería un generador de correo
> no deseado.
>
> **Lo que sigue sin poderse hacer** es el ejemplo de «tickets por área»: requiere campos
> personalizados en tickets como dimensión de análisis, y hoy los campos personalizados son de
> tareas y proyectos. Está anotado en la auditoría.

**Sobre la exportación, una advertencia:** hacerla en el cliente es rápido de escribir y se
rompe con volumen —el navegador no puede con cien mil filas— y además no sirve para la
programación, que ocurre sin nadie delante. **Debe ser del servidor**, con el mismo motor de
consulta que pinta el reporte en pantalla.

---

## Decisiones tomadas (2026-08-15)

1. **El menú va completo, con backend.** Archivado, papelera, visibilidad y favoritos se
   construyen de verdad; nada de entradas que no filtran.
2. **El orden es 5A → 5D → 5C → 5B.** El dashboard va después de los reportes porque se
   alimenta de ellos: el dashboard es lo primero que ve quien entra y resume varios reportes;
   reportes es donde se generan y se crean.
3. **Gráficas: Apache ECharts** vía `ngx-echarts`. El estudio y el porqué, abajo.

---

## Estudio: qué librería de gráficas

Se compararon las candidatas reales para Angular en 2026: Chart.js (ya está en el proyecto),
ApexCharts, ECharts, Highcharts, FusionCharts, Vega-Lite y D3.

**Recomendación: Apache ECharts con `ngx-echarts`.** Tres razones, en orden de peso:

1. **Es la única que resuelve bien la exportación desde el servidor.** ECharts renderiza en
   Node **a SVG sin una sola dependencia nativa**. Chart.js y ApexCharts obligan a `node-canvas`
   —pila de C/C++, Cairo o Skia— o a un Chrome headless, y eso hay que instalarlo, mantenerlo y
   vigilarlo en el contenedor de la API. Como la exportación va a ser asíncrona y del servidor,
   esta diferencia decide por sí sola.
2. **Cubre lo que un constructor de reportes tiene que ofrecer**: barras apiladas, series
   temporales, mapas de calor, treemaps, dispersión, embudos. Chart.js se queda corta en cuanto
   alguien quiere algo más que barras y líneas, y eso pasaría la primera semana.
3. **Encaja con la versión de Angular del proyecto.** El proyecto va por Angular 21;
   `ngx-echarts` publicó su v21 y sigue vivo (v22 en junio de 2026). No es una librería que haya
   que adoptar cruzando los dedos.

**El coste, que hay que asumir a conciencia:** ECharts pesa. Importada entera se come el
presupuesto de bundle, que ya está pasado (1,51 MB frente a 1,00 MB, §6). **Hay que importarla
con los módulos justos** —`echarts/core` y los `charts`/`components` que se usen— y eso obliga a
que el mapa «tipo de gráfica → módulos que carga» esté en un solo sitio.

**La segunda opción, y por qué no gana: Vega-Lite.** Conceptualmente es la que mejor encaja,
porque una gráfica *es* una especificación JSON, que es justo lo que hay que guardar cuando el
usuario construye la suya. Pero su integración con Angular es escasa y su runtime interactivo
pesa más de lo que aporta aquí. Se le toma prestada la idea, que es la parte valiosa:

> **Lo que se guarda no son opciones de ECharts, sino una definición neutra de la gráfica**
> —origen, filtros, agrupación, medida, forma—. La definición se traduce a opciones de ECharts
> para pintarla en el navegador, y **la misma definición** la usa el servidor para exportarla.
> Guardar opciones de ECharts ataría todos los reportes guardados a la librería: cambiarla algún
> día invalidaría el trabajo de los usuarios, no sólo el nuestro.

Chart.js se queda mientras tanto: los dos componentes que ya existen siguen funcionando y no hay
por qué reescribirlos el primer día.

---

## Exportación: asíncrona, del servidor, y avisando

Como pediste:

1. El usuario pide la exportación y **recupera el control inmediatamente**; no se queda mirando
   una barra.
2. El trabajo se encola con su estado (`Pendiente`, `Generando`, `Lista`, `Fallida`).
3. Al terminar, **se le avisa**. El aviso viene activado de serie y **se puede desactivar como
   cualquier otro**, desde las preferencias de notificaciones que ya existen.
4. Si sigue en la pantalla, el aviso es en la propia aplicación; si no, por los canales que ya
   tenga configurados.

**Lo que hay que cuidar, porque es donde estas cosas se pudren:** un trabajo que falla tiene que
decir **por qué** y quedar visible, no desaparecer. Una exportación que se queda en «generando»
para siempre es peor que un error, porque nadie sabe si esperar.

> **Cómo quedó (2026-09-04).** Los cuatro puntos están construidos. Sobre el aviso de arriba: una
> exportación que lleve más de diez minutos «generando» se vuelve a coger, y agotados tres
> intentos se marca fallida **con el motivo guardado en la fila**, no sólo en el registro del
> servidor —quien pregunta «¿por qué no salió mi informe?» no tiene acceso a los registros—. Hay
> prueba de integración que provoca un fallo real y comprueba que el motivo explica qué pasa.

---

## Estudio: qué reportes y qué widgets de serie

**El criterio: sólo se ofrece de serie lo que los datos de hoy pueden responder.** Un widget de
serie que sale vacío para todo el mundo enseña que el producto no sabe de qué habla.

Con lo que hay hoy (tickets, tareas con prioridad, responsables, fechas, horas, dependencias y
checklists; proyectos; documentos):

| Reporte de serie | Widget que alimenta |
|---|---|
| Tickets por estado y antigüedad | Embudo de estados |
| Tickets abiertos por responsable | Barras horizontales |
| Tiempo medio hasta la resolución | Serie temporal con la media móvil |
| Tareas por estado y proyecto | Barras apiladas |
| Carga por persona y semana | La tabla que ya hace el 4C |
| Cumplimiento de fechas límite | Porcentaje a tiempo frente a tarde |
| Tareas bloqueadas | Contador con la lista detrás |
| Documentos creados por mes | Serie temporal |

**Tu ejemplo —«tickets para el área de diseño, front o back»— no se puede hacer hoy**, y merece
la pena decirlo claro: **no hay un campo de área en los tickets**. Hay tres caminos y el tercero
es el bueno:

1. Añadir un campo fijo `Area` a los tickets. Rápido, y equivocado: cada cliente tiene sus áreas.
2. Reutilizar las etiquetas. Existen, pero una etiqueta no es una dimensión: nada impide poner
   tres áreas a un mismo ticket, y entonces los totales no suman.
3. **Usar los campos personalizados del 4B como dimensión de análisis.** El cliente define un
   campo «Área» de tipo selección con sus valores, y el constructor de reportes lo ofrece como
   agrupación igual que el estado o la prioridad.

El tercero es, además, **el diferencial**: agrupar por un campo que el propio cliente definió es
justo lo que ni ClickUp ni Monday hacen bien. Obliga a dos cosas: extender los campos
personalizados a tickets —hoy son de tareas y proyectos— y que el motor de consulta sepa
agrupar por ellos.

---

## Lo que sigue pendiente de antes

Se aborda **antes de empezar el 5A**: son cabos sueltos que ensucian pantallas que la Fase 5 va
a tocar, y arreglarlos después sería hacerlo dos veces.

- [x] **Un fallo levantaba dos avisos duplicados**: el del interceptor y el del componente.
  Resuelto con un `HttpContextToken`: la petición que va a explicar su propio error pide no
  avisar, y el interceptor lo respeta.
- [ ] **Comentarios**. `GET /tasks/{id}/comments` devuelve 404 porque **no existían**: no hay
  ninguna entidad de comentario en todo el backend, aunque el panel de detalle lleve su interfaz
  escrita. Se implementan para tareas, tickets y proyectos.
- [ ] **Campos calculados**, dejados fuera del 4B.

La contraseña de MySQL **no está pendiente**: es una base de pruebas y así queda anotado.
