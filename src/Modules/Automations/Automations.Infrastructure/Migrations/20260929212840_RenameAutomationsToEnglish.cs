using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automations.Infrastructure.Migrations
{
    /// <summary>
    /// Automations en inglés: columnas, índices y el vocabulario guardado de las reglas.
    ///
    /// Las reglas guardan su disparador, los campos y operadores de sus condiciones y los tipos de
    /// sus acciones como texto, en español («TareaCreada», «Estado», «Igual», «CambiarPrioridad»…),
    /// y el motor los compara con las constantes del código. Sin reescribirlos, ninguna regla
    /// existente volvería a saltar. El historial guarda el resultado de cada ejecución igual.
    ///
    /// <b>No se tocan los valores que escribe el usuario</b> (el «Done» de una condición, a quién
    /// se asigna), salvo el destinatario especial de «Notificar», que es una palabra del sistema.
    /// </summary>
    public partial class RenameAutomationsToEnglish : Migration
    {
        /// <summary>(tabla, columna, español, inglés). Escrito aquí para que la migración haga siempre lo mismo.</summary>
        private static readonly (string Table, string Column, string Spanish, string English)[] StoredValues =
        [
            ("AutomationRules", "Trigger", "TareaCreada", "TaskCreated"),
            ("AutomationRules", "Trigger", "TareaCambiaDeEstado", "TaskStatusChanged"),
            ("AutomationRules", "Trigger", "TareaCambiaDePrioridad", "TaskPriorityChanged"),
            ("AutomationRules", "Trigger", "TareaPorVencer", "TaskDueSoon"),

            ("AutomationConditions", "Field", "Estado", "Status"),
            ("AutomationConditions", "Field", "EstadoAnterior", "PreviousStatus"),
            ("AutomationConditions", "Field", "Prioridad", "Priority"),
            ("AutomationConditions", "Field", "PrioridadAnterior", "PreviousPriority"),
            ("AutomationConditions", "Field", "ProyectoId", "ProjectId"),
            ("AutomationConditions", "Field", "ResponsableId", "AssigneeId"),
            ("AutomationConditions", "Field", "DiasParaVencer", "DaysUntilDue"),
            ("AutomationConditions", "Field", "Titulo", "Title"),

            ("AutomationConditions", "Operator", "Igual", "EqualTo"),
            ("AutomationConditions", "Operator", "Distinto", "NotEqualTo"),
            ("AutomationConditions", "Operator", "Contiene", "Contains"),
            ("AutomationConditions", "Operator", "EstaVacio", "IsEmpty"),
            ("AutomationConditions", "Operator", "MenorOIgual", "LessOrEqual"),
            ("AutomationConditions", "Operator", "MayorOIgual", "GreaterOrEqual"),

            ("AutomationActions", "Type", "CambiarEstado", "ChangeStatus"),
            ("AutomationActions", "Type", "CambiarPrioridad", "ChangePriority"),
            ("AutomationActions", "Type", "AsignarA", "AssignTo"),
            ("AutomationActions", "Type", "Notificar", "Notify"),

            ("AutomationExecutions", "Outcome", "Aplicada", "Applied"),
            ("AutomationExecutions", "Outcome", "NoCumplioCondiciones", "ConditionsNotMet"),
            ("AutomationExecutions", "Outcome", "Fallida", "Failed"),
        ];

        private static void ChangeStoredValues(MigrationBuilder migrationBuilder, bool toEnglish)
        {
            foreach (var (table, column, spanish, english) in StoredValues)
            {
                var (from, to) = toEnglish ? (spanish, english) : (english, spanish);
                migrationBuilder.Sql($"UPDATE `{table}` SET `{column}` = '{to}' WHERE `{column}` = '{from}';");
            }

            // El destinatario especial de «Notificar»: sólo en esas acciones, porque en «Asignar a»
            // el valor es una persona.
            var notify = toEnglish ? "Notify" : "Notificar";
            var (recipientFrom, recipientTo) = toEnglish ? ("Responsable", "Assignee") : ("Assignee", "Responsable");
            migrationBuilder.Sql(
                $"UPDATE `AutomationActions` SET `Value` = '{recipientTo}' WHERE `Type` = '{notify}' AND `Value` = '{recipientFrom}';");
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VecesEjecutada",
                table: "AutomationRules",
                newName: "ExecutionCount");

            migrationBuilder.RenameColumn(
                name: "UltimaEjecucionUtc",
                table: "AutomationRules",
                newName: "LastExecutedAtUtc");

            migrationBuilder.RenameColumn(
                name: "Nombre",
                table: "AutomationRules",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "Disparador",
                table: "AutomationRules",
                newName: "Trigger");

            migrationBuilder.RenameColumn(
                name: "Activa",
                table: "AutomationRules",
                newName: "IsActive");

            migrationBuilder.RenameIndex(
                name: "UX_AutomationRules_Tenant_Nombre",
                table: "AutomationRules",
                newName: "UX_AutomationRules_Tenant_Name");

            migrationBuilder.RenameIndex(
                name: "IX_AutomationRules_Tenant_Disparador_Activa",
                table: "AutomationRules",
                newName: "IX_AutomationRules_Tenant_Trigger_IsActive");

            migrationBuilder.RenameColumn(
                name: "Resultado",
                table: "AutomationExecutions",
                newName: "Outcome");

            migrationBuilder.RenameColumn(
                name: "Dia",
                table: "AutomationExecutions",
                newName: "Day");

            migrationBuilder.RenameColumn(
                name: "Detalle",
                table: "AutomationExecutions",
                newName: "Detail");

            migrationBuilder.RenameColumn(
                name: "CuandoUtc",
                table: "AutomationExecutions",
                newName: "AtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_AutomationExecutions_Memoria",
                table: "AutomationExecutions",
                newName: "IX_AutomationExecutions_DailyMemory");

            migrationBuilder.RenameIndex(
                name: "IX_AutomationExecutions_Historial",
                table: "AutomationExecutions",
                newName: "IX_AutomationExecutions_History");

            migrationBuilder.RenameColumn(
                name: "Valor",
                table: "AutomationConditions",
                newName: "Value");

            migrationBuilder.RenameColumn(
                name: "Operador",
                table: "AutomationConditions",
                newName: "Operator");

            migrationBuilder.RenameColumn(
                name: "Campo",
                table: "AutomationConditions",
                newName: "Field");

            migrationBuilder.RenameColumn(
                name: "Valor",
                table: "AutomationActions",
                newName: "Value");

            migrationBuilder.RenameColumn(
                name: "Tipo",
                table: "AutomationActions",
                newName: "Type");

            ChangeStoredValues(migrationBuilder, toEnglish: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ChangeStoredValues(migrationBuilder, toEnglish: false);

            migrationBuilder.RenameColumn(
                name: "Trigger",
                table: "AutomationRules",
                newName: "Disparador");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "AutomationRules",
                newName: "Nombre");

            migrationBuilder.RenameColumn(
                name: "LastExecutedAtUtc",
                table: "AutomationRules",
                newName: "UltimaEjecucionUtc");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "AutomationRules",
                newName: "Activa");

            migrationBuilder.RenameColumn(
                name: "ExecutionCount",
                table: "AutomationRules",
                newName: "VecesEjecutada");

            migrationBuilder.RenameIndex(
                name: "UX_AutomationRules_Tenant_Name",
                table: "AutomationRules",
                newName: "UX_AutomationRules_Tenant_Nombre");

            migrationBuilder.RenameIndex(
                name: "IX_AutomationRules_Tenant_Trigger_IsActive",
                table: "AutomationRules",
                newName: "IX_AutomationRules_Tenant_Disparador_Activa");

            migrationBuilder.RenameColumn(
                name: "Outcome",
                table: "AutomationExecutions",
                newName: "Resultado");

            migrationBuilder.RenameColumn(
                name: "Detail",
                table: "AutomationExecutions",
                newName: "Detalle");

            migrationBuilder.RenameColumn(
                name: "Day",
                table: "AutomationExecutions",
                newName: "Dia");

            migrationBuilder.RenameColumn(
                name: "AtUtc",
                table: "AutomationExecutions",
                newName: "CuandoUtc");

            migrationBuilder.RenameIndex(
                name: "IX_AutomationExecutions_History",
                table: "AutomationExecutions",
                newName: "IX_AutomationExecutions_Historial");

            migrationBuilder.RenameIndex(
                name: "IX_AutomationExecutions_DailyMemory",
                table: "AutomationExecutions",
                newName: "IX_AutomationExecutions_Memoria");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "AutomationConditions",
                newName: "Valor");

            migrationBuilder.RenameColumn(
                name: "Operator",
                table: "AutomationConditions",
                newName: "Operador");

            migrationBuilder.RenameColumn(
                name: "Field",
                table: "AutomationConditions",
                newName: "Campo");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "AutomationActions",
                newName: "Valor");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "AutomationActions",
                newName: "Tipo");
        }
    }
}
