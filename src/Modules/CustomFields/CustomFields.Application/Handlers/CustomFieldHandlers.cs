using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using CustomFields.Application.Abstractions;
using CustomFields.Application.Commands;
using CustomFields.Application.DTOs;
using CustomFields.Application.Queries;
using CustomFields.Domain.Entities;
using CustomFields.Domain.Servicios;

namespace CustomFields.Application.Handlers;

public sealed class DefineCustomFieldCommandHandler(
    ICustomFieldRepository repositorio,
    ICustomFieldsUnitOfWork unitOfWork) : ICommandHandler<DefineCustomFieldCommand, CustomFieldDefinitionDto>
{
  public async Task<Result<CustomFieldDefinitionDto>> Handle(DefineCustomFieldCommand request, CancellationToken ct)
  {
    // El nombre es lo que ve la gente al rellenar: dos campos «Cliente» en la misma entidad
    // serían indistinguibles en el formulario.
    if (await repositorio.ExisteNombreAsync(request.TenantId, request.EntidadDestino, request.Nombre.Trim(), null, ct))
      return Result<CustomFieldDefinitionDto>.Failure(CustomFieldDefinition.Reglas.NombreRepetido);

    CustomFieldDefinition definicion;
    try
    {
      definicion = CustomFieldDefinition.Create(
          request.TenantId, request.Nombre, request.Tipo, request.EntidadDestino,
          request.Obligatorio, request.Opciones, request.Posicion);
    }
    catch (InvalidOperationException ex) { return Result<CustomFieldDefinitionDto>.Failure(ex.Message); }

    await repositorio.AddDefinitionAsync(definicion, ct);
    await unitOfWork.SaveChangesAsync(ct);

    return Result<CustomFieldDefinitionDto>.Success(ADto(definicion));
  }

  internal static CustomFieldDefinitionDto ADto(CustomFieldDefinition d) =>
      new(d.Id, d.Nombre, d.Tipo, d.EntidadDestino, d.Obligatorio, d.Opciones, d.Posicion);
}

public sealed class UpdateCustomFieldCommandHandler(
    ICustomFieldRepository repositorio,
    ICustomFieldsUnitOfWork unitOfWork) : ICommandHandler<UpdateCustomFieldCommand, bool>
{
  public async Task<Result<bool>> Handle(UpdateCustomFieldCommand request, CancellationToken ct)
  {
    var definicion = await repositorio.GetDefinitionAsync(request.TenantId, request.Id, ct);
    if (definicion is null)
      return Result<bool>.Failure("El campo no existe");

    if (await repositorio.ExisteNombreAsync(request.TenantId, definicion.EntidadDestino, request.Nombre.Trim(), request.Id, ct))
      return Result<bool>.Failure(CustomFieldDefinition.Reglas.NombreRepetido);

    try { definicion.Actualizar(request.Nombre, request.Obligatorio, request.Opciones, request.Posicion); }
    catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

    await unitOfWork.SaveChangesAsync(ct);
    return Result<bool>.Success(true);
  }
}

public sealed class RemoveCustomFieldCommandHandler(
    ICustomFieldRepository repositorio,
    ICustomFieldsUnitOfWork unitOfWork) : ICommandHandler<RemoveCustomFieldCommand, bool>
{
  public async Task<Result<bool>> Handle(RemoveCustomFieldCommand request, CancellationToken ct)
  {
    var definicion = await repositorio.GetDefinitionAsync(request.TenantId, request.Id, ct);
    if (definicion is null)
      return Result<bool>.Failure("El campo no existe");

    // Los valores se van con la definición: dejarlos sería guardar respuestas a una pregunta
    // que ya nadie hace.
    await repositorio.RemoveValuesOfDefinitionAsync(request.TenantId, request.Id, ct);
    repositorio.RemoveDefinition(definicion);
    await unitOfWork.SaveChangesAsync(ct);

    return Result<bool>.Success(true);
  }
}

public sealed class SetCustomFieldValueCommandHandler(
    ICustomFieldRepository repositorio,
    ICustomFieldsUnitOfWork unitOfWork) : ICommandHandler<SetCustomFieldValueCommand, bool>
{
  public async Task<Result<bool>> Handle(SetCustomFieldValueCommand request, CancellationToken ct)
  {
    var definicion = await repositorio.GetDefinitionAsync(request.TenantId, request.DefinitionId, ct);
    if (definicion is null)
      return Result<bool>.Failure("El campo no existe");

    // La validación es del dominio y devuelve el valor ya en forma canónica; el handler sólo
    // guarda lo que ella aprueba.
    var resultado = ValidadorDeValor.Validar(definicion, request.Valor);
    if (!resultado.EsValido)
      return Result<bool>.Failure(resultado.Error!);

    var existente = await repositorio.GetValueAsync(request.TenantId, request.DefinitionId, request.EntityId, ct);

    if (existente is null)
      await repositorio.AddValueAsync(
          CustomFieldValue.Create(request.TenantId, request.DefinitionId, request.EntityId, resultado.ValorCanonico), ct);
    else
      existente.Cambiar(resultado.ValorCanonico);

    await unitOfWork.SaveChangesAsync(ct);
    return Result<bool>.Success(true);
  }
}

public sealed class GetCustomFieldsQueryHandler(ICustomFieldRepository repositorio)
    : IQueryHandler<GetCustomFieldsQuery, IReadOnlyList<CustomFieldDefinitionDto>>
{
  public async Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> Handle(GetCustomFieldsQuery request, CancellationToken ct)
  {
    var definiciones = await repositorio.GetDefinitionsAsync(request.TenantId, request.EntidadDestino, ct);

    return Result<IReadOnlyList<CustomFieldDefinitionDto>>.Success(
        definiciones.Select(DefineCustomFieldCommandHandler.ADto).ToList());
  }
}

/// <summary>
/// Los campos de una entidad, con su valor si lo tiene.
///
/// Devuelve **todas** las definiciones que aplican, no sólo las que ya tienen valor: si sólo
/// llegaran las rellenas, un campo nuevo no aparecería nunca en el formulario y nadie podría
/// rellenarlo.
/// </summary>
public sealed class GetCustomFieldValuesQueryHandler(ICustomFieldRepository repositorio)
    : IQueryHandler<GetCustomFieldValuesQuery, IReadOnlyList<CustomFieldValueDto>>
{
  public async Task<Result<IReadOnlyList<CustomFieldValueDto>>> Handle(GetCustomFieldValuesQuery request, CancellationToken ct)
  {
    var definiciones = await repositorio.GetDefinitionsAsync(request.TenantId, request.EntidadDestino, ct);
    var valores = await repositorio.GetValuesAsync(request.TenantId, request.EntityId, ct);

    var porDefinicion = valores.ToDictionary(v => v.DefinitionId, v => v.Valor);

    var salida = definiciones
        .OrderBy(d => d.Posicion)
        .ThenBy(d => d.Nombre)
        .Select(d => new CustomFieldValueDto(
            d.Id, d.Nombre, d.Tipo, d.Obligatorio, d.Opciones, d.Posicion,
            porDefinicion.TryGetValue(d.Id, out var valor) ? valor : null))
        .ToList();

    return Result<IReadOnlyList<CustomFieldValueDto>>.Success(salida);
  }
}
