using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using CustomFields.Application.Abstractions;
using CustomFields.Application.Commands;
using CustomFields.Application.DTOs;
using CustomFields.Application.Queries;
using CustomFields.Domain.Entities;
using CustomFields.Domain.Services;
using CustomFields.Domain.ValueObjects;

namespace CustomFields.Application.Handlers;

public sealed class DefineCustomFieldCommandHandler(
    ICustomFieldRepository repository,
    ICustomFieldsUnitOfWork unitOfWork) : ICommandHandler<DefineCustomFieldCommand, CustomFieldDefinitionDto>
{
  public async Task<Result<CustomFieldDefinitionDto>> Handle(DefineCustomFieldCommand request, CancellationToken ct)
  {
    // El nombre es lo que ve la gente al rellenar: dos campos «Cliente» en la misma entidad
    // serían indistinguibles en el formulario.
    if (await repository.NameExistsAsync(request.TenantId, request.TargetEntity, request.Name.Trim(), null, ct))
      return Result<CustomFieldDefinitionDto>.Failure(CustomFieldDefinition.Rules.DuplicateName);

    CustomFieldDefinition definition;
    try
    {
      definition = CustomFieldDefinition.Create(
          request.TenantId, request.Name, request.Type, request.TargetEntity,
          request.IsRequired, request.Options, request.Position, request.Formula);
    }
    catch (InvalidOperationException ex) { return Result<CustomFieldDefinitionDto>.Failure(ex.Message); }

    // El dominio ya comprobó que la fórmula se puede leer. Lo que sólo se puede comprobar aquí
    // es contra qué apunta, porque hace falta ver los demás campos del inquilino.
    var problem = await FormulaValidator.ValidateAgainstOthersAsync(
        repository, request.TenantId, request.TargetEntity, request.Name, request.Formula, null, ct);

    if (problem is not null)
      return Result<CustomFieldDefinitionDto>.Failure(problem);

    await repository.AddDefinitionAsync(definition, ct);
    await unitOfWork.SaveChangesAsync(ct);

    return Result<CustomFieldDefinitionDto>.Success(ADto(definition));
  }

  internal static CustomFieldDefinitionDto ADto(CustomFieldDefinition d) =>
      new(d.Id, d.Name, d.Type, d.TargetEntity, d.IsRequired, d.Options, d.Position, d.Formula);
}

public sealed class UpdateCustomFieldCommandHandler(
    ICustomFieldRepository repository,
    ICustomFieldsUnitOfWork unitOfWork) : ICommandHandler<UpdateCustomFieldCommand, bool>
{
  public async Task<Result<bool>> Handle(UpdateCustomFieldCommand request, CancellationToken ct)
  {
    var definition = await repository.GetDefinitionAsync(request.TenantId, request.Id, ct);
    if (definition is null)
      return Result<bool>.Failure("El campo no existe");

    if (await repository.NameExistsAsync(request.TenantId, definition.TargetEntity, request.Name.Trim(), request.Id, ct))
      return Result<bool>.Failure(CustomFieldDefinition.Rules.DuplicateName);

    var problem = await FormulaValidator.ValidateAgainstOthersAsync(
        repository, request.TenantId, definition.TargetEntity, request.Name, request.Formula, request.Id, ct);

    if (problem is not null)
      return Result<bool>.Failure(problem);

    try { definition.Update(request.Name, request.IsRequired, request.Options, request.Position, request.Formula); }
    catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

    await unitOfWork.SaveChangesAsync(ct);
    return Result<bool>.Success(true);
  }
}

public sealed class RemoveCustomFieldCommandHandler(
    ICustomFieldRepository repository,
    ICustomFieldsUnitOfWork unitOfWork) : ICommandHandler<RemoveCustomFieldCommand, bool>
{
  public async Task<Result<bool>> Handle(RemoveCustomFieldCommand request, CancellationToken ct)
  {
    var definition = await repository.GetDefinitionAsync(request.TenantId, request.Id, ct);
    if (definition is null)
      return Result<bool>.Failure("El campo no existe");

    // Los valores se van con la definición: dejarlos sería guardar respuestas a una pregunta
    // que ya nadie hace.
    await repository.RemoveValuesOfDefinitionAsync(request.TenantId, request.Id, ct);
    repository.RemoveDefinition(definition);
    await unitOfWork.SaveChangesAsync(ct);

    return Result<bool>.Success(true);
  }
}

