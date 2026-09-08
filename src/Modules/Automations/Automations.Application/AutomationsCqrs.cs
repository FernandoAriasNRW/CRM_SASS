using Automations.Application.Abstractions;
using Automations.Domain.Entities;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;

namespace Automations.Application;

// ---------------------------------------------------------------- DTOs

public sealed record CondicionDto(string Campo, string Operador, string? Valor);

public sealed record AccionDto(string Tipo, string Valor);

public sealed record AutomationRuleDto(
    Guid Id,
    string Nombre,
    string Disparador,
    bool Activa,
    IReadOnlyList<CondicionDto> Condiciones,
    IReadOnlyList<AccionDto> Acciones,
    int VecesEjecutada,
    DateTime? UltimaEjecucionUtc);

// ---------------------------------------------------------------- Comandos

public sealed record DefineAutomationRuleCommand(
    Guid TenantId,
    string Nombre,
    string Disparador,
    IReadOnlyList<CondicionDto>? Condiciones,
    IReadOnlyList<AccionDto> Acciones) : ICommand<AutomationRuleDto>;

public sealed record UpdateAutomationRuleCommand(
    Guid TenantId,
    Guid Id,
    string Nombre,
    string Disparador,
    IReadOnlyList<CondicionDto>? Condiciones,
    IReadOnlyList<AccionDto> Acciones) : ICommand<bool>;

public sealed record SetAutomationRuleActiveCommand(
    Guid TenantId, Guid Id, bool Activa) : ICommand<bool>;

public sealed record RemoveAutomationRuleCommand(Guid TenantId, Guid Id) : ICommand<bool>;

// ---------------------------------------------------------------- Consultas

public sealed record GetAutomationRulesQuery(Guid TenantId) : IQuery<IReadOnlyList<AutomationRuleDto>>;

// ---------------------------------------------------------------- Traducción

public static class Mapeo
{
    public static AutomationRuleDto ADto(AutomationRule regla) => new(
        regla.Id,
        regla.Nombre,
        regla.Disparador,
        regla.Activa,
        regla.Condiciones.Select(c => new CondicionDto(c.Campo, c.Operador, c.Valor)).ToList(),
        regla.Acciones.Select(a => new AccionDto(a.Tipo, a.Valor)).ToList(),
        regla.VecesEjecutada,
        regla.UltimaEjecucionUtc);

    public static List<CondicionDeAutomatizacion> ACondiciones(IEnumerable<CondicionDto>? dtos) =>
        (dtos ?? []).Select(c => new CondicionDeAutomatizacion(c.Campo, c.Operador, c.Valor)).ToList();

    public static List<AccionDeAutomatizacion> AAcciones(IEnumerable<AccionDto>? dtos) =>
        (dtos ?? []).Select(a => new AccionDeAutomatizacion(a.Tipo, a.Valor)).ToList();
}

// ---------------------------------------------------------------- Manejadores

public sealed class DefineAutomationRuleHandler(
    IAutomationRuleRepository repositorio,
    IAutomationsUnitOfWork unitOfWork) : ICommandHandler<DefineAutomationRuleCommand, AutomationRuleDto>
{
    public async Task<Result<AutomationRuleDto>> Handle(DefineAutomationRuleCommand request, CancellationToken ct)
    {
        // El nombre es lo único que distingue una automatización de otra en la lista. Dos con el
        // mismo nombre y distinto comportamiento son imposibles de administrar.
        if (await repositorio.ExisteConNombreAsync(request.TenantId, request.Nombre.Trim(), null, ct))
            return Result<AutomationRuleDto>.Failure(AutomationRule.Reglas.NombreRepetido);

        AutomationRule regla;
        try
        {
            regla = AutomationRule.Create(
                request.TenantId, request.Nombre, request.Disparador,
                Mapeo.ACondiciones(request.Condiciones), Mapeo.AAcciones(request.Acciones));
        }
        catch (InvalidOperationException ex) { return Result<AutomationRuleDto>.Failure(ex.Message); }

        await repositorio.AddAsync(regla, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<AutomationRuleDto>.Success(Mapeo.ADto(regla));
    }
}

public sealed class UpdateAutomationRuleHandler(
    IAutomationRuleRepository repositorio,
    IAutomationsUnitOfWork unitOfWork) : ICommandHandler<UpdateAutomationRuleCommand, bool>
{
    public async Task<Result<bool>> Handle(UpdateAutomationRuleCommand request, CancellationToken ct)
    {
        var regla = await repositorio.GetByIdAsync(request.TenantId, request.Id, ct);
        if (regla is null) return Result<bool>.Failure(NoEncontrada);

        if (await repositorio.ExisteConNombreAsync(request.TenantId, request.Nombre.Trim(), request.Id, ct))
            return Result<bool>.Failure(AutomationRule.Reglas.NombreRepetido);

        try
        {
            regla.Actualizar(
                request.Nombre, request.Disparador,
                Mapeo.ACondiciones(request.Condiciones), Mapeo.AAcciones(request.Acciones));
        }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

        await repositorio.UpdateAsync(regla, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public const string NoEncontrada = "Automatización no encontrada";
}

public sealed class SetAutomationRuleActiveHandler(
    IAutomationRuleRepository repositorio,
    IAutomationsUnitOfWork unitOfWork) : ICommandHandler<SetAutomationRuleActiveCommand, bool>
{
    public async Task<Result<bool>> Handle(SetAutomationRuleActiveCommand request, CancellationToken ct)
    {
        var regla = await repositorio.GetByIdAsync(request.TenantId, request.Id, ct);
        if (regla is null) return Result<bool>.Failure(UpdateAutomationRuleHandler.NoEncontrada);

        if (request.Activa) regla.Activar(); else regla.Desactivar();

        await repositorio.UpdateAsync(regla, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

public sealed class RemoveAutomationRuleHandler(
    IAutomationRuleRepository repositorio,
    IAutomationsUnitOfWork unitOfWork) : ICommandHandler<RemoveAutomationRuleCommand, bool>
{
    public async Task<Result<bool>> Handle(RemoveAutomationRuleCommand request, CancellationToken ct)
    {
        var regla = await repositorio.GetByIdAsync(request.TenantId, request.Id, ct);
        if (regla is null) return Result<bool>.Failure(UpdateAutomationRuleHandler.NoEncontrada);

        await repositorio.RemoveAsync(regla, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

public sealed class GetAutomationRulesHandler(IAutomationRuleRepository repositorio)
    : IQueryHandler<GetAutomationRulesQuery, IReadOnlyList<AutomationRuleDto>>
{
    public async Task<Result<IReadOnlyList<AutomationRuleDto>>> Handle(GetAutomationRulesQuery request, CancellationToken ct)
    {
        var reglas = await repositorio.GetByTenantAsync(request.TenantId, ct);

        return Result<IReadOnlyList<AutomationRuleDto>>.Success(
            reglas.Select(Mapeo.ADto).ToList());
    }
}
