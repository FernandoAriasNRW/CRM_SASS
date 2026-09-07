using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Programaciones;

public sealed record ProgramacionDto(
    Guid Id,
    Guid ReportId,
    string Frecuencia,
    string Formato,
    string Hora,
    int? Dia,
    bool Activa,
    DateOnly? UltimoDiaGenerado);

/// <summary>
/// Programa un informe: cada cuánto, a qué hora y en qué formato.
///
/// La hora llega como «HH:mm» y es **local del inquilino**, no UTC. Ver
/// <see cref="ProgramacionDeInforme"/> para el porqué.
/// </summary>
public sealed record ProgramarInformeCommand(
    Guid TenantId,
    Guid ReportId,
    Guid DestinatarioId,
    string Frecuencia,
    string Formato,
    string Hora,
    int? Dia) : ICommand<ProgramacionDto>;

public sealed record GetProgramacionesQuery(Guid TenantId, Guid ReportId) : IQuery<IReadOnlyList<ProgramacionDto>>;

public sealed record CambiarProgramacionCommand(Guid TenantId, Guid Id, bool Activa) : ICommand<bool>;

public sealed record QuitarProgramacionCommand(Guid TenantId, Guid Id) : ICommand<bool>;

public sealed class ProgramarInformeHandler(
    IReportRepository informes,
    IRepositorioDeProgramaciones programaciones,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<ProgramarInformeCommand, ProgramacionDto>
{
    public async Task<Result<ProgramacionDto>> Handle(ProgramarInformeCommand request, CancellationToken ct)
    {
        var frecuencia = FrecuenciaDeInforme.FromName<FrecuenciaDeInforme>(request.Frecuencia);
        if (frecuencia is null)
        {
            return Result<ProgramacionDto>.Failure(
                $"La frecuencia «{request.Frecuencia}» no existe. Las que hay: "
                + string.Join(", ", FrecuenciaDeInforme.All().Select(f => f.Name)));
        }

        var formato = ReportFormat.FromName<ReportFormat>(request.Formato);
        if (formato is null)
        {
            return Result<ProgramacionDto>.Failure(
                $"El formato «{request.Formato}» no existe. Los que hay: "
                + string.Join(", ", ReportFormat.All().Select(f => f.Name)));
        }

        if (!TimeOnly.TryParse(request.Hora, out var hora))
            return Result<ProgramacionDto>.Failure($"«{request.Hora}» no es una hora; se espera algo como 08:00");

        var informe = await informes.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (informe is null)
            return Result<ProgramacionDto>.Failure("El informe no existe");

        var nueva = ProgramacionDeInforme.Crear(
            request.TenantId, request.ReportId, request.DestinatarioId, frecuencia, formato, hora, request.Dia);

        if (nueva.IsFailure)
            return Result<ProgramacionDto>.Failure(nueva.Error!);

        await programaciones.AnadirAsync(nueva.Value!, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<ProgramacionDto>.Success(ADto(nueva.Value!));
    }

    internal static ProgramacionDto ADto(ProgramacionDeInforme p) => new(
        p.Id, p.ReportId, p.Frecuencia.Name, p.Formato.Name,
        p.Hora.ToString("HH\\:mm"), p.Dia, p.Activa, p.UltimoDiaGenerado);
}

public sealed class GetProgramacionesHandler(IRepositorioDeProgramaciones programaciones)
    : IQueryHandler<GetProgramacionesQuery, IReadOnlyList<ProgramacionDto>>
{
    public async Task<Result<IReadOnlyList<ProgramacionDto>>> Handle(GetProgramacionesQuery request, CancellationToken ct)
    {
        var lista = await programaciones.DelInformeAsync(request.TenantId, request.ReportId, ct);

        return Result<IReadOnlyList<ProgramacionDto>>.Success(
            lista.Select(ProgramarInformeHandler.ADto).ToList());
    }
}

public sealed class CambiarProgramacionHandler(
    IRepositorioDeProgramaciones programaciones,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<CambiarProgramacionCommand, bool>
{
    public async Task<Result<bool>> Handle(CambiarProgramacionCommand request, CancellationToken ct)
    {
        var programacion = await programaciones.PorIdAsync(request.TenantId, request.Id, ct);
        if (programacion is null)
            return Result<bool>.Failure("Esa programación no existe");

        if (request.Activa) programacion.Activar();
        else programacion.Desactivar();

        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class QuitarProgramacionHandler(
    IRepositorioDeProgramaciones programaciones,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<QuitarProgramacionCommand, bool>
{
    public async Task<Result<bool>> Handle(QuitarProgramacionCommand request, CancellationToken ct)
    {
        var programacion = await programaciones.PorIdAsync(request.TenantId, request.Id, ct);

        // Quitar algo que ya no está no es un error: es el estado que se pedía.
        if (programacion is null)
            return Result<bool>.Success(true);

        programaciones.Quitar(programacion);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public interface IRepositorioDeProgramaciones
{
    Task<ProgramacionDeInforme?> PorIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ProgramacionDeInforme>> DelInformeAsync(Guid tenantId, Guid reportId, CancellationToken ct = default);

    /// <summary>
    /// Todas las activas, <b>de todos los inquilinos</b>.
    ///
    /// El trabajador corre sin petición y por tanto sin inquilino, así que esta consulta cruza el
    /// filtro global a propósito. Es la excepción que <c>TenantDbContext</c> documenta, y está
    /// aquí —en un método con nombre— para que el trabajador no escriba consultas por su cuenta.
    /// </summary>
    Task<IReadOnlyList<ProgramacionDeInforme>> ActivasAsync(CancellationToken ct = default);

    Task AnadirAsync(ProgramacionDeInforme programacion, CancellationToken ct = default);

    void Quitar(ProgramacionDeInforme programacion);

    Task GuardarAsync(CancellationToken ct = default);
}
