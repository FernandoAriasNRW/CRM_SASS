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
public sealed class SiembraSinCruzarInquilinosFlowTests(CrmApiFactory factory)
{
    /// <summary>
    /// Las tablas que tocaban los sembradores, con su columna de inquilino y lo que haya que
    /// cambiar además para no chocar con un índice único al copiar.
    /// </summary>
    private static readonly (string Tabla, string Inquilino, string Extra)[] Tablas =
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
    public async Task Sembrar_no_se_lleva_los_datos_de_otra_organizacion()
    {
        // El host arranca —migra y siembra— la primera vez que se le pide algo. Sin esto, la base
        // estaría vacía al copiar.
        _ = factory.Services;

        var ajeno = Guid.NewGuid().ToString();
        await using var conexion = new MySqlConnection(factory.ConnectionString);
        await conexion.OpenAsync();

        var copiadas = new List<(string Tabla, string Inquilino, string Id)>();

        try
        {
            foreach (var (tabla, inquilino, extra) in Tablas)
            {
                var id = await CopiarConOtroInquilinoAsync(conexion, tabla, inquilino, extra, ajeno);
                if (id is not null) copiadas.Add((tabla, inquilino, id));
            }

            copiadas.Should().HaveCountGreaterThan(8,
                "la API siembra al arrancar; si casi todas las tablas están vacías esto no comprueba nada");

            using (var ambito = factory.Services.CreateScope())
                await ambito.ServiceProvider.GetRequiredService<DataSeederService>().SeedAllAsync();

            foreach (var (tabla, inquilino, id) in copiadas)
            {
                var ahora = await EscalarAsync(conexion,
                    $"SELECT `{inquilino}` FROM `{tabla}` WHERE `Id` = @id", ("@id", id));

                ahora.Should().Be(ajeno,
                    $"la fila de {tabla} es de otra organización; sembrar la demostración no puede quedársela");
            }
        }
        finally
        {
            foreach (var (tabla, _, id) in copiadas)
                await EjecutarAsync(conexion, $"DELETE FROM `{tabla}` WHERE `Id` = @id", ("@id", id));
        }
    }

    /// <summary>
    /// Lo que esos <c>UPDATE</c> querían hacer, y sigue haciéndose: una fila que se quedó con el
    /// inquilino vacío —de cuando el inquilino llegaba siempre vacío— pasa a la demostración, en
    /// vez de quedarse invisible para siempre.
    /// </summary>
    [Fact]
    public async Task Sembrar_adopta_las_filas_sin_inquilino()
    {
        _ = factory.Services;

        await using var conexion = new MySqlConnection(factory.ConnectionString);
        await conexion.OpenAsync();

        var demo = await EscalarAsync(conexion, "SELECT `TenantId` FROM `User` WHERE `Email` = 'admin@acme.com'");
        var id = await CopiarConOtroInquilinoAsync(
            conexion, "Tags", "TenantId", ", `Name` = CONCAT('sin-inquilino-', `Id`)", Guid.Empty.ToString());
        id.Should().NotBeNull("el sembrador crea etiquetas");

        try
        {
            using (var ambito = factory.Services.CreateScope())
                await ambito.ServiceProvider.GetRequiredService<DataSeederService>().SeedAllAsync();

            (await EscalarAsync(conexion, "SELECT `TenantId` FROM `Tags` WHERE `Id` = @id", ("@id", id!)))
                .Should().Be(demo, "una fila sin inquilino no la ve nadie; la siembra se la da a la demostración");
        }
        finally
        {
            await EjecutarAsync(conexion, "DELETE FROM `Tags` WHERE `Id` = @id", ("@id", id!));
        }
    }

    /// <summary>Copia una fila cualquiera de la tabla con otro inquilino. Nulo si está vacía.</summary>
    private static async Task<string?> CopiarConOtroInquilinoAsync(
        MySqlConnection conexion, string tabla, string inquilino, string extra, string ajeno)
    {
        var nuevo = Guid.NewGuid().ToString();

        await EjecutarAsync(conexion, "DROP TEMPORARY TABLE IF EXISTS copia");
        await EjecutarAsync(conexion, $"CREATE TEMPORARY TABLE copia AS SELECT * FROM `{tabla}` LIMIT 1");

        if (await EscalarAsync(conexion, "SELECT COUNT(*) FROM copia") == "0")
            return null;

        await EjecutarAsync(conexion, $"UPDATE copia SET `Id` = @nuevo, `{inquilino}` = @ajeno {extra}",
            ("@nuevo", nuevo), ("@ajeno", ajeno));
        await EjecutarAsync(conexion, $"INSERT INTO `{tabla}` SELECT * FROM copia");
        await EjecutarAsync(conexion, "DROP TEMPORARY TABLE copia");

        return nuevo;
    }

    private static async Task EjecutarAsync(MySqlConnection conexion, string sql, params (string, object)[] parametros)
    {
        await using var comando = new MySqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<string?> EscalarAsync(MySqlConnection conexion, string sql, params (string, object)[] parametros)
    {
        await using var comando = new MySqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor);
        return (await comando.ExecuteScalarAsync())?.ToString();
    }
}
