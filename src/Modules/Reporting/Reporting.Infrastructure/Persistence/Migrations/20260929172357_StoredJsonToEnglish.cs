using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Las claves y los valores en español que viven dentro del JSON guardado de informes y paneles.
    ///
    /// <b>La definición de cada informe</b> (<c>Reports.DefinitionJson</c>) se guardaba con claves
    /// como <c>"origen"</c> o <c>"agrupacion"</c> y con las claves del catálogo en español
    /// (<c>"Tareas"</c>, <c>"estado"</c>, <c>"conteo"</c>, <c>"barras"</c>…). <b>La colocación de los
    /// recuadros de cada panel</b> (<c>Dashboards.WidgetsJson</c>) lleva <c>"ancho"</c>,
    /// <c>"alto"</c>, <c>"forma"</c> y <c>"titulo"</c>. El código ahora lee todo en inglés: sin
    /// reescribirlo, un informe guardado se leería sin origen y fallaría al exportarse, y los
    /// recuadros del panel perderían su tamaño.
    ///
    /// <b>Cada valor sólo se cambia detrás de su clave</b> (<c>"groupBy":"estado"</c>, no cualquier
    /// <c>"estado"</c>), así que el texto libre de un filtro —<c>"value"</c>— se queda como estaba
    /// aunque coincida con una clave del catálogo. Y nada se toca si la comilla va escapada: eso es
    /// que está dentro de un texto, no que es una clave del JSON.
    /// </summary>
    public partial class StoredJsonToEnglish : Migration
    {
        /// <summary>Las claves del JSON. Escritas aquí, no leídas del código: una migración hace siempre lo mismo.</summary>
        private static readonly (string Spanish, string English)[] Keys =
        [
            ("origen", "dataSource"),
            ("agrupacion", "groupBy"),
            ("medida", "measure"),
            ("forma", "visualization"),
            ("filtros", "filters"),
            ("granularidad", "granularity"),
            ("maximoDeGrupos", "maxGroups"),
            ("campo", "field"),
            ("operador", "operator"),
            ("valor", "value"),
            ("ancho", "width"),
            ("alto", "height"),
            ("titulo", "title"),
        ];

        /// <summary>
        /// Los valores del catálogo, con la clave (ya en inglés) detrás de la que pueden aparecer.
        /// </summary>
        private static readonly (string Key, string Spanish, string English)[] Values =
        [
            ("dataSource", "Tareas", "Tasks"),
            ("dataSource", "Proyectos", "Projects"),

            ("groupBy|field", "estado", "status"),
            ("groupBy|field", "prioridad", "priority"),
            ("groupBy|field", "responsable", "assignee"),
            ("groupBy|field", "proyecto", "project"),
            ("groupBy|field", "vencimiento", "due_date"),
            ("groupBy|field", "creacion", "created_at"),
            ("groupBy|field", "horas", "estimated_hours"),
            ("groupBy|field", "agente", "agent"),
            ("groupBy|field", "resolucion", "resolved_at"),
            ("groupBy|field", "dueno", "owner"),
            ("groupBy|field", "inicio", "start_date"),

            ("measure", "conteo", "count"),
            ("measure", "suma_horas", "sum_estimated_hours"),
            ("measure", "media_horas", "avg_estimated_hours"),
            ("measure", "media_dias_resolucion", "avg_days_to_resolve"),

            ("granularity", "dia", "day"),
            ("granularity", "semana", "week"),
            ("granularity", "mes", "month"),
            ("granularity", "ano", "year"),

            ("visualization", "tabla", "table"),
            ("visualization", "barras", "bar"),
            ("visualization", "barras_apiladas", "stacked_bar"),
            ("visualization", "lineas", "line"),
            ("visualization", "tarta", "pie"),

            ("operator", "es", "is"),
            ("operator", "no_es", "is_not"),
            ("operator", "contiene", "contains"),
            ("operator", "mayor_que", "greater_than"),
            ("operator", "menor_que", "less_than"),
            ("operator", "vacio", "empty"),
            ("operator", "no_vacio", "not_empty"),
        ];

        private static readonly (string Table, string Column)[] Columns =
        [
            ("Reports", "DefinitionJson"),
            ("Dashboards", "WidgetsJson"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Primero las claves y después los valores, que se buscan detrás de la clave nueva.
            foreach (var (table, column) in Columns)
            {
                foreach (var (spanish, english) in Keys)
                    RenameKey(migrationBuilder, table, column, spanish, english);

                foreach (var (key, spanish, english) in Values)
                    RenameValue(migrationBuilder, table, column, key, spanish, english);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Al revés: los valores mientras las claves siguen en inglés, y después las claves.
            foreach (var (table, column) in Columns)
            {
                foreach (var (key, spanish, english) in Values)
                    RenameValue(migrationBuilder, table, column, key, english, spanish);

                foreach (var (spanish, english) in Keys)
                    RenameKey(migrationBuilder, table, column, english, spanish);
            }
        }

        /// <summary><c>"old":</c> → <c>"new":</c>, salvo que la comilla vaya escapada.</summary>
        private static void RenameKey(MigrationBuilder migrationBuilder, string table, string column, string from, string to)
            => migrationBuilder.Sql(
                $"UPDATE `{table}` SET `{column}` = REGEXP_REPLACE(`{column}`, '(^|[^\\\\\\\\])\"{from}\":', '$1\"{to}\":') "
                + $"WHERE `{column}` LIKE '%\"{from}\":%';");

        /// <summary><c>"clave":"old"</c> → <c>"clave":"new"</c>, sin mirar mayúsculas en el valor viejo.</summary>
        private static void RenameValue(
            MigrationBuilder migrationBuilder, string table, string column, string key, string from, string to)
            => migrationBuilder.Sql(
                $"UPDATE `{table}` SET `{column}` = REGEXP_REPLACE(`{column}`, '(\"(?:{key})\":\")(?i:{from})\"', '$1{to}\"') "
                + $"WHERE `{column}` LIKE '%\"{from}\"%';");
    }
}
