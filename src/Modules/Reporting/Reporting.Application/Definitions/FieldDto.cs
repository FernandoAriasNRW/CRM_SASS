using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

/// <param name="Operators">
/// Los operadores aplicables a **este** campo, ya resueltos. Se mandan por campo y no sólo por
/// tipo porque la nullabilidad también decide: «está vacío» vale sobre la fecha de resolución de
/// un ticket y no sobre el vencimiento de una tarea. Con la lista por tipo, la pantalla ofrecía
/// una combinación que el servidor rechaza.
/// </param>
/// <param name="Values">
/// Los valores admitidos, cuando el campo es una lista cerrada —un estado, una prioridad—, o
/// vacío si admite texto libre.
///
/// <b>Se sirven para que nadie los escriba a mano.</b> Sin ellos, filtrar por estado obliga a
/// teclear «Open» adivinando, y un valor mal escrito produce un informe vacío que parece un
/// informe sin datos. Es la misma clase de fallo que este módulo ya ha tenido tres veces.
/// </param>
public sealed record FieldDto(
    string Key, string Name, string Type, IReadOnlyList<string> Operators, IReadOnlyList<string> Values);
