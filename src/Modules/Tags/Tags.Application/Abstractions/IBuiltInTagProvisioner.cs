namespace Tags.Application.Abstractions;

/// <summary>
/// Da a una organización las etiquetas predefinidas que le falten (<c>BuiltIn.BuiltInTags</c>).
///
/// Idempotente: busca por clave, así que se puede llamar en cada arranque. Corre fuera de una
/// petición, donde el filtro de inquilino no tiene con quién comparar; la implementación tiene que
/// fijar el inquilino ella misma.
/// </summary>
public interface IBuiltInTagProvisioner
{
    /// <returns>Cuántas etiquetas ha creado.</returns>
    Task<int> ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
