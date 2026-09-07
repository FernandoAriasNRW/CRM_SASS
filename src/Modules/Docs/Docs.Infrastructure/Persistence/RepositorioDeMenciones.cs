using BuildingBlocks.Application.Abstractions;
using Docs.Application.Menciones;
using Docs.Domain.Menciones;
using Microsoft.EntityFrameworkCore;

namespace Docs.Infrastructure.Persistence;

public sealed class RepositorioDeMenciones(DocsDbContext contexto) : IRepositorioDeMenciones
{
    public async Task ReescribirAsync(
        Guid tenantId, Guid documentId, Guid pageId,
        IReadOnlyList<MencionEnDocumento> menciones,
        CancellationToken ct = default)
    {
        // Borrar y volver a escribir, no comparar. Ver el porqué en IRepositorioDeMenciones: lo
        // que no se detecta al comparar se queda para siempre, y una tarea acabaría enseñando un
        // documento que ya no habla de ella.
        await contexto.MencionesEnDocumentos
            .Where(m => m.TenantId == tenantId && m.PageId == pageId)
            .ExecuteDeleteAsync(ct);

        if (menciones.Count > 0)
            await contexto.MencionesEnDocumentos.AddRangeAsync(menciones, ct);

        await contexto.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentoQueMenciona>> QuienMencionaAsync(
        Guid tenantId, string tipo, Guid entidadId, CancellationToken ct = default)
    {
        // Se une con la página y el documento para devolver los títulos: quien pregunta va a
        // pintar una lista de enlaces, y sin los títulos tendría que pedir cada uno por separado.
        return await contexto.MencionesEnDocumentos.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.TipoMencionado == tipo && m.EntidadMencionadaId == entidadId)
            .Join(contexto.Pages.AsNoTracking(), m => m.PageId, p => p.Id, (m, p) => new { m, p })
            .Join(contexto.Documents.AsNoTracking(), x => x.m.DocumentId, d => d.Id, (x, d) => new { x.m, x.p, d })
            // **Se ordena antes de proyectar.** Ordenando después, el criterio es una propiedad
            // del objeto que se acaba de construir y EF no sabe traducir eso: la consulta falla al
            // ejecutarse con «could not be translated», no al compilar.
            .OrderByDescending(x => x.m.DetectadaUtc)
            .Select(x => new DocumentoQueMenciona(
                x.d.Id, x.p.Id, x.d.Title, x.p.Title, x.m.TextoVisible, x.m.DetectadaUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MencionEnDocumento>> DeLaPaginaAsync(
        Guid tenantId, Guid pageId, CancellationToken ct = default)
        => await contexto.MencionesEnDocumentos.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.PageId == pageId)
            .OrderBy(m => m.TextoVisible)
            .ToListAsync(ct);
}

/// <summary>
/// Responde el puerto <see cref="IMencionesEnDocumentos"/> para el inquilino de la petición.
///
/// Lo implementa Docs porque es quien guarda las menciones, y lo consumen las pantallas de tareas
/// y tickets sin conocerlo: la dependencia va de todos a BuildingBlocks, nunca entre módulos. Es
/// el mismo reparto que con los favoritos y la visibilidad.
/// </summary>
public sealed class MencionesEnDocumentos(
    IRepositorioDeMenciones repositorio,
    IUserContext usuario) : IMencionesEnDocumentos
{
    public Task<IReadOnlyList<DocumentoQueMenciona>> QuienMencionaAsync(
        string tipo, Guid entidadId, CancellationToken ct = default)
        => repositorio.QuienMencionaAsync(usuario.TenantId, tipo, entidadId, ct);
}
