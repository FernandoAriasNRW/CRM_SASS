using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkItems.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Tareas en inglés: las columnas de la recurrencia y las de la checklist.
    ///
    /// <b>Corregida a mano.</b> EF emparejó mal dos columnas: proponía
    /// <c>Recurrence_Intervalo → Recurrence_SeriesDay</c> y
    /// <c>Recurrence_DiaDeLaSerie → Recurrence_Interval</c>. Las dos son <c>int</c>, así que el
    /// emparejamiento por tipo las cruzó, y eso habría **intercambiado el intervalo con el día de
    /// la serie** en todas las tareas recurrentes: una que se repite cada 2 meses empezando el día
    /// 31 habría pasado a repetirse cada 31 meses el día 2, sin ningún error que lo delatara.
    ///
    /// También cambia el valor guardado de la frecuencia: «Diaria», «Semanal» y «Mensual» pasan a
    /// «Daily», «Weekly» y «Monthly». Sin el <c>UPDATE</c>, el generador no reconocería la
    /// frecuencia de las series que ya existen y <c>RecurrenceCalendar.Next</c> lanzaría al
    /// calcular la siguiente fecha, dejando de crear las tareas de esas series.
    /// </summary>
    public partial class RenameWorkItemsToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Recurrencia. Cada columna a la suya: ver la nota de arriba.
            migrationBuilder.RenameColumn(
                name: "Recurrence_Frecuencia", table: "Tasks", newName: "Recurrence_Frequency");
            migrationBuilder.RenameColumn(
                name: "Recurrence_Intervalo", table: "Tasks", newName: "Recurrence_Interval");
            migrationBuilder.RenameColumn(
                name: "Recurrence_ProximaOcurrencia", table: "Tasks", newName: "Recurrence_NextOccurrence");
            migrationBuilder.RenameColumn(
                name: "Recurrence_FechaFin", table: "Tasks", newName: "Recurrence_EndDate");
            migrationBuilder.RenameColumn(
                name: "Recurrence_DiaDeLaSerie", table: "Tasks", newName: "Recurrence_SeriesDay");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_Recurrence_ProximaOcurrencia",
                table: "Tasks",
                newName: "IX_Tasks_Recurrence_NextOccurrence");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_TenantId_Archivado_Borrado",
                table: "Tasks",
                newName: "IX_Tasks_TenantId_ArchivedAtUtc_IsDeleted");

            migrationBuilder.Sql("UPDATE Tasks SET Recurrence_Frequency = 'Daily' WHERE Recurrence_Frequency = 'Diaria'");
            migrationBuilder.Sql("UPDATE Tasks SET Recurrence_Frequency = 'Weekly' WHERE Recurrence_Frequency = 'Semanal'");
            migrationBuilder.Sql("UPDATE Tasks SET Recurrence_Frequency = 'Monthly' WHERE Recurrence_Frequency = 'Mensual'");

            // --- Checklist.
            migrationBuilder.RenameColumn(name: "Texto", table: "TaskChecklistItems", newName: "Text");
            migrationBuilder.RenameColumn(name: "Hecho", table: "TaskChecklistItems", newName: "IsDone");
            migrationBuilder.RenameColumn(name: "Posicion", table: "TaskChecklistItems", newName: "Position");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: "Position", table: "TaskChecklistItems", newName: "Posicion");
            migrationBuilder.RenameColumn(name: "IsDone", table: "TaskChecklistItems", newName: "Hecho");
            migrationBuilder.RenameColumn(name: "Text", table: "TaskChecklistItems", newName: "Texto");

            migrationBuilder.Sql("UPDATE Tasks SET Recurrence_Frequency = 'Diaria' WHERE Recurrence_Frequency = 'Daily'");
            migrationBuilder.Sql("UPDATE Tasks SET Recurrence_Frequency = 'Semanal' WHERE Recurrence_Frequency = 'Weekly'");
            migrationBuilder.Sql("UPDATE Tasks SET Recurrence_Frequency = 'Mensual' WHERE Recurrence_Frequency = 'Monthly'");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_TenantId_ArchivedAtUtc_IsDeleted",
                table: "Tasks",
                newName: "IX_Tasks_TenantId_Archivado_Borrado");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_Recurrence_NextOccurrence",
                table: "Tasks",
                newName: "IX_Tasks_Recurrence_ProximaOcurrencia");

            migrationBuilder.RenameColumn(
                name: "Recurrence_SeriesDay", table: "Tasks", newName: "Recurrence_DiaDeLaSerie");
            migrationBuilder.RenameColumn(
                name: "Recurrence_EndDate", table: "Tasks", newName: "Recurrence_FechaFin");
            migrationBuilder.RenameColumn(
                name: "Recurrence_NextOccurrence", table: "Tasks", newName: "Recurrence_ProximaOcurrencia");
            migrationBuilder.RenameColumn(
                name: "Recurrence_Interval", table: "Tasks", newName: "Recurrence_Intervalo");
            migrationBuilder.RenameColumn(
                name: "Recurrence_Frequency", table: "Tasks", newName: "Recurrence_Frecuencia");
        }
    }
}
