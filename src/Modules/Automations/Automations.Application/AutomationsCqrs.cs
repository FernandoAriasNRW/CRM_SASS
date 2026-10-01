using Automations.Application.Abstractions;
using Automations.Domain.Entities;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;

namespace Automations.Application;

// ---------------------------------------------------------------- DTOs

public sealed record ConditionDto(string Field, string Operator, string? Value);

public sealed record ActionDto(string Type, string Value);

public sealed record AutomationRuleDto(
    Guid Id,
    string Name,
    string Trigger,
    bool IsActive,
    IReadOnlyList<ConditionDto> Conditions,
    IReadOnlyList<ActionDto> Actions,
    int ExecutionCount,
    DateTime? LastExecutedAtUtc);

// ---------------------------------------------------------------- Comandos

public sealed record DefineAutomationRuleCommand(
    Guid TenantId,
    string Name,
    string Trigger,
    IReadOnlyList<ConditionDto>? Conditions,
    IReadOnlyList<ActionDto> Actions) : ICommand<AutomationRuleDto>;

public sealed record UpdateAutomationRuleCommand(
    Guid TenantId,
    Guid Id,
    string Name,
    string Trigger,
    IReadOnlyList<ConditionDto>? Conditions,
    IReadOnlyList<ActionDto> Actions) : ICommand<bool>;

public sealed record SetAutomationRuleActiveCommand(
    Guid TenantId, Guid Id, bool IsActive) : ICommand<bool>;

public sealed record RemoveAutomationRuleCommand(Guid TenantId, Guid Id) : ICommand<bool>;

// ---------------------------------------------------------------- Consultas

public sealed record GetAutomationRulesQuery(Guid TenantId) : IQuery<IReadOnlyList<AutomationRuleDto>>;

/// <summary>Una ejecución tal como se enseña en el historial de la regla.</summary>
public sealed record ExecutionDto(
    Guid EntityId,
    string Outcome,
    string? Detail,
    DateTime AtUtc);

/// <summary>
/// Las últimas ejecuciones de una regla.
///
/// Es la respuesta a «mi automatización no funciona». El contador de la regla sólo distingue
/// «no salta» de «salta»; esto añade el caso que más despista, que es «salta y las condiciones
/// no se cumplen» —y entonces quien la configuró ve el contador a cero y culpa al disparador,
/// cuando lo que falla es una condición suya—.
/// </summary>
public sealed record GetExecutionsQuery(Guid TenantId, Guid RuleId, int Count = 20)
    : IQuery<IReadOnlyList<ExecutionDto>>;

public sealed class GetExecutionsHandler(IExecutionRepository repository)
    : IQueryHandler<GetExecutionsQuery, IReadOnlyList<ExecutionDto>>
{
    /// <summary>Un tope duro: sin él, quien pida cien mil se lleva la tabla entera.</summary>
    private const int MaxPerRequest = 100;

    public async Task<Result<IReadOnlyList<ExecutionDto>>> Handle(GetExecutionsQuery request, CancellationToken ct)
    {
        var count = Math.Clamp(request.Count, 1, MaxPerRequest);

        var executions = await repository.LatestForRuleAsync(
            request.TenantId, request.RuleId, count, ct);

        return Result<IReadOnlyList<ExecutionDto>>.Success(
            executions.Select(e => new ExecutionDto(e.EntityId, e.Outcome, e.Detail, e.AtUtc)).ToList());
    }
}

// ---------------------------------------------------------------- Traducción

public static class Mapping
{
    public static AutomationRuleDto ToDto(AutomationRule rule) => new(
        rule.Id,
        rule.Name,
        rule.Trigger,
        rule.IsActive,
        rule.Conditions.Select(c => new ConditionDto(c.Field, c.Operator, c.Value)).ToList(),
        rule.Actions.Select(a => new ActionDto(a.Type, a.Value)).ToList(),
        rule.ExecutionCount,
        rule.LastExecutedAtUtc);

    public static List<AutomationCondition> ToConditions(IEnumerable<ConditionDto>? dtos) =>
        (dtos ?? []).Select(c => new AutomationCondition(c.Field, c.Operator, c.Value)).ToList();

