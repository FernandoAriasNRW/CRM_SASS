using Microsoft.EntityFrameworkCore;

namespace ApiHost.Seeding;

/// <summary>
/// Las filas que se quedaron sin inquilino, y sólo ésas.
///
/// Hubo un tiempo en que el inquilino llegaba siempre vacío y se guardaron filas con
/// <see cref="Guid.Empty"/>, que el filtro global no vuelve a ver nunca. Los sembradores las
/// «adoptan» para la organización de demostración.
///
/// <b>Estaba escrito como <c>WHERE TenantId != &lt;demo&gt;</c></b> en once sembradores, y la siembra
/// corre en cada arranque y en cualquier entorno: cada vez que la API arrancaba, los usuarios,
/// proyectos, tareas, tickets, documentos y eventos de <b>todas</b> las organizaciones pasaban a la
/// de demostración. Por eso vive aquí, una sola vez, y la condición es la única que se corresponde
/// con «huérfana»: el inquilino vacío. Lo vigila <c>SiembraSinCruzarInquilinosFlowTests</c>.
/// </summary>
public static class OrphanRows
{
    /// <summary>
    /// Pasa a <paramref name="tenantId"/> las filas de <paramref name="table"/> sin inquilino.
    /// La tabla y la columna son constantes del sembrador, nunca datos de fuera: por eso pueden ir
    /// en el texto de la consulta.
    /// </summary>
    public static Task AdoptAsync(
        DbContext db, string table, string tenantColumn, Guid tenantId, CancellationToken ct)
        => db.Database.ExecuteSqlRawAsync(
            $"UPDATE `{table}` SET `{tenantColumn}` = {{0}} WHERE `{tenantColumn}` = {{1}}",
            [tenantId, Guid.Empty], ct);
}
