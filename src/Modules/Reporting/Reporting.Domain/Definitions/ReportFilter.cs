using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Domain;

namespace Reporting.Domain.Definitions;

/// <summary>Una condición sobre un campo.</summary>
public sealed record ReportFilter(string Field, string Operator, string? Value)
{
    public Result Validate(DataSource dataSource)
    {
        var field = dataSource.Field(Field);
        if (field is null)
        {
            return Result.Failure(
                $"«{Field}» no es un campo de {dataSource.Name}. Los que hay: "
                + string.Join(", ", dataSource.Fields.Select(c => c.Key)));
        }

        var op = ReportCatalog.Operator(Operator);
        if (op is null)
        {
            return Result.Failure(
                $"«{Operator}» no es un operador. Los que hay: "
                + string.Join(", ", ReportCatalog.Operators().Select(o => o.Key)));
        }

        // Se comprueba contra los operadores de **ese campo**, no sólo de su tipo. «Mayor que»
        // sobre un estado no significa nada, y «está vacío» sobre un campo que siempre tiene
        // valor tampoco: el segundo se colaba mirando sólo el tipo, y el filtro reventaba al
        // ejecutarse.
        if (ReportCatalog.OperatorsFor(field).All(o => o.Key != op.Key))
        {
            return Result.Failure(
                $"«{op.Name}» no se puede aplicar a {field.Name}"
                + (op.ChecksEmptiness ? ", que siempre tiene valor" : $", que es de tipo {field.Type}"));
        }

        if (op.NeedsValue && string.IsNullOrWhiteSpace(Value))
            return Result.Failure($"«{op.Name}» sobre {field.Name} necesita un valor");

        return Result.Success();
    }
}
