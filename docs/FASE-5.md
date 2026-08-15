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

## 5A — Un solo menú de navegación

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

### Orden sugerido

1. Componente `app-panel-de-navegacion` compartido, alimentado por un vocabulario por módulo,
   con las entradas que **hoy sí** se pueden filtrar (ver todo, asignado a mí, creado por mí, y
   las particulares que salen de datos existentes).
2. Archivado y papelera, transversales, con su columna y su filtro por defecto.
3. Visibilidad y compartición.
4. Favoritos.

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

## 5C — Dashboard

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

**Herramienta de gráficas:** hay dos componentes propios (`doughnut-chart`, `line-chart`). Para
lo que viene —barras apiladas, series temporales, tablas dinámicas— conviene decidir si se
crecen o se adopta una librería. Recomiendo decidirlo con la lista de widgets delante y no
antes.

---

## 5D — Reportes

- **Lista de todos los reportes**, los de serie y los creados.
- **Constructor**: origen de datos, filtros, agrupación, medida, forma de pintarlo.
- **Exportación** a Excel, PDF y CSV.
- **Programación**: que un reporte se genere y se envíe solo. El módulo `Communication` ya manda
  correo y la Fase 4 dejó un motor de reglas del que se puede aprender.

**Sobre la exportación, una advertencia:** hacerla en el cliente es rápido de escribir y se
rompe con volumen —el navegador no puede con cien mil filas— y además no sirve para la
programación, que ocurre sin nadie delante. **Debe ser del servidor**, con el mismo motor de
consulta que pinta el reporte en pantalla.

---

## Lo que hay que decidir antes de empezar

1. **Alcance del 5A**: ¿el menú completo con archivado, papelera, visibilidad y favoritos —que
   es trabajo de backend en todos los módulos— o primero el menú con lo que hoy sí se puede
   filtrar, y esos cuatro conceptos después?
2. **Orden**: los cuatro bloques son independientes salvo que el 5C depende del 5D para no
   acabar con dos motores de consulta.
3. **Gráficas**: crecer los componentes propios o adoptar una librería.

---

## Lo que sigue pendiente de antes

- Los **campos calculados**, dejados fuera del 4B.
- **`GET /tasks/{id}/comments` devuelve 404** y llena de avisos el panel de detalle.
- Un fallo levanta **dos avisos duplicados**: el del interceptor y el del componente.
- **Cambiar la contraseña de MySQL**, del lado de quien administra la máquina.