public sealed class SetCustomFieldValueCommandHandler(
    ICustomFieldRepository repository,
    ICustomFieldsUnitOfWork unitOfWork) : ICommandHandler<SetCustomFieldValueCommand, bool>
{
  public async Task<Result<bool>> Handle(SetCustomFieldValueCommand request, CancellationToken ct)
  {
    var definition = await repository.GetDefinitionAsync(request.TenantId, request.DefinitionId, ct);
    if (definition is null)
      return Result<bool>.Failure("El campo no existe");

    // Un campo calculado no se rellena. Aceptar el valor y luego ignorarlo al leer sería peor
    // que rechazarlo: quien lo escribió vería el suyo desaparecer sin explicación.
    if (FieldType.IsComputed(definition.Type))
      return Result<bool>.Failure(CustomFieldDefinition.Rules.ComputedIsReadOnly);

    // La validación es del dominio y devuelve el valor ya en forma canónica; el handler sólo
    // guarda lo que ella aprueba.
    var result = ValueValidator.Validate(definition, request.Value);
    if (!result.IsValid)
      return Result<bool>.Failure(result.Error!);

    var existing = await repository.GetValueAsync(request.TenantId, request.DefinitionId, request.EntityId, ct);

    if (existing is null)
      await repository.AddValueAsync(
          CustomFieldValue.Create(request.TenantId, request.DefinitionId, request.EntityId, result.CanonicalValue), ct);
    else
      existing.Change(result.CanonicalValue);

    await unitOfWork.SaveChangesAsync(ct);
    return Result<bool>.Success(true);
  }
}

public sealed class GetCustomFieldsQueryHandler(ICustomFieldRepository repository)
    : IQueryHandler<GetCustomFieldsQuery, IReadOnlyList<CustomFieldDefinitionDto>>
{
  public async Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> Handle(GetCustomFieldsQuery request, CancellationToken ct)
  {
    var definitions = await repository.GetDefinitionsAsync(request.TenantId, request.TargetEntity, ct);

    return Result<IReadOnlyList<CustomFieldDefinitionDto>>.Success(
        definitions.Select(DefineCustomFieldCommandHandler.ADto).ToList());
  }
}

/// <summary>
/// Los campos de una entidad, con su valor si lo tiene.
///
/// Devuelve **todas** las definiciones que aplican, no sólo las que ya tienen valor: si sólo
/// llegaran las rellenas, un campo nuevo no aparecería nunca en el formulario y nadie podría
/// rellenarlo.
/// </summary>
public sealed class GetCustomFieldValuesQueryHandler(ICustomFieldRepository repository)
    : IQueryHandler<GetCustomFieldValuesQuery, IReadOnlyList<CustomFieldValueDto>>
{
  public async Task<Result<IReadOnlyList<CustomFieldValueDto>>> Handle(GetCustomFieldValuesQuery request, CancellationToken ct)
  {
    var definitions = await repository.GetDefinitionsAsync(request.TenantId, request.TargetEntity, ct);
    var values = await repository.GetValuesAsync(request.TenantId, request.EntityId, ct);

    var byDefinition = values.ToDictionary(v => v.DefinitionId, v => v.Value);

    // Los campos calculados no tienen valor guardado: se calculan aquí, cada vez. Es lo que
    // garantiza que nunca estén desfasados —ver el comentario de TipoDeCampo.Formula— y lo que
    // permite que una fórmula use el resultado de otra.
    var computedValues = FieldCalculator.Compute(definitions, byDefinition)
        .ToDictionary(c => c.DefinitionId);

    var output = definitions
        .OrderBy(d => d.Position)
        .ThenBy(d => d.Name)
        .Select(d =>
        {
            if (computedValues.TryGetValue(d.Id, out var computedValue))
                return new CustomFieldValueDto(
                    d.Id, d.Name, d.Type, d.IsRequired, d.Options, d.Position,
                    computedValue.Value, d.Formula, computedValue.Error);

            return new CustomFieldValueDto(
                d.Id, d.Name, d.Type, d.IsRequired, d.Options, d.Position,
                byDefinition.TryGetValue(d.Id, out var value) ? value : null, d.Formula);
        })
        .ToList();

    return Result<IReadOnlyList<CustomFieldValueDto>>.Success(output);
  }
}
