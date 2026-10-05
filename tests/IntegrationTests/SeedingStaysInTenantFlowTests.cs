using ApiHost.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que sembrar la demostración no toque los datos de otras organizaciones.
///
/// <b>Nace de un fallo medido.</b> Cada sembrador empezaba con
/// <c>UPDATE … SET TenantId = &lt;demo&gt; WHERE TenantId != &lt;demo&gt;</c>, y la siembra corre en
/// <b>cada arranque, en cualquier entorno</b>. Así que cada vez que la API arrancaba, los usuarios,
/// proyectos, tareas, tickets, documentos y eventos de <b>todas</b> las organizaciones pasaban a la
/// de demostración. En la base de desarrollo, una organización se había quedado sin un solo
/// usuario ni tarea; sólo le quedaban los campos personalizados, cuyo sembrador no tenía ese
/// <c>UPDATE</c>.
///
/// Se copia una fila existente de cada tabla con un inquilino ajeno, se siembra, y se mira que
/// siga siendo suya. Se copia en vez de construirla para no depender de las columnas de cada
/// tabla: lo único que importa aquí es el inquilino.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SeedingStaysInTenantFlowTests(CrmApiFactory factory)
{
    /// <summary>
    /// Las tablas que tocaban los sembradores, con su columna de inquilino y lo que haya que
    /// cambiar además para no chocar con un índice único al copiar.
    /// </summary>
    private static readonly (string Table, string TenantColumn, string Extra)[] Tables =
    [
        ("User", "TenantId", ", `Email` = CONCAT('ajeno-', `Id`, '@ejemplo.com')"),
        ("EntityPermissions", "TenantId", ""),
        ("SavedViews", "TenantId", ""),
        ("Teams", "TenantId", ""),
        ("Spaces", "TenantId", ""),
        ("Folders", "TenantId", ""),
        ("Projects", "TenantId", ""),
        ("Tasks", "TenantId", ""),
        ("Tickets", "TenantId", ""),
        ("Tags", "TenantId", ""),
        ("Documents", "TenantId", ""),
        ("Notifications", "TenantId", ""),
        ("calendar_events", "tenant_id", ""),
        ("Conversations", "TenantId", ""),
        ("Messages", "TenantId", ""),
        ("webhook_subscriptions", "TenantId", ""),
    ];

    [Fact]
    public async Task Seeding_does_not_take_another_organisation_data()
    {
        // El host arranca —migra y siembra— la primera vez que se le pide algo. Sin esto, la base
        // estaría vacía al copiar.
        _ = factory.Services;

        var foreign = Guid.NewGuid().ToString();
        await using var connection = new MySqlConnection(factory.ConnectionString);
        await connection.OpenAsync();

        var copied = new List<(string Table, string TenantColumn, string Id)>();

        try
        {
            foreach (var (table, tenantColumn, extra) in Tables)
            {
                var id = await CopyWithOtherTenantAsync(connection, table, tenantColumn, extra, foreign);
                if (id is not null) copied.Add((table, tenantColumn, id));
            }

            copied.Should().HaveCountGreaterThan(8,
                "la API siembra al arrancar; si casi todas las tablas están vacías esto no comprueba nada");

            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<DataSeederService>().SeedAllAsync();

            foreach (var (table, tenantColumn, id) in copied)
            {
                var now = await ScalarAsync(connection,
                    $"SELECT `{tenantColumn}` FROM `{table}` WHERE `Id` = @id", ("@id", id));

                now.Should().Be(foreign,
                    $"la fila de {table} es de otra organización; sembrar la demostración no puede quedársela");
            }
        }
        finally
        {
            foreach (var (table, _, id) in copied)
                await ExecuteAsync(connection, $"DELETE FROM `{table}` WHERE `Id` = @id", ("@id", id));
        }
    }

    /// <summary>
    /// Lo que esos <c>UPDATE</c> querían hacer, y sigue haciéndose: una fila que se quedó con el
    /// inquilino vacío —de cuando el inquilino llegaba siempre vacío— pasa a la demostración, en
    /// vez de quedarse invisible para siempre.
    /// </summary>
    [Fact]
    public async Task Seeding_adopts_rows_without_tenant()
    {
        _ = factory.Services;

        await using var connection = new MySqlConnection(factory.ConnectionString);
        await connection.OpenAsync();

        var demo = await ScalarAsync(connection, "SELECT `TenantId` FROM `User` WHERE `Email` = 'admin@acme.com'");
        var id = await CopyWithOtherTenantAsync(
            connection, "Tags", "TenantId", ", `Name` = CONCAT('sin-inquilino-', `Id`)", Guid.Empty.ToString());
        id.Should().NotBeNull("el sembrador crea etiquetas");

        try
        {
            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<DataSeederService>().SeedAllAsync();

            (await ScalarAsync(connection, "SELECT `TenantId` FROM `Tags` WHERE `Id` = @id", ("@id", id!)))
                .Should().Be(demo, "una fila sin inquilino no la ve nadie; la siembra se la da a la demostración");
        }
        finally
        {
            await ExecuteAsync(connection, "DELETE FROM `Tags` WHERE `Id` = @id", ("@id", id!));
        }
    }

    /// <summary>Copia una fila cualquiera de la tabla con otro inquilino. Nulo si está vacía.</summary>
    private static async Task<string?> CopyWithOtherTenantAsync(
        MySqlConnection connection, string table, string tenantColumn, string extra, string foreign)
    {
        var newValue = Guid.NewGuid().ToString();

        await ExecuteAsync(connection, "DROP TEMPORARY TABLE IF EXISTS copia");
        await ExecuteAsync(connection, $"CREATE TEMPORARY TABLE copia AS SELECT * FROM `{table}` LIMIT 1");

        if (await ScalarAsync(connection, "SELECT COUNT(*) FROM copia") == "0")
            return null;

        await ExecuteAsync(connection, $"UPDATE copia SET `Id` = @nuevo, `{tenantColumn}` = @ajeno {extra}",
            ("@nuevo", newValue), ("@ajeno", foreign));
        await ExecuteAsync(connection, $"INSERT INTO `{table}` SELECT * FROM copia");
        await ExecuteAsync(connection, "DROP TEMPORARY TABLE copia");

        return newValue;
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql, params (string, object)[] parameters)
    {
        await using var command = new MySqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarAsync(MySqlConnection connection, string sql, params (string, object)[] parameters)
    {
        await using var command = new MySqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (await command.ExecuteScalarAsync())?.ToString();
    }
}
