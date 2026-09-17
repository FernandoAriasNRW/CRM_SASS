namespace ApiHost.Seeding;

/// <summary>
/// Los datos de demostración de un módulo.
///
/// Cada sembrador es idempotente: comprueba lo que ya hay y sólo añade lo que falta, así que
/// sembrar dos veces deja la base igual que sembrar una.
/// </summary>
public interface IModuleSeeder
{
    /// <summary>El nombre con el que aparece en el registro y en la lista de fallos.</summary>
    string Module { get; }

    /// <summary>Menor primero. Identity va el primero: fija el inquilino del resto.</summary>
    int Order { get; }

    Task SeedAsync(SeedContext context, CancellationToken cancellationToken);
}
