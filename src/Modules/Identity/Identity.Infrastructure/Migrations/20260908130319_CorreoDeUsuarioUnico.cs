using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Migrations
{
    /// <summary>
    /// El correo de usuario pasa a ser único, y se limpian los duplicados que ya había.
    ///
    /// <b>Por qué hace falta.</b> El sembrador buscaba al administrador con el filtro de inquilino
    /// puesto, y en ese momento el inquilino todavía no se conoce —sale del propio administrador—,
    /// así que la búsqueda no encontraba nada y lo creaba otra vez. En cada arranque. La base de
    /// desarrollo acabó con 695 filas y 11 correos distintos, 115 de ellas «admin@acme.com».
    ///
    /// Y no era sólo desorden: el inicio de sesión busca por correo y se queda con una fila
    /// cualquiera, de modo que quien entraba no era el administrador que posee los proyectos.
    /// «Mis proyectos» enseñaba 0 teniendo cinco.
    ///
    /// El sembrador ya está arreglado, pero **el arreglo del código no impide que vuelva a pasar
    /// por otro camino** —dos instancias arrancando a la vez, un alta manual, una importación—.
    /// Lo que lo impide es el índice.
    /// </summary>
    public partial class CorreoDeUsuarioUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // `varchar(320)` en vez de `longtext`: MySQL no indexa un `longtext` sin decirle
            // cuántos caracteres mirar, y un índice por prefijo daría por iguales dos correos que
            // sólo coinciden al principio. 320 es el máximo de la norma —64 de buzón, arroba, 255
            // de dominio—, así que no recorta ningún correo válido.
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "User",
                type: "varchar(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext");

            // Quién sobrevive de cada correo repetido: **el más antiguo**.
            //
            // No es indiferente. Los proyectos, las tareas y los tickets se sembraron en la
            // primera pasada y apuntan a los usuarios de esa pasada; quedarse con el último
            // dejaría cinco proyectos con un propietario que ya no existe. Se desempata por `Id`
            // para que dos filas creadas en el mismo instante no dependan del orden que devuelva
            // el motor.
            migrationBuilder.Sql(@"
                CREATE TABLE `_UsuariosDuplicados` AS
                SELECT `Id` AS `Duplicado`, `Superviviente` FROM (
                    SELECT `Id`,
                           FIRST_VALUE(`Id`) OVER (
                               PARTITION BY `Email` ORDER BY `CreatedAtUtc`, `Id`
                           ) AS `Superviviente`
                    FROM `User`
                ) `t`
                WHERE `Id` <> `Superviviente`;");

            // Lo que colgaba de los duplicados se pasa al superviviente en lugar de borrarse: son
            // las vistas guardadas, los favoritos y los permisos de la persona, y para ella son la
            // misma cuenta —nunca supo que había 115 filas—.
            migrationBuilder.Sql(@"
                UPDATE `SavedViews` `v`
                  JOIN `_UsuariosDuplicados` `d` ON `v`.`UserId` = `d`.`Duplicado`
                   SET `v`.`UserId` = `d`.`Superviviente`;");

            migrationBuilder.Sql(@"
                UPDATE `Favoritos` `f`
                  JOIN `_UsuariosDuplicados` `d` ON `f`.`UserId` = `d`.`Duplicado`
                   SET `f`.`UserId` = `d`.`Superviviente`;");

            migrationBuilder.Sql(@"
                UPDATE `EntityPermissions` `p`
                  JOIN `_UsuariosDuplicados` `d` ON `p`.`UserId` = `d`.`Duplicado`
                   SET `p`.`UserId` = `d`.`Superviviente`;");

            // Y ahora las copias que ese traslado ha juntado. Cada duplicado traía su propia copia
            // de las mismas vistas sembradas: sin esto, el administrador se encontraría la barra
            // de vistas con trescientos botones iguales, que es un estropicio distinto pero
            // igual de visible.
            migrationBuilder.Sql(@"
                DELETE `v` FROM `SavedViews` `v`
                  JOIN `SavedViews` `otra`
                    ON `otra`.`UserId` = `v`.`UserId`
                   AND `otra`.`ModuleName` = `v`.`ModuleName`
                   AND `otra`.`ViewName` = `v`.`ViewName`
                   AND `otra`.`Id` < `v`.`Id`;");

            migrationBuilder.Sql(@"
                DELETE `f` FROM `Favoritos` `f`
                  JOIN `Favoritos` `otro`
                    ON `otro`.`UserId` = `f`.`UserId`
                   AND `otro`.`Tipo` = `f`.`Tipo`
                   AND `otro`.`EntityId` = `f`.`EntityId`
                   AND `otro`.`Id` < `f`.`Id`;");

            migrationBuilder.Sql(@"
                DELETE `p` FROM `EntityPermissions` `p`
                  JOIN `EntityPermissions` `otro`
                    ON `otro`.`UserId` <=> `p`.`UserId`
                   AND `otro`.`EntityType` = `p`.`EntityType`
                   AND `otro`.`EntityId` = `p`.`EntityId`
                   AND `otro`.`TargetType` = `p`.`TargetType`
                   AND `otro`.`TeamId` <=> `p`.`TeamId`
                   AND `otro`.`RoleName` <=> `p`.`RoleName`
                   AND `otro`.`Id` < `p`.`Id`;");

            migrationBuilder.Sql(@"
                DELETE `u` FROM `User` `u`
                  JOIN `_UsuariosDuplicados` `d` ON `u`.`Id` = `d`.`Duplicado`;");

            migrationBuilder.Sql("DROP TABLE `_UsuariosDuplicados`;");

            // El índice va aquí y no en `UserConfiguration` porque `Email` es un tipo complejo:
            // `HasIndex(\"Email\")` intenta crear una propiedad sombra con ese nombre y EF lo
            // rechaza. La restricción vive donde de verdad manda, que es la base.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX `IX_User_Email_Unico` ON `User` (`Email`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Los usuarios duplicados que se borraron no vuelven, y no deben: eran copias de la
            // misma persona creadas por un fallo. Deshacer esto devuelve la columna y quita la
            // restricción; no reconstruye la basura.
            migrationBuilder.Sql("DROP INDEX `IX_User_Email_Unico` ON `User`;");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "User",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(320)",
                oldMaxLength: 320);
        }
    }
}
