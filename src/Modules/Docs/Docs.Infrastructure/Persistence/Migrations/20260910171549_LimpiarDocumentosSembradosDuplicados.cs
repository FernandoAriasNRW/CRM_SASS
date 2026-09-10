using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Docs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Quita las copias de los tres documentos de demostración que el sembrador repitió en cada
    /// arranque.
    ///
    /// Misma causa que los 695 usuarios y los 184 eventos: sin petición HTTP el inquilino vale
    /// <c>Guid.Empty</c>, la comprobación «ya hay documentos?» respondía que no y los tres se
    /// creaban otra vez. La base de desarrollo acabó con cuarenta y cinco copias de cada uno, y
    /// la galería de «Mis plantillas» era una cuadrícula interminable de la misma tarjeta.
    ///
    /// El sembrador ya abre <c>ComoInquilino</c> antes de mirar, así que no crea más; esto limpia
    /// lo que quedó.
    ///
    /// <b>Sólo toca los tres títulos que siembra el sembrador, y sólo en sus tipos.</b> Un
    /// documento creado a partir de la plantilla se llama igual que ella, y borrar por título a
    /// secas se llevaría por delante el trabajo de alguien. Tampoco se pone un índice único: dos
    /// documentos con el mismo título son perfectamente legítimos.
    /// </summary>
    public partial class LimpiarDocumentosSembradosDuplicados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se queda la copia más antigua de cada (inquilino, título, tipo): la de la primera
            // pasada, a la que apunta cualquier cosa que se hubiera enlazado.
            const string copias = @"
                SELECT `d`.`Id`
                  FROM `Documents` `d`
                  JOIN `Documents` `otro`
                    ON `otro`.`TenantId` = `d`.`TenantId`
                   AND `otro`.`Title` = `d`.`Title`
                   AND `otro`.`Type` = `d`.`Type`
                   AND (`otro`.`CreatedAtUtc` < `d`.`CreatedAtUtc`
                        OR (`otro`.`CreatedAtUtc` = `d`.`CreatedAtUtc` AND `otro`.`Id` < `d`.`Id`))
                 WHERE `d`.`Type` IN (2, 3, 4)
                   AND `d`.`Title` IN (
                         'Arquitectura del Sistema CRM SaaS Suite',
                         'Plantilla: Especificación de Producto (PRD)',
                         'Minuta de Reunión: Planificación Sprint Q3')";

            // MySQL no deja leer de la misma tabla que se está borrando dentro de una subconsulta,
            // de ahí la tabla temporal en vez de un `DELETE ... WHERE Id IN (SELECT ...)`.
            migrationBuilder.Sql($@"CREATE TEMPORARY TABLE `docs_duplicados` AS {copias};");

            // Las hijas primero y por identificador, no por la clave ajena: si algún día la
            // relación dejara de borrar en cascada, esto seguiría dejando la base limpia.
            migrationBuilder.Sql(@"
                DELETE FROM `Pages`
                 WHERE `DocumentId` IN (SELECT `Id` FROM `docs_duplicados`);");

            migrationBuilder.Sql(@"
                DELETE FROM `DocumentPermissions`
                 WHERE `DocumentId` IN (SELECT `Id` FROM `docs_duplicados`);");

            migrationBuilder.Sql(@"
                DELETE FROM `Documents`
                 WHERE `Id` IN (SELECT `Id` FROM `docs_duplicados`);");

            migrationBuilder.Sql(@"DROP TEMPORARY TABLE `docs_duplicados`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Las copias no vuelven: eran el resultado de un fallo, no un dato.
        }
    }
}
