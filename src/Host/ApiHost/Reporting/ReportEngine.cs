using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Projects.Infrastructure.Persistence;
using Reporting.Application.Exports;
using Reporting.Domain.Definitions;
using Ticketing.Infrastructure.Persistence;
using WorkItems.Infrastructure.Persistence;

namespace ApiHost.Reporting;

/// <summary>
/// Traduce una <see cref="ReportDefinition"/> a filas.
///
/// <b>Traduce, no interpreta.</b> Cada pieza de la definición se resuelve con un <c>switch</c>
/// contra <see cref="ReportCatalog"/>; no se compone SQL ni se construyen expresiones desde
/// texto. Es lo que hace que un constructor de informes en manos de los usuarios no sea una vía
/// para ejecutar consultas arbitrarias: lo que no está en el catálogo no llega hasta aquí, y si
/// llegara, caería en el <c>default</c> con un mensaje que dice qué falta.
///
/// <b>Vive en el host</b> por lo mismo que <see cref="DashboardQueries"/> y
/// <see cref="ReportData"/>: un informe de tareas mira WorkItems y uno de tickets mira
/// Ticketing, y ningún módulo referencia a otro.
///
/// <b>Se agrupa en memoria, no en la base.</b> Es deliberado y tiene un límite escrito: primero
/// se filtra en la base —que es donde está el volumen— y se traen sólo las dos columnas que hacen
/// falta, agrupación y medida. Agrupar en SQL exigiría construir la expresión de agrupación
/// dinámicamente, que es justo lo que este diseño evita. Con el tope de
/// <see cref="ReportData.MaxRows"/> filas, la diferencia no se nota; por encima de eso,
/// habría que escribir un traductor a <c>GroupBy</c> por cada campo del catálogo.
/// </summary>
public sealed class ReportEngine(
    WorkItemsDbContext tasksDb,
    TicketingDbContext ticketsDb,
    ProjectsDbContext projectsDb) : global::Reporting.Application.Definitions.IReportResolver
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Lo que se escribe cuando un grupo no tiene valor: sin responsable, sin fecha.</summary>
    private const string Unassigned = "(sin asignar)";

    /// <summary>Donde va a parar la cola cuando hay más grupos de los que caben.</summary>
    private const string Others = "Otros";

    /// <summary>Lo que se escribe cuando no hay nada que promediar. Nunca un cero.</summary>
    private const string NoData = "—";

    /// <summary>
    /// Los valores admitidos de un campo de lista cerrada.
    ///
    /// Los aporta el host porque viven en otros módulos: los estados de un ticket son de
    /// Ticketing y los de una tarea de WorkItems, y Reporting no referencia a ninguno.
    ///
    /// Salen de los propios objetos de valor del dominio, no de una lista escrita aquí: añadir un
    /// estado nuevo a un ticket lo pone en el constructor de informes sin tocar nada.
    /// </summary>
    public IReadOnlyList<string> ValuesOf(string dataSource, string field) => (dataSource, field) switch
    {
        ("Tasks", "status") => WorkItems.Domain.ValueObjects.TaskStatus
            .All().Select(e => e.Value).ToList(),

        ("Tasks", "priority") => WorkItems.Domain.ValueObjects.TaskPriority
            .All().Select(p => p.Value).ToList(),

        ("Tickets", "status") => Ticketing.Domain.ValueObjects.TicketStatus
            .All().Select(e => e.Name).ToList(),

        ("Tickets", "priority") => Ticketing.Domain.ValueObjects.TicketPriority
            .All().Select(p => p.Name).ToList(),

        // ProjectStatus no expone `All()` propio; se usa el `GetAll` de la enumeración base, que
        // es de donde salen los demás. Así, un estado nuevo aparece aquí solo.
        ("Projects", "status") => Projects.Domain.ValueObjects.ProjectStatus
            .GetAll<Projects.Domain.ValueObjects.ProjectStatus>().Select(e => e.Value).ToList(),

        // Lo demás es texto libre, una fecha o un identificador: no hay lista que ofrecer.
        _ => []
    };

    public async Task<ReportTable> ResolveAsync(
        string title, Guid tenantId, ReportDefinition definition, CancellationToken ct)
    {
        var validacion = definition.Validate();
        if (validacion.IsFailure)
            throw new InvalidOperationException(validacion.Error);

        // El inquilino se declara antes de consultar: el motor lo llama el trabajador de segundo
        // plano, que no tiene petición y por tanto no tiene usuario. Sin esto el filtro global
        // compara contra Guid.Empty y devuelve cero filas sin dar ningún error.
        using var _ = tasksDb.AsTenant(tenantId);
        using var __ = ticketsDb.AsTenant(tenantId);
        using var ___ = projectsDb.AsTenant(tenantId);

        var dataSource = ReportCatalog.FindDataSource(definition.DataSource)!;

        var raw = dataSource.Key switch
        {
            "Tasks" => await FromTasksAsync(tenantId, definition, ct),
            "Tickets" => await FromTicketsAsync(tenantId, definition, ct),
            "Projects" => await FromProjectsAsync(tenantId, definition, ct),
            _ => throw new InvalidOperationException($"El origen «{dataSource.Key}» no tiene motor todavía")
        };

        return Compose(title, definition, dataSource, raw);
    }

    /// <summary>
    /// Una fila en bruto: por qué grupo cae y qué número aporta.
    ///
    /// El número es opcional porque el conteo no lo usa, y porque una media sobre filas sin valor
    /// —tickets sin resolver, tareas sin horas— debe ignorarlas en lugar de contarlas como cero.
    /// Contar los ceros bajaría la media y nadie sabría por qué.
    /// </summary>
    private sealed record RawRow(string Group, double? Value);

    #region Los tres orígenes

    private async Task<List<RawRow>> FromTasksAsync(Guid tenantId, ReportDefinition d, CancellationToken ct)
    {
        var query = tasksDb.Tasks.AsNoTracking().Where(t => t.TenantId == tenantId);

        foreach (var filter in d.AppliedFilters)
        {
            query = filter.Field.ToLowerInvariant() switch
            {
                "status" => ApplyText(query, filter, t => t.Status.Value),
                "priority" => ApplyText(query, filter, t => t.Priority.Value),
                "assignee" => ApplyGuid(query, filter, t => t.AssigneeId),
                "project" => ApplyGuid(query, filter, t => t.ProjectId),
                "due_date" => ApplyDate(query, filter, t => t.DueDate.ToDateTime(TimeOnly.MinValue)),
                "created_at" => ApplyDate(query, filter, t => t.CreatedAtUtc),
                "estimated_hours" => ApplyNumber(query, filter, t => (double)t.EstimatedHours),
                _ => throw new InvalidOperationException($"No sé filtrar tareas por «{filter.Field}»")
            };
        }

        var rows = await query
            .Take(ReportData.MaxRows)
            .Select(t => new
            {
                Status = t.Status.Value,
                Priority = t.Priority.Value,
                t.AssigneeId,
                t.ProjectId,
                t.DueDate,
                t.CreatedAtUtc,
                t.EstimatedHours
            })
            .ToListAsync(ct);

        // Los nombres de proyecto se resuelven de una vez y no fila a fila: agrupar por proyecto
        // sobre mil tareas serían mil consultas.
        var projectNames = d.GroupBy.Equals("project", StringComparison.OrdinalIgnoreCase)
            ? await projectsDb.Projects.AsNoTracking()
                .Where(p => p.TenantId == tenantId)
                .ToDictionaryAsync(p => p.Id, p => p.Name.Value, ct)
            : [];

        return rows.Select(t => new RawRow(
            Group: d.GroupBy.ToLowerInvariant() switch
            {
                "status" => t.Status,
                "priority" => t.Priority,
                "assignee" => t.AssigneeId == Guid.Empty ? Unassigned : t.AssigneeId.ToString(),
                "project" => projectNames.GetValueOrDefault(t.ProjectId, Unassigned),
                "due_date" => ByDate(t.DueDate.ToDateTime(TimeOnly.MinValue), d.Granularity),
                "created_at" => ByDate(t.CreatedAtUtc, d.Granularity),
                "estimated_hours" => t.EstimatedHours.ToString("0.##", Spanish),
                _ => throw new InvalidOperationException($"No sé agrupar tareas por «{d.GroupBy}»")
            },
            Value: d.Measure.ToLowerInvariant() switch
            {
                "count" => null,
                "sum_estimated_hours" or "avg_estimated_hours" => (double)t.EstimatedHours,
                _ => throw new InvalidOperationException($"No sé calcular «{d.Measure}» sobre tareas")
            }))
            .ToList();
    }

    private async Task<List<RawRow>> FromTicketsAsync(Guid tenantId, ReportDefinition d, CancellationToken ct)
    {
        var query = ticketsDb.Tickets.AsNoTracking().Where(t => t.TenantId == tenantId);

        foreach (var filter in d.AppliedFilters)
        {
            query = filter.Field.ToLowerInvariant() switch
            {
                // El estado y la prioridad se guardan como número. **Se traduce el valor que
                // llega, no la columna**: meter la traducción dentro de la consulta
                // —`t => EstadoDeTicket(t.StatusValue)`— parece lo natural y EF no sabe traducir
                // esa llamada a SQL, así que la consulta reventaba en cuanto alguien filtraba por
                // estado. Comparando contra el número, el filtro se traduce solo.
                "status" => ApplyCode(query, filter, t => t.StatusValue, StatusCode),
                "priority" => ApplyCode(query, filter, t => t.PriorityValue, PriorityCode),
                "agent" => ApplyNullableGuid(query, filter, t => t.AssignedAgentId),
                "created_at" => ApplyDate(query, filter, t => t.CreatedAt),
                "resolved_at" => ApplyNullableDate(query, filter, t => t.ResolvedAt),
                _ => throw new InvalidOperationException($"No sé filtrar tickets por «{filter.Field}»")
            };
        }

        var rows = await query
            .Take(ReportData.MaxRows)
            .Select(t => new { t.StatusValue, t.PriorityValue, t.AssignedAgentId, t.CreatedAt, t.ResolvedAt })
            .ToListAsync(ct);

        return rows.Select(t => new RawRow(
            Group: d.GroupBy.ToLowerInvariant() switch
            {
                "status" => Ticketing.Domain.ValueObjects.TicketStatus
                    .FromValue<Ticketing.Domain.ValueObjects.TicketStatus>(t.StatusValue).Name,
                "priority" => Ticketing.Domain.ValueObjects.TicketPriority
                    .FromValue<Ticketing.Domain.ValueObjects.TicketPriority>(t.PriorityValue).Name,
                // Un ticket sin agente y otro con el Guid vacío son lo mismo para quien lee el
                // informe: nadie lo lleva. Se juntan en un solo grupo en vez de dar dos filas que
                // significan lo mismo.
                "agent" => t.AssignedAgentId is null || t.AssignedAgentId == Guid.Empty
                    ? Unassigned
                    : t.AssignedAgentId.Value.ToString(),
                "created_at" => ByDate(t.CreatedAt, d.Granularity),
                "resolved_at" => t.ResolvedAt is null ? Unassigned : ByDate(t.ResolvedAt.Value, d.Granularity),
                _ => throw new InvalidOperationException($"No sé agrupar tickets por «{d.GroupBy}»")
            },
            Value: d.Measure.ToLowerInvariant() switch
            {
                "count" => null,

                // Sólo cuentan los resueltos. Meter los abiertos con los días que llevan mezclaría
                // «cuánto se tarda en resolver» con «cuánto lleva esperando esto», que son dos
                // preguntas distintas y la media de las dos no contesta ninguna.
                "avg_days_to_resolve" => t.ResolvedAt is null
                    ? null
                    : (t.ResolvedAt.Value - t.CreatedAt).TotalDays,

                _ => throw new InvalidOperationException($"No sé calcular «{d.Measure}» sobre tickets")
            }))
            .ToList();
    }

    private async Task<List<RawRow>> FromProjectsAsync(Guid tenantId, ReportDefinition d, CancellationToken ct)
    {
        var query = projectsDb.Projects.AsNoTracking().Where(p => p.TenantId == tenantId);

        foreach (var filter in d.AppliedFilters)
        {
            query = filter.Field.ToLowerInvariant() switch
            {
                "status" => ApplyText(query, filter, p => p.Status.Value),
                "owner" => ApplyGuid(query, filter, p => p.OwnerId),
                // StartDate es DateOnly; se convierte para poder comparar con la fecha del filtro
                // en la propia consulta.
                "start_date" => ApplyDate(query, filter, p => p.StartDate.ToDateTime(TimeOnly.MinValue)),
                _ => throw new InvalidOperationException($"No sé filtrar proyectos por «{filter.Field}»")
            };
        }

        var rows = await query
            .Take(ReportData.MaxRows)
            .Select(p => new { Status = p.Status.Value, p.OwnerId, p.StartDate })
            .ToListAsync(ct);

        return rows.Select(p => new RawRow(
            Group: d.GroupBy.ToLowerInvariant() switch
            {
                "status" => p.Status,
                "owner" => p.OwnerId == Guid.Empty ? Unassigned : p.OwnerId.ToString(),
                "start_date" => ByDate(p.StartDate, d.Granularity),
                _ => throw new InvalidOperationException($"No sé agrupar proyectos por «{d.GroupBy}»")
            },
            Value: d.Measure.Equals("count", StringComparison.OrdinalIgnoreCase)
                ? null
                : throw new InvalidOperationException($"No sé calcular «{d.Measure}» sobre proyectos")))
            .ToList();
    }

    #endregion

    #region Componer el resultado

    private static ReportTable Compose(
        string title, ReportDefinition d, DataSource dataSource, List<RawRow> raw)
    {
        var measure = dataSource.Measure(d.Measure)!;
        var isAverage = d.Measure.StartsWith("avg_", StringComparison.OrdinalIgnoreCase);

        var groups = raw
            .GroupBy(f => f.Group)
            .Select(g => new
            {
                Group = g.Key,
                Value = d.Measure.Equals("count", StringComparison.OrdinalIgnoreCase)
                    ? (double?)g.Count()
                    : isAverage
                        // Las filas sin valor se descartan de la media, no se cuentan como cero.
                        // Sin esto, un mes con dos tickets resueltos y treinta abiertos daría una
                        // media de resolución absurdamente baja.
                        //
                        // Y si **ninguna** fila del grupo tiene valor, la media es `null`, no 0:
                        // «0 días medios hasta resolver» dice que se resuelve al instante, que es
                        // lo contrario de «no hay ninguno resuelto». Este proyecto ya tuvo esa
                        // mentira exacta en el tiempo medio de entrega del panel.
                        ? Media(g.Select(x => x.Value))
                        : g.Sum(x => x.Value ?? 0)
            })
            .ToList();

        // **Las fechas se ordenan por fecha; lo demás, por cantidad.**
        //
        // Ordenar siempre por cantidad parece razonable —lo grande primero— y destroza cualquier
        // serie temporal: «tickets por mes» salía 2026-08, 2026-09, 2026-07, y una gráfica de
        // líneas con el eje de tiempo desordenado no significa nada. Se vio al mirar la salida
        // real, no en el código.
        //
        // El orden alfabético vale como orden cronológico porque las claves se escriben con el
        // año delante —«2026-08», «2026-S32»—, que es justo para lo que se eligió ese formato.
        var groupsByDate = dataSource.Field(d.GroupBy)?.Type == FieldType.Date;

        groups = groupsByDate
            ? groups.OrderBy(g => g.Group, StringComparer.Ordinal).ToList()
            : groups.OrderByDescending(g => g.Value ?? double.MinValue).ToList();

        var trimmed = false;

        if (groups.Count > d.EffectiveGroups)
        {
            // La cola se junta en «Otros» en vez de desaparecer: los totales tienen que seguir
            // cuadrando con la lista completa, o el informe miente por omisión. En una media, en
            // cambio, juntar la cola daría un número sin significado, así que ahí se recorta y se
            // dice.
            var head = groups.Take(d.EffectiveGroups).ToList();
            var tail = groups.Skip(d.EffectiveGroups).ToList();

            if (!isAverage)
                head.Add(new { Group = Others, Value = (double?)tail.Sum(g => g.Value ?? 0) });

            groups = head;
            trimmed = true;
        }

        var rows = groups
            .Select(g => (IReadOnlyList<string>)
            [
                g.Group,
                // La raya y no el cero cuando no hay nada que promediar. Ver arriba.
                g.Value is null ? NoData : g.Value.Value.ToString("0.##", Spanish)
            ])
            .ToList();

        return new ReportTable(
            title,
            Subtitle(d, dataSource, measure, raw.Count, trimmed),
            [dataSource.Field(d.GroupBy)!.Name, measure.Name],
            rows);
    }

    /// <summary>
    /// La media de los que tienen valor, o <c>null</c> si no hay ninguno.
    ///
    /// Devolver 0 sería decir que se resuelve al instante cuando la verdad es que nada se ha
    /// resuelto. Son dos afirmaciones distintas y sólo una es cierta.
    /// </summary>
    private static double? Media(IEnumerable<double?> values)
    {
        var withValue = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return withValue.Count == 0 ? null : withValue.Average();
    }

    /// <summary>
    /// La línea que dice de qué va el informe: origen, filtros y cuántas filas lo componen.
    ///
    /// Los filtros aplicados van escritos ahí a propósito. Un informe exportado circula por correo
    /// y acaba en una reunión sin quien lo generó delante; sin esta línea, nadie puede saber si
    /// «Tickets por estado» son todos o sólo los de una persona.
    /// </summary>
    private static string Subtitle(
        ReportDefinition d, DataSource dataSource, AvailableMeasure measure, int rows, bool trimmed)
    {
        var parts = new List<string>
        {
            dataSource.Name,
            $"agrupado por {dataSource.Field(d.GroupBy)!.Name.ToLowerInvariant()}",
            measure.Name.ToLowerInvariant()
        };

        if (d.AppliedFilters.Count > 0)
        {
            var filters = d.AppliedFilters.Select(f =>
            {
                var field = dataSource.Field(f.Field)?.Name ?? f.Field;
                var op = ReportCatalog.Operator(f.Operator);
                return op?.NeedsValue == false
                    ? $"{field} {op.Name.ToLowerInvariant()}"
                    : $"{field} {op?.Name.ToLowerInvariant() ?? f.Operator} {f.Value}";
            });

            parts.Add("filtrado por " + string.Join(" y ", filters));
        }

        parts.Add($"sobre {rows:N0} filas");

        if (trimmed)
            parts.Add($"sólo los {d.EffectiveGroups} mayores");

        return string.Join(" · ", parts)
               + $" · Generado el {DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm", Spanish)} UTC";
    }

    #endregion

    #region Filtros, uno por tipo de campo

    // Cada uno recibe cómo llegar al campo y aplica el operador. Están separados por tipo porque
    // los operadores válidos dependen del tipo, y el catálogo ya lo ha comprobado antes de llegar
    // aquí: lo que queda es traducir.

    /// <summary>El método <c>string.Contains(string)</c>, para construir el «contiene».</summary>
    private static readonly System.Reflection.MethodInfo Contains =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private static IQueryable<T> ApplyText<T>(
        IQueryable<T> query, ReportFilter f, System.Linq.Expressions.Expression<Func<T, string>> field)
    {
        return f.Operator.ToLowerInvariant() switch
        {
            "is" => query.Where(Compare(field, f.Value!, equal: true)),
            "is_not" => query.Where(Compare(field, f.Value!, equal: false)),
            "contains" => query.Where(ContainsText(field, f.Value!)),
            _ => throw new InvalidOperationException($"El operador «{f.Operator}» no vale para texto")
        };
    }

    /// <summary>
    /// «Contiene», construido como árbol de expresión.
    ///
    /// La versión evidente —compilar el acceso al campo y escribir <c>x => acceso(x).Contains(v)</c>—
    /// compila y **falla al ejecutarse**: EF ve una invocación a un delegado y no la sabe traducir
    /// a SQL. El filtro reventaba con «The LINQ expression could not be translated» en cuanto
    /// alguien lo usaba. Construyendo la llamada, se traduce a un LIKE.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<T, bool>> ContainsText<T>(
        System.Linq.Expressions.Expression<Func<T, string>> field, string value)
    {
        var call = System.Linq.Expressions.Expression.Call(
            field.Body, Contains, System.Linq.Expressions.Expression.Constant(value));

        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(call, field.Parameters[0]);
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> Compare<T>(
        System.Linq.Expressions.Expression<Func<T, string>> field, string value, bool equal)
    {
        var parametro = field.Parameters[0];
        var comparison = System.Linq.Expressions.Expression.Equal(
            field.Body, System.Linq.Expressions.Expression.Constant(value));

        System.Linq.Expressions.Expression body = equal
            ? comparison
            : System.Linq.Expressions.Expression.Not(comparison);

        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parametro);
    }

    private static IQueryable<T> ApplyGuid<T>(
        IQueryable<T> query, ReportFilter f, System.Linq.Expressions.Expression<Func<T, Guid>> field)
    {
        var parametro = field.Parameters[0];
        var op = f.Operator.ToLowerInvariant();

        // «Vacío» sobre un identificador que no admite nulos es `Guid.Empty`: así se guarda una
        // tarea sin responsable. Sin esto, el operador —que el catálogo ofrece para este tipo—
        // intentaba interpretar la cadena vacía como identificador y fallaba.
        if (op is "empty" or "not_empty")
        {
            var isEmpty = System.Linq.Expressions.Expression.Equal(
                field.Body, System.Linq.Expressions.Expression.Constant(Guid.Empty));

            System.Linq.Expressions.Expression condition = op == "empty"
                ? isEmpty
                : System.Linq.Expressions.Expression.Not(isEmpty);

            return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(condition, parametro));
        }

        if (!Guid.TryParse(f.Value, out var value))
            throw new InvalidOperationException($"«{f.Value}» no es un identificador válido para {f.Field}");

        var comparison = System.Linq.Expressions.Expression.Equal(
            field.Body, System.Linq.Expressions.Expression.Constant(value));

        System.Linq.Expressions.Expression body = op == "is_not"
            ? System.Linq.Expressions.Expression.Not(comparison)
            : comparison;

        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parametro));
    }

    private static IQueryable<T> ApplyNullableGuid<T>(
        IQueryable<T> query, ReportFilter f, System.Linq.Expressions.Expression<Func<T, Guid?>> field)
    {
        var parametro = field.Parameters[0];
        var isNull = System.Linq.Expressions.Expression.Constant(null, typeof(Guid?));

        System.Linq.Expressions.Expression body = f.Operator.ToLowerInvariant() switch
        {
            "empty" => System.Linq.Expressions.Expression.Equal(field.Body, isNull),
            "not_empty" => System.Linq.Expressions.Expression.NotEqual(field.Body, isNull),
            "is" or "is_not" => Equality(field.Body, f),
            _ => throw new InvalidOperationException($"El operador «{f.Operator}» no vale para {f.Field}")
        };

        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parametro));

        static System.Linq.Expressions.Expression Equality(System.Linq.Expressions.Expression body, ReportFilter f)
        {
            if (!Guid.TryParse(f.Value, out var value))
                throw new InvalidOperationException($"«{f.Value}» no es un identificador válido para {f.Field}");

            var comparison = System.Linq.Expressions.Expression.Equal(
                body, System.Linq.Expressions.Expression.Constant((Guid?)value, typeof(Guid?)));

            return f.Operator.Equals("is_not", StringComparison.OrdinalIgnoreCase)
                ? System.Linq.Expressions.Expression.Not(comparison)
                : comparison;
        }
    }

    private static IQueryable<T> ApplyNumber<T>(
        IQueryable<T> query, ReportFilter f, System.Linq.Expressions.Expression<Func<T, double>> field)
    {
        if (!double.TryParse(f.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            && !double.TryParse(f.Value, NumberStyles.Any, Spanish, out value))
        {
            throw new InvalidOperationException($"«{f.Value}» no es un número");
        }

        var parametro = field.Parameters[0];
        var constant = System.Linq.Expressions.Expression.Constant(value);

        System.Linq.Expressions.Expression body = f.Operator.ToLowerInvariant() switch
        {
            "greater_than" => System.Linq.Expressions.Expression.GreaterThan(field.Body, constant),
            "less_than" => System.Linq.Expressions.Expression.LessThan(field.Body, constant),
            "is" => System.Linq.Expressions.Expression.Equal(field.Body, constant),
            "is_not" => System.Linq.Expressions.Expression.NotEqual(field.Body, constant),
            _ => throw new InvalidOperationException($"El operador «{f.Operator}» no vale para un número")
        };

        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parametro));
    }

    private static IQueryable<T> ApplyDate<T>(
        IQueryable<T> query, ReportFilter f, System.Linq.Expressions.Expression<Func<T, DateTime>> field)
    {
        var value = ParseDate(f);
        var parametro = field.Parameters[0];
        var constant = System.Linq.Expressions.Expression.Constant(value);

        System.Linq.Expressions.Expression body = f.Operator.ToLowerInvariant() switch
        {
            "greater_than" => System.Linq.Expressions.Expression.GreaterThan(field.Body, constant),
            "less_than" => System.Linq.Expressions.Expression.LessThan(field.Body, constant),
            _ => throw new InvalidOperationException($"El operador «{f.Operator}» no vale para una fecha obligatoria")
        };

        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parametro));
    }

    private static IQueryable<T> ApplyNullableDate<T>(
        IQueryable<T> query, ReportFilter f, System.Linq.Expressions.Expression<Func<T, DateTime?>> field)
    {
        var parametro = field.Parameters[0];
        var isNull = System.Linq.Expressions.Expression.Constant(null, typeof(DateTime?));

        System.Linq.Expressions.Expression body = f.Operator.ToLowerInvariant() switch
        {
            "empty" => System.Linq.Expressions.Expression.Equal(field.Body, isNull),
            "not_empty" => System.Linq.Expressions.Expression.NotEqual(field.Body, isNull),
            "greater_than" => System.Linq.Expressions.Expression.GreaterThan(
                field.Body, System.Linq.Expressions.Expression.Constant((DateTime?)ParseDate(f), typeof(DateTime?))),
            "less_than" => System.Linq.Expressions.Expression.LessThan(
                field.Body, System.Linq.Expressions.Expression.Constant((DateTime?)ParseDate(f), typeof(DateTime?))),
            _ => throw new InvalidOperationException($"El operador «{f.Operator}» no vale para una fecha")
        };

        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parametro));
    }

    private static DateTime ParseDate(ReportFilter f)
    {
        // Se admite ISO y el formato español, porque el valor puede venir del constructor de la
        // pantalla o de una definición escrita a mano. Se fija UTC: sin eso, la misma definición
        // filtraría distinto según la zona horaria del servidor.
        if (DateTime.TryParse(f.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var iso))
            return iso;

        if (DateTime.TryParse(f.Value, Spanish, DateTimeStyles.AdjustToUniversal, out var esp))
            return esp;

        throw new InvalidOperationException($"«{f.Value}» no es una fecha");
    }

    #endregion

    /// <summary>
    /// Cómo se escribe una fecha según la granularity pedida.
    ///
    /// El año va delante en todas para que el orden alfabético coincida con el cronológico: con
    /// «03/2026» antes que «12/2025», la gráfica saldría desordenada y nadie lo achacaría a esto.
    /// </summary>
    private static string ByDate(DateTime date, string? granularity) => granularity switch
    {
        "day" => date.ToString("yyyy-MM-dd", Spanish),
        "week" => $"{ISOWeek.GetYear(date)}-S{ISOWeek.GetWeekOfYear(date):00}",
        "month" => date.ToString("yyyy-MM", Spanish),
        "year" => date.ToString("yyyy", Spanish),
        _ => date.ToString("yyyy-MM", Spanish)
    };

    private static string ByDate(DateOnly date, string? granularity)
        => ByDate(date.ToDateTime(TimeOnly.MinValue), granularity);

    /// <summary>
    /// El número con el que se guarda un estado de ticket, a partir de su nombre.
    ///
    /// Devuelve <c>null</c> si el nombre no existe, y quien lo use debe traducir eso a un error
    /// con nombre: filtrar por un estado inventado devolviendo cero filas sería un informe vacío
    /// sin explicación.
    /// </summary>
    private static int? StatusCode(string name)
        => Ticketing.Domain.ValueObjects.TicketStatus
            .FromName<Ticketing.Domain.ValueObjects.TicketStatus>(name)?.Value;

    private static int? PriorityCode(string name)
        => Ticketing.Domain.ValueObjects.TicketPriority
            .FromName<Ticketing.Domain.ValueObjects.TicketPriority>(name)?.Value;

    /// <summary>
    /// Filtra por un campo que se guarda como número pero se escribe con nombre.
    ///
    /// La traducción ocurre **aquí, sobre el valor del filtro**, no dentro de la consulta: una
    /// llamada a un método dentro de la expresión no la sabe traducir EF y la consulta falla al
    /// ejecutarse.
    /// </summary>
    private static IQueryable<T> ApplyCode<T>(
        IQueryable<T> query,
        ReportFilter f,
        System.Linq.Expressions.Expression<Func<T, int>> field,
        Func<string, int?> toCode)
    {
        var code = toCode(f.Value ?? string.Empty);

        if (code is null)
            throw new InvalidOperationException($"«{f.Value}» no es un valor válido para {f.Field}");

        var parametro = field.Parameters[0];
        var constant = System.Linq.Expressions.Expression.Constant(code.Value);
        var comparison = System.Linq.Expressions.Expression.Equal(field.Body, constant);

        System.Linq.Expressions.Expression body = f.Operator.ToLowerInvariant() switch
        {
            "is" => comparison,
            "is_not" => System.Linq.Expressions.Expression.Not(comparison),

            // «Contiene» sobre un estado se admite en el catálogo porque el campo es de tipo
            // texto, y aquí se resuelve como igualdad: los estados son una lista cerrada, así que
            // «contiene Open» y «es Open» quieren decir lo mismo para quien lo escribe.
            "contains" => comparison,

            _ => throw new InvalidOperationException($"El operador «{f.Operator}» no vale para {f.Field}")
        };

        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parametro));
    }
}
