using BuildingBlocks.Application.Abstractions;
using Docs.Domain.Menciones;

namespace Docs.Application.Menciones;

/// <summary>
/// Guarda las menciones de una página a partir de su contenido.
///
/// <b>Se reescriben enteras cada vez que se guarda la página</b>, en vez de ir añadiendo y
/// quitando. Es lo que hace que borrar una mención del texto la borre de verdad: con un
/// diferencial habría que detectar la desaparición, y lo que no se detecta se queda para siempre
/// —una tarea enseñando un documento que ya no habla de ella—.
///
/// El coste es un borrado y una inserción por guardado, sobre una tabla indexada por página y con
/// pocas filas por página. A cambio, el estado no puede divergir del texto.
/// </summary>
public interface IRepositorioDeMenciones
{
    /// <summary>Reemplaza las menciones de una página por las que trae el contenido.</summary>
    Task ReescribirAsync(
        Guid tenantId, Guid documentId, Guid pageId,
        IReadOnlyList<MencionEnDocumento> menciones,
        CancellationToken ct = default);

    /// <summary>Los documentos que mencionan una entidad. Es lo que responde el puerto.</summary>
    Task<IReadOnlyList<MentioningDocument>> GetMentioningDocumentsAsync(
        Guid tenantId, string tipo, Guid entityId, CancellationToken ct = default);

    /// <summary>Lo que menciona una página, para pintarlo dentro del propio documento.</summary>
    Task<IReadOnlyList<MencionEnDocumento>> DeLaPaginaAsync(
        Guid tenantId, Guid pageId, CancellationToken ct = default);
}

/// <summary>
/// Actualiza las menciones de una página cuando su contenido cambia.
///
/// Vive en un servicio y no dentro del handler de guardar para que se pueda llamar desde los dos
/// sitios que escriben contenido —crear página y actualizarla— sin repetir la lógica en ninguno.
/// </summary>
public sealed class ActualizadorDeMenciones(IRepositorioDeMenciones repositorio)
{
    public async Task ActualizarAsync(
        Guid tenantId, Guid documentId, Guid pageId, string? contenido, CancellationToken ct = default)
    {
        var encontradas = LectorDeMenciones.Leer(contenido);

        var menciones = encontradas
            .Select(m => MencionEnDocumento.Crear(
                tenantId, documentId, pageId, m.Tipo, m.EntidadId, m.VisibleText))
            .ToList();

        // Se llama siempre, aunque no haya ninguna: una página de la que se han borrado todas las
        // menciones tiene que quedarse sin ellas, y saltarse la llamada dejaría las viejas.
        await repositorio.ReescribirAsync(tenantId, documentId, pageId, menciones, ct);
    }
}
