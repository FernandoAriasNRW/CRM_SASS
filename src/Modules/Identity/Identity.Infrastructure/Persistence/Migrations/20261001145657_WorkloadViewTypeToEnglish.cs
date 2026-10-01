using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// La vista de carga de trabajo de Tareas pasa de llamarse <c>carga</c> a <c>workload</c>.
    ///
    /// Las vistas guardadas recuerdan con qué vista se crearon en su <c>StateJson</c>
    /// (<c>"viewType":"carga"</c>). Sin reescribirlo, una vista guardada sobre la carga de trabajo se
    /// abriría en blanco: la pantalla ya no conoce ese valor.
    ///
    /// Sólo se cambia el valor detrás de su clave, así que un nombre o un filtro que contenga la
    /// palabra «carga» se queda como estaba.
    /// </summary>
    public partial class WorkloadViewTypeToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE `SavedViews` SET `StateJson` = REPLACE(`StateJson`, '\"viewType\":\"carga\"', '\"viewType\":\"workload\"') "
                + "WHERE `StateJson` LIKE '%\"viewType\":\"carga\"%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE `SavedViews` SET `StateJson` = REPLACE(`StateJson`, '\"viewType\":\"workload\"', '\"viewType\":\"carga\"') "
                + "WHERE `StateJson` LIKE '%\"viewType\":\"workload\"%';");
        }
    }
}