    public static List<AutomationAction> ToActions(IEnumerable<ActionDto>? dtos) =>
        (dtos ?? []).Select(a => new AutomationAction(a.Type, a.Value)).ToList();
}

// ---------------------------------------------------------------- Manejadores

public sealed class DefineAutomationRuleHandler(
    IAutomationRuleRepository repository,
    IAutomationsUnitOfWork unitOfWork) : ICommandHandler<DefineAutomationRuleCommand, AutomationRuleDto>
{
    public async Task<Result<AutomationRuleDto>> Handle(DefineAutomationRuleCommand request, CancellationToken ct)
    {
        // El nombre es lo único que distingue una automatización de otra en la lista. Dos con el
        // mismo nombre y distinto comportamiento son imposibles de administrar.
        if (await repository.ExistsWithNameAsync(request.TenantId, request.Name.Trim(), null, ct))
            return Result<AutomationRuleDto>.Failure(AutomationRule.Rules.DuplicateName);

        AutomationRule rule;
        try
        {
            rule = AutomationRule.Create(
                request.TenantId, request.Name, request.Trigger,
                Mapping.ToConditions(request.Conditions), Mapping.ToActions(request.Actions));
        }
        catch (InvalidOperationException ex) { return Result<AutomationRuleDto>.Failure(ex.Message); }

        await repository.AddAsync(rule, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<AutomationRuleDto>.Success(Mapping.ToDto(rule));
    }
}

public sealed class UpdateAutomationRuleHandler(
    IAutomationRuleRepository repository,
    IAutomationsUnitOfWork unitOfWork) : ICommandHandler<UpdateAutomationRuleCommand, bool>
{
    public async Task<Result<bool>> Handle(UpdateAutomationRuleCommand request, CancellationToken ct)
    {
        var rule = await repository.GetByIdAsync(request.TenantId, request.Id, ct);
        if (rule is null) return Result<bool>.Failure(NotFound);

        if (await repository.ExistsWithNameAsync(request.TenantId, request.Name.Trim(), request.Id, ct))
            return Result<bool>.Failure(AutomationRule.Rules.DuplicateName);

        try
        {
            rule.Update(
                request.Name, request.Trigger,
                Mapping.ToConditions(request.Conditions), Mapping.ToActions(request.Actions));
        }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

        await repository.UpdateAsync(rule, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public const string NotFound = "Automatización no encontrada";
}

public sealed class SetAutomationRuleActiveHandler(
    IAutomationRuleRepository repository,
    IAutomationsUnitOfWork unitOfWork) : ICommandHandler<SetAutomationRuleActiveCommand, bool>
{
    public async Task<Result<bool>> Handle(SetAutomationRuleActiveCommand request, CancellationToken ct)
    {
        var rule = await repository.GetByIdAsync(request.TenantId, request.Id, ct);
        if (rule is null) return Result<bool>.Failure(UpdateAutomationRuleHandler.NotFound);

        if (request.IsActive) rule.Activate(); else rule.Deactivate();

        await repository.UpdateAsync(rule, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

public sealed class RemoveAutomationRuleHandler(
    IAutomationRuleRepository repository,
    IAutomationsUnitOfWork unitOfWork) : ICommandHandler<RemoveAutomationRuleCommand, bool>
{
    public async Task<Result<bool>> Handle(RemoveAutomationRuleCommand request, CancellationToken ct)
    {
        var rule = await repository.GetByIdAsync(request.TenantId, request.Id, ct);
        if (rule is null) return Result<bool>.Failure(UpdateAutomationRuleHandler.NotFound);

        await repository.RemoveAsync(rule, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

public sealed class GetAutomationRulesHandler(IAutomationRuleRepository repository)
    : IQueryHandler<GetAutomationRulesQuery, IReadOnlyList<AutomationRuleDto>>
{
    public async Task<Result<IReadOnlyList<AutomationRuleDto>>> Handle(GetAutomationRulesQuery request, CancellationToken ct)
    {
        var rules = await repository.GetByTenantAsync(request.TenantId, ct);

        return Result<IReadOnlyList<AutomationRuleDto>>.Success(
            rules.Select(Mapping.ToDto).ToList());
    }
}
