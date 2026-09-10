using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Calendar.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Quita los eventos que el sembrador duplicó mientras el filtro de inquilino le escondía lo
    /// que ya había sembrado.
    ///
    /// Es el mismo fallo que dejó 695 usuarios, con la misma causa: al arrancar no hay petición,
    /// el inquilino vale <c>Guid.Empty</c>, la comprobación «¿ya hay eventos?» decía que no y los
    /// cuatro de demostración se creaban otra vez. La base de desarrollo acabó con 184 eventos y
    /// cuatro títulos: cuarenta y seis «Despliegue a Producción v2.1» el mismo día y a la misma
    /// hora.
    ///
    /// El sembrador ya no los crea —abre <c>ComoInquilino</c> antes de mirar—, pero los que hay se
    /// quedan, y un calendario con cuarenta y seis copias de la misma reunión es ilegible.
    ///
    /// <b>Aquí no hay índice único que poner.</b> A diferencia del correo, dos eventos idénticos
    /// pueden ser legítimos: dos reuniones distintas pueden llamarse igual y empezar a la vez en
    /// salas distintas. Esto limpia lo que un fallo concreto generó; no impone una regla que el
    /// calendario no tiene.
    /// </summary>
    public partial class LimpiarEventosDuplicados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se queda el más antiguo de cada (inquilino, título, inicio, fin): es el que se creó
            // en la primera pasada, y por tanto al que apunta cualquier cosa que se hubiera
            // enlazado.
            //
            // **El organizador no entra en la comparación, y eso hay que explicarlo.** La primera
            // versión de esta migración lo incluía —dos eventos homónimos de personas distintas sí
            // son dos eventos— y no borró ni una fila: cada copia tenía un organizador distinto,
            // porque cada arranque creaba también un administrador nuevo y le colgaba los eventos.
            // Los dos fallos eran el mismo. Aquellos organizadores ya no existen —los quitó la
            // limpieza de usuarios— y el que sobrevive es justamente el de la copia más antigua,
            // que es la que se conserva aquí.
            migrationBuilder.Sql(@"
                DELETE `e` FROM `calendar_events` `e`
                  JOIN `calendar_events` `otro`
                    ON `otro`.`tenant_id` = `e`.`tenant_id`
                   AND `otro`.`title` = `e`.`title`
                   AND `otro`.`start_time` = `e`.`start_time`
                   AND `otro`.`end_time` = `e`.`end_time`
                   AND (`otro`.`created_at` < `e`.`created_at`
                        OR (`otro`.`created_at` = `e`.`created_at` AND `otro`.`id` < `e`.`id`));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Las copias no vuelven, y no deben: eran el resultado de un fallo. Deshacer esta
            // migración no tiene nada que rehacer.
        }
    }
}
