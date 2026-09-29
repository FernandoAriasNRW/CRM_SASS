using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Domain;

namespace Reporting.Domain.Definitions;

/// <summary>Una condición sobre un campo.</summary>
public sealed record ReportFilter(string Campo, string Operador, string? Valor)
{
    public Result Validate(DataSource dataSource)
    {
        var field = dataSource.Field(Campo);
        if (field is null)
        {
            return Result.Failure(
                $"«{Campo}» no es un campo de {dataSource.Name}. Los que hay: "
                + string.Join(", ", dataSource.Fields.Select(c => c.Key)));
        }

        var op = ReportCatalog.Operator(Operador);
        if (op is null)
        {
            return Result.Failure(
                $"«{Operador}» no es un operador. Los que hay: "
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

        if (op.NeedsValue && string.IsNullOrWhiteSpace(Valor))
            return Result.Failure($"«{op.Name}» sobre {field.Name} necesita un valor");

        return Result.Success();
    }
}
