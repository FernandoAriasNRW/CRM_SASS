using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Projects.Infrastructure.Persistence;
using Reporting.Application.Exportaciones;
using Reporting.Domain.Definicion;
using Ticketing.Infrastructure.Persistence;
using WorkItems.Infrastructure.Persistence;

namespace ApiHost.Reporting;

/// <summary>
/// Traduce una <see cref="DefinicionDeInforme"/> a filas.
///
/// <b>Traduce, no interpreta.</b> Cada pieza de la definición se resuelve con un <c>switch</c>
/// contra <see cref="CatalogoDeInformes"/>; no se compone SQL ni se construyen expresiones desde
/// texto. Es lo que hace que un constructor de informes en manos de los usuarios no sea una vía
/// para ejecutar consultas arbitrarias: lo que no está en el catálogo no llega hasta aquí, y si
/// llegara, caería en el <c>default</c> con un mensaje que dice qué falta.
///
/// <b>Vive en el host</b> por lo mismo que <see cref="ConsultasDelPanel"/> y
/// <see cref="DatosDelInforme"/>: un informe de tareas mira WorkItems y uno de tickets mira
/// Ticketing, y ningún módulo referencia a otro.
///
/// <b>Se agrupa en memoria, no en la base.</b> Es deliberado y tiene un límite escrito: primero
/// se filtra en la base —que es donde está el volumen— y se traen sólo las dos columnas que hacen
/// falta, agrupación y medida. Agrupar en SQL exigiría construir la expresión de agrupación
/// dinámicamente, que es justo lo que este diseño evita. Con el tope de
/// <see cref="DatosDelInforme.FilasMaximas"/> filas, la diferencia no se nota; por encima de eso,
/// habría que escribir un traductor a <c>GroupBy</c> por cada campo del catálogo.
/// </summary>
public sealed class MotorDeInformes(
    WorkItemsDbContext tareasDb,
    TicketingDbContext ticketsDb,
    ProjectsDbContext proyectosDb) : global::Reporting.Application.Definiciones.IResolutorDeInformes
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Lo que se escribe cuando un grupo no tiene valor: sin responsable, sin fecha.</summary>
    private const string SinValor = "(sin asignar)";

    /// <summary>Donde va a parar la cola cuando hay más grupos de los que caben.</summary>
    private const string Otros = "Otros";

    /// <summary>Lo que se escribe cuando no hay nada que promediar. Nunca un cero.</summary>
    private const string SinDato = "—";

    /// <summary>
    /// Los valores admitidos de un campo de lista cerrada.
    ///
    /// Los aporta el host porque viven en otros módulos: los estados de un ticket son de
    /// Ticketing y los de una tarea de WorkItems, y Reporting no referencia a ninguno.
    ///
    /// Salen de los propios objetos de valor del dominio, no de una lista escrita aquí: añadir un
    /// estado nuevo a un ticket lo pone en el constructor de informes sin tocar nada.
    /// </summary>
    public IReadOnlyList<string> ValoresDe(string origen, string campo) => (origen, campo) switch
    {
        ("Tareas", "estado") => WorkItems.Domain.ValueObjects.TaskStatus
            .All().Select(e => e.Value).ToList(),

        ("Tareas", "prioridad") => WorkItems.Domain.ValueObjects.TaskPriority
            .All().Select(p => p.Value).ToList(),

        ("Tickets", "estado") => Ticketing.Domain.ValueObjects.TicketStatus
            .All().Select(e => e.Name).ToList(),

        ("Tickets", "prioridad") => Ticketing.Domain.ValueObjects.TicketPriority
            .All().Select(p => p.Name).ToList(),

        // ProjectStatus no expone `All()` propio; se usa el `GetAll` de la enumeración base, que
        // es de donde salen los demás. Así, un estado nuevo aparece aquí solo.
        ("Proyectos", "estado") => Projects.Domain.ValueObjects.ProjectStatus
            .GetAll<Projects.Domain.ValueObjects.ProjectStatus>().Select(e => e.Value).ToList(),

        // Lo demás es texto libre, una fecha o un identificador: no hay lista que ofrecer.
        _ => []
    };

    public async Task<TablaDeInforme> ResolveAsync(
        string titulo, Guid tenantId, DefinicionDeInforme definicion, CancellationToken ct)
    {
        var validacion = definicion.Validar();
        if (validacion.IsFailure)
            throw new InvalidOperationException(validacion.Error);

        // El inquilino se declara antes de consultar: el motor lo llama el trabajador de segundo
        // plano, que no tiene petición y por tanto no tiene usuario. Sin esto el filtro global
        // compara contra Guid.Empty y devuelve cero filas sin dar ningún error.
        using var _ = tareasDb.AsTenant(tenantId);
        using var __ = ticketsDb.AsTenant(tenantId);
        using var ___ = proyectosDb.AsTenant(tenantId);

        var origen = CatalogoDeInformes.Origen(definicion.Origen)!;

        var crudo = origen.Clave switch
        {
            "Tareas" => await DeTareasAsync(tenantId, definicion, ct),
            "Tickets" => await DeTicketsAsync(tenantId, definicion, ct),
            "Proyectos" => await DeProyectosAsync(tenantId, definicion, ct),
            _ => throw new InvalidOperationException($"El origen «{origen.Clave}» no tiene motor todavía")
        };

        return Componer(titulo, definicion, origen, crudo);
    }

    /// <summary>
    /// Una fila en bruto: por qué grupo cae y qué número aporta.
    ///
    /// El número es opcional porque el conteo no lo usa, y porque una media sobre filas sin valor
    /// —tickets sin resolver, tareas sin horas— debe ignorarlas en lugar de contarlas como cero.
    /// Contar los ceros bajaría la media y nadie sabría por qué.
    /// </summary>
    private sealed record FilaCruda(string Grupo, double? Valor);

    #region Los tres orígenes

    private async Task<List<FilaCruda>> DeTareasAsync(Guid tenantId, DefinicionDeInforme d, CancellationToken ct)
    {
        var consulta = tareasDb.Tasks.AsNoTracking().Where(t => t.TenantId == tenantId);

        foreach (var filtro in d.FiltrosAplicados)
        {
            consulta = filtro.Campo.ToLowerInvariant() switch
            {
                "estado" => AplicarTexto(consulta, filtro, t => t.Status.Value),
                "prioridad" => AplicarTexto(consulta, filtro, t => t.Priority.Value),
                "responsable" => AplicarGuid(consulta, filtro, t => t.AssigneeId),
                "proyecto" => AplicarGuid(consulta, filtro, t => t.ProjectId),
                "vencimiento" => AplicarFecha(consulta, filtro, t => t.DueDate.ToDateTime(TimeOnly.MinValue)),
                "creacion" => AplicarFecha(consulta, filtro, t => t.CreatedAtUtc),
                "horas" => AplicarNumero(consulta, filtro, t => (double)t.EstimatedHours),
                _ => throw new InvalidOperationException($"No sé filtrar tareas por «{filtro.Campo}»")
            };
        }

        var filas = await consulta
            .Take(DatosDelInforme.FilasMaximas)
            .Select(t => new
            {
                Estado = t.Status.Value,
                Prioridad = t.Priority.Value,
                t.AssigneeId,
                t.ProjectId,
                t.DueDate,
                t.CreatedAtUtc,
                t.EstimatedHours
            })
            .ToListAsync(ct);

        // Los nombres de proyecto se resuelven de una vez y no fila a fila: agrupar por proyecto
        // sobre mil tareas serían mil consultas.
        var nombresDeProyecto = d.Agrupacion.Equals("proyecto", StringComparison.OrdinalIgnoreCase)
            ? await proyectosDb.Projects.AsNoTracking()
                .Where(p => p.TenantId == tenantId)
                .ToDictionaryAsync(p => p.Id, p => p.Name.Value, ct)
            : [];

        return filas.Select(t => new FilaCruda(
            Grupo: d.Agrupacion.ToLowerInvariant() switch
            {
                "estado" => t.Estado,
                "prioridad" => t.Prioridad,
                "responsable" => t.AssigneeId == Guid.Empty ? SinValor : t.AssigneeId.ToString(),
                "proyecto" => nombresDeProyecto.GetValueOrDefault(t.ProjectId, SinValor),
                "vencimiento" => PorFecha(t.DueDate.ToDateTime(TimeOnly.MinValue), d.Granularidad),
                "creacion" => PorFecha(t.CreatedAtUtc, d.Granularidad),
                "horas" => t.EstimatedHours.ToString("0.##", Espanol),
                _ => throw new InvalidOperationException($"No sé agrupar tareas por «{d.Agrupacion}»")
            },
            Valor: d.Medida.ToLowerInvariant() switch
            {
                "conteo" => null,
                "suma_horas" or "media_horas" => (double)t.EstimatedHours,
                _ => throw new InvalidOperationException($"No sé calcular «{d.Medida}» sobre tareas")
            }))
            .ToList();
    }

    private async Task<List<FilaCruda>> DeTicketsAsync(Guid tenantId, DefinicionDeInforme d, CancellationToken ct)
    {
        var consulta = ticketsDb.Tickets.AsNoTracking().Where(t => t.TenantId == tenantId);

        foreach (var filtro in d.FiltrosAplicados)
        {
            consulta = filtro.Campo.ToLowerInvariant() switch
            {
                // El estado y la prioridad se guardan como número. **Se traduce el valor que
                // llega, no la columna**: meter la traducción dentro de la consulta
                // —`t => EstadoDeTicket(t.StatusValue)`— parece lo natural y EF no sabe traducir
                // esa llamada a SQL, así que la consulta reventaba en cuanto alguien filtraba por
                // estado. Comparando contra el número, el filtro se traduce solo.
                "estado" => AplicarCodigo(consulta, filtro, t => t.StatusValue, CodigoDeEstado),
                "prioridad" => AplicarCodigo(consulta, filtro, t => t.PriorityValue, CodigoDePrioridad),
                "agente" => AplicarGuidNulo(consulta, filtro, t => t.AssignedAgentId),
                "creacion" => AplicarFecha(consulta, filtro, t => t.CreatedAt),
                "resolucion" => AplicarFechaNula(consulta, filtro, t => t.ResolvedAt),
                _ => throw new InvalidOperationException($"No sé filtrar tickets por «{filtro.Campo}»")
            };
        }

        var filas = await consulta
            .Take(DatosDelInforme.FilasMaximas)
            .Select(t => new { t.StatusValue, t.PriorityValue, t.AssignedAgentId, t.CreatedAt, t.ResolvedAt })
            .ToListAsync(ct);

        return filas.Select(t => new FilaCruda(
            Grupo: d.Agrupacion.ToLowerInvariant() switch
            {
                "estado" => Ticketing.Domain.ValueObjects.TicketStatus
                    .FromValue<Ticketing.Domain.ValueObjects.TicketStatus>(t.StatusValue).Name,
                "prioridad" => Ticketing.Domain.ValueObjects.TicketPriority
                    .FromValue<Ticketing.Domain.ValueObjects.TicketPriority>(t.PriorityValue).Name,
                // Un ticket sin agente y otro con el Guid vacío son lo mismo para quien lee el
                // informe: nadie lo lleva. Se juntan en un solo grupo en vez de dar dos filas que
                // significan lo mismo.
                "agente" => t.AssignedAgentId is null || t.AssignedAgentId == Guid.Empty
                    ? SinValor
                    : t.AssignedAgentId.Value.ToString(),
                "creacion" => PorFecha(t.CreatedAt, d.Granularidad),
                "resolucion" => t.ResolvedAt is null ? SinValor : PorFecha(t.ResolvedAt.Value, d.Granularidad),
                _ => throw new InvalidOperationException($"No sé agrupar tickets por «{d.Agrupacion}»")
            },
            Valor: d.Medida.ToLowerInvariant() switch
            {
                "conteo" => null,

                // Sólo cuentan los resueltos. Meter los abiertos con los días que llevan mezclaría
                // «cuánto se tarda en resolver» con «cuánto lleva esperando esto», que son dos
                // preguntas distintas y la media de las dos no contesta ninguna.
                "media_dias_resolucion" => t.ResolvedAt is null
                    ? null
                    : (t.ResolvedAt.Value - t.CreatedAt).TotalDays,

                _ => throw new InvalidOperationException($"No sé calcular «{d.Medida}» sobre tickets")
            }))
            .ToList();
    }

    private async Task<List<FilaCruda>> DeProyectosAsync(Guid tenantId, DefinicionDeInforme d, CancellationToken ct)
    {
        var consulta = proyectosDb.Projects.AsNoTracking().Where(p => p.TenantId == tenantId);

        foreach (var filtro in d.FiltrosAplicados)
        {
            consulta = filtro.Campo.ToLowerInvariant() switch
            {
                "estado" => AplicarTexto(consulta, filtro, p => p.Status.Value),
                "dueno" => AplicarGuid(consulta, filtro, p => p.OwnerId),
                // StartDate es DateOnly; se convierte para poder comparar con la fecha del filtro
                // en la propia consulta.
                "inicio" => AplicarFecha(consulta, filtro, p => p.StartDate.ToDateTime(TimeOnly.MinValue)),
                _ => throw new InvalidOperationException($"No sé filtrar proyectos por «{filtro.Campo}»")
            };
        }

        var filas = await consulta
            .Take(DatosDelInforme.FilasMaximas)
            .Select(p => new { Estado = p.Status.Value, p.OwnerId, p.StartDate })
            .ToListAsync(ct);

        return filas.Select(p => new FilaCruda(
            Grupo: d.Agrupacion.ToLowerInvariant() switch
            {
                "estado" => p.Estado,
                "dueno" => p.OwnerId == Guid.Empty ? SinValor : p.OwnerId.ToString(),
                "inicio" => PorFecha(p.StartDate, d.Granularidad),
                _ => throw new InvalidOperationException($"No sé agrupar proyectos por «{d.Agrupacion}»")
            },
            Valor: d.Medida.Equals("conteo", StringComparison.OrdinalIgnoreCase)
                ? null
                : throw new InvalidOperationException($"No sé calcular «{d.Medida}» sobre proyectos")))
            .ToList();
    }

    #endregion

    #region Componer el resultado

    private static TablaDeInforme Componer(
        string titulo, DefinicionDeInforme d, OrigenDeDatos origen, List<FilaCruda> crudo)
    {
        var medida = origen.Medida(d.Medida)!;
        var esMedia = d.Medida.StartsWith("media", StringComparison.OrdinalIgnoreCase);

        var grupos = crudo
            .GroupBy(f => f.Grupo)
            .Select(g => new
            {
                Grupo = g.Key,
                Valor = d.Medida.Equals("conteo", StringComparison.OrdinalIgnoreCase)
                    ? (double?)g.Count()
                    : esMedia
                        // Las filas sin valor se descartan de la media, no se cuentan como cero.
                        // Sin esto, un mes con dos tickets resueltos y treinta abiertos daría una
                        // media de resolución absurdamente baja.
                        //
                        // Y si **ninguna** fila del grupo tiene valor, la media es `null`, no 0:
                        // «0 días medios hasta resolver» dice que se resuelve al instante, que es
                        // lo contrario de «no hay ninguno resuelto». Este proyecto ya tuvo esa
                        // mentira exacta en el tiempo medio de entrega del panel.
                        ? Media(g.Select(x => x.Valor))
                        : g.Sum(x => x.Valor ?? 0)
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
        var agrupaPorFecha = origen.Campo(d.Agrupacion)?.Tipo == TipoDeCampo.Fecha;

        grupos = agrupaPorFecha
            ? grupos.OrderBy(g => g.Grupo, StringComparer.Ordinal).ToList()
            : grupos.OrderByDescending(g => g.Valor ?? double.MinValue).ToList();

        var recortado = false;

        if (grupos.Count > d.GruposEfectivos)
        {
            // La cola se junta en «Otros» en vez de desaparecer: los totales tienen que seguir
            // cuadrando con la lista completa, o el informe miente por omisión. En una media, en
            // cambio, juntar la cola daría un número sin significado, así que ahí se recorta y se
            // dice.
            var cabeza = grupos.Take(d.GruposEfectivos).ToList();
            var cola = grupos.Skip(d.GruposEfectivos).ToList();

            if (!esMedia)
                cabeza.Add(new { Grupo = Otros, Valor = (double?)cola.Sum(g => g.Valor ?? 0) });

            grupos = cabeza;
            recortado = true;
        }

        var filas = grupos
            .Select(g => (IReadOnlyList<string>)
            [
                g.Grupo,
                // La raya y no el cero cuando no hay nada que promediar. Ver arriba.
                g.Valor is null ? SinDato : g.Valor.Value.ToString("0.##", Espanol)
            ])
            .ToList();

        return new TablaDeInforme(
            titulo,
            Subtitulo(d, origen, medida, crudo.Count, recortado),
            [origen.Campo(d.Agrupacion)!.Nombre, medida.Nombre],
            filas);
    }

    /// <summary>
    /// La media de los que tienen valor, o <c>null</c> si no hay ninguno.
    ///
    /// Devolver 0 sería decir que se resuelve al instante cuando la verdad es que nada se ha
    /// resuelto. Son dos afirmaciones distintas y sólo una es cierta.
    /// </summary>
    private static double? Media(IEnumerable<double?> valores)
    {
        var conValor = valores.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return conValor.Count == 0 ? null : conValor.Average();
    }

    /// <summary>
    /// La línea que dice de qué va el informe: origen, filtros y cuántas filas lo componen.
    ///
    /// Los filtros aplicados van escritos ahí a propósito. Un informe exportado circula por correo
    /// y acaba en una reunión sin quien lo generó delante; sin esta línea, nadie puede saber si
    /// «Tickets por estado» son todos o sólo los de una persona.
    /// </summary>
    private static string Subtitulo(
        DefinicionDeInforme d, OrigenDeDatos origen, MedidaDisponible medida, int filas, bool recortado)
    {
        var partes = new List<string>
        {
            origen.Nombre,
            $"agrupado por {origen.Campo(d.Agrupacion)!.Nombre.ToLowerInvariant()}",
            medida.Nombre.ToLowerInvariant()
        };

        if (d.FiltrosAplicados.Count > 0)
        {
            var filtros = d.FiltrosAplicados.Select(f =>
            {
                var campo = origen.Campo(f.Campo)?.Nombre ?? f.Campo;
                var operador = CatalogoDeInformes.Operador(f.Operador);
                return operador?.NecesitaValor == false
                    ? $"{campo} {operador.Nombre.ToLowerInvariant()}"
                    : $"{campo} {operador?.Nombre.ToLowerInvariant() ?? f.Operador} {f.Valor}";
            });

            partes.Add("filtrado por " + string.Join(" y ", filtros));
        }

        partes.Add($"sobre {filas:N0} filas");

        if (recortado)
            partes.Add($"sólo los {d.GruposEfectivos} mayores");

        return string.Join(" · ", partes)
               + $" · Generado el {DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm", Espanol)} UTC";
    }

    #endregion

    #region Filtros, uno por tipo de campo

    // Cada uno recibe cómo llegar al campo y aplica el operador. Están separados por tipo porque
    // los operadores válidos dependen del tipo, y el catálogo ya lo ha comprobado antes de llegar
    // aquí: lo que queda es traducir.

    /// <summary>El método <c>string.Contains(string)</c>, para construir el «contiene».</summary>
    private static readonly System.Reflection.MethodInfo Contiene =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private static IQueryable<T> AplicarTexto<T>(
        IQueryable<T> consulta, FiltroDeInforme f, System.Linq.Expressions.Expression<Func<T, string>> campo)
    {
        return f.Operador.ToLowerInvariant() switch
        {
            "es" => consulta.Where(Comparar(campo, f.Valor!, igual: true)),
            "no_es" => consulta.Where(Comparar(campo, f.Valor!, igual: false)),
            "contiene" => consulta.Where(ContieneTexto(campo, f.Valor!)),
            _ => throw new InvalidOperationException($"El operador «{f.Operador}» no vale para texto")
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
    private static System.Linq.Expressions.Expression<Func<T, bool>> ContieneTexto<T>(
        System.Linq.Expressions.Expression<Func<T, string>> campo, string valor)
    {
        var llamada = System.Linq.Expressions.Expression.Call(
            campo.Body, Contiene, System.Linq.Expressions.Expression.Constant(valor));

        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(llamada, campo.Parameters[0]);
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> Comparar<T>(
        System.Linq.Expressions.Expression<Func<T, string>> campo, string valor, bool igual)
    {
        var parametro = campo.Parameters[0];
        var comparacion = System.Linq.Expressions.Expression.Equal(
            campo.Body, System.Linq.Expressions.Expression.Constant(valor));

        System.Linq.Expressions.Expression cuerpo = igual
            ? comparacion
            : System.Linq.Expressions.Expression.Not(comparacion);

        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(cuerpo, parametro);
    }

    private static IQueryable<T> AplicarGuid<T>(
        IQueryable<T> consulta, FiltroDeInforme f, System.Linq.Expressions.Expression<Func<T, Guid>> campo)
    {
        var parametro = campo.Parameters[0];
        var operador = f.Operador.ToLowerInvariant();

        // «Vacío» sobre un identificador que no admite nulos es `Guid.Empty`: así se guarda una
        // tarea sin responsable. Sin esto, el operador —que el catálogo ofrece para este tipo—
        // intentaba interpretar la cadena vacía como identificador y fallaba.
        if (operador is "vacio" or "no_vacio")
        {
            var esVacio = System.Linq.Expressions.Expression.Equal(
                campo.Body, System.Linq.Expressions.Expression.Constant(Guid.Empty));

            System.Linq.Expressions.Expression condicion = operador == "vacio"
                ? esVacio
                : System.Linq.Expressions.Expression.Not(esVacio);

            return consulta.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(condicion, parametro));
        }

        if (!Guid.TryParse(f.Valor, out var valor))
            throw new InvalidOperationException($"«{f.Valor}» no es un identificador válido para {f.Campo}");

        var comparacion = System.Linq.Expressions.Expression.Equal(
            campo.Body, System.Linq.Expressions.Expression.Constant(valor));

        System.Linq.Expressions.Expression cuerpo = operador == "no_es"
            ? System.Linq.Expressions.Expression.Not(comparacion)
            : comparacion;

        return consulta.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(cuerpo, parametro));
    }

    private static IQueryable<T> AplicarGuidNulo<T>(
        IQueryable<T> consulta, FiltroDeInforme f, System.Linq.Expressions.Expression<Func<T, Guid?>> campo)
    {
        var parametro = campo.Parameters[0];
        var nulo = System.Linq.Expressions.Expression.Constant(null, typeof(Guid?));

        System.Linq.Expressions.Expression cuerpo = f.Operador.ToLowerInvariant() switch
        {
            "vacio" => System.Linq.Expressions.Expression.Equal(campo.Body, nulo),
            "no_vacio" => System.Linq.Expressions.Expression.NotEqual(campo.Body, nulo),
            "es" or "no_es" => Igualdad(campo.Body, f),
            _ => throw new InvalidOperationException($"El operador «{f.Operador}» no vale para {f.Campo}")
        };

        return consulta.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(cuerpo, parametro));

        static System.Linq.Expressions.Expression Igualdad(System.Linq.Expressions.Expression cuerpo, FiltroDeInforme f)
        {
            if (!Guid.TryParse(f.Valor, out var valor))
                throw new InvalidOperationException($"«{f.Valor}» no es un identificador válido para {f.Campo}");

            var comparacion = System.Linq.Expressions.Expression.Equal(
                cuerpo, System.Linq.Expressions.Expression.Constant((Guid?)valor, typeof(Guid?)));

            return f.Operador.Equals("no_es", StringComparison.OrdinalIgnoreCase)
                ? System.Linq.Expressions.Expression.Not(comparacion)
                : comparacion;
        }
    }

    private static IQueryable<T> AplicarNumero<T>(
        IQueryable<T> consulta, FiltroDeInforme f, System.Linq.Expressions.Expression<Func<T, double>> campo)
    {
        if (!double.TryParse(f.Valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var valor)
            && !double.TryParse(f.Valor, NumberStyles.Any, Espanol, out valor))
        {
            throw new InvalidOperationException($"«{f.Valor}» no es un número");
        }

        var parametro = campo.Parameters[0];
        var constante = System.Linq.Expressions.Expression.Constant(valor);

        System.Linq.Expressions.Expression cuerpo = f.Operador.ToLowerInvariant() switch
        {
            "mayor_que" => System.Linq.Expressions.Expression.GreaterThan(campo.Body, constante),
            "menor_que" => System.Linq.Expressions.Expression.LessThan(campo.Body, constante),
            "es" => System.Linq.Expressions.Expression.Equal(campo.Body, constante),
            "no_es" => System.Linq.Expressions.Expression.NotEqual(campo.Body, constante),
            _ => throw new InvalidOperationException($"El operador «{f.Operador}» no vale para un número")
        };

        return consulta.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(cuerpo, parametro));
    }

    private static IQueryable<T> AplicarFecha<T>(
        IQueryable<T> consulta, FiltroDeInforme f, System.Linq.Expressions.Expression<Func<T, DateTime>> campo)
    {
        var valor = LeerFecha(f);
        var parametro = campo.Parameters[0];
        var constante = System.Linq.Expressions.Expression.Constant(valor);

        System.Linq.Expressions.Expression cuerpo = f.Operador.ToLowerInvariant() switch
        {
            "mayor_que" => System.Linq.Expressions.Expression.GreaterThan(campo.Body, constante),
            "menor_que" => System.Linq.Expressions.Expression.LessThan(campo.Body, constante),
            _ => throw new InvalidOperationException($"El operador «{f.Operador}» no vale para una fecha obligatoria")
        };

        return consulta.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(cuerpo, parametro));
    }

    private static IQueryable<T> AplicarFechaNula<T>(
        IQueryable<T> consulta, FiltroDeInforme f, System.Linq.Expressions.Expression<Func<T, DateTime?>> campo)
    {
        var parametro = campo.Parameters[0];
        var nulo = System.Linq.Expressions.Expression.Constant(null, typeof(DateTime?));

        System.Linq.Expressions.Expression cuerpo = f.Operador.ToLowerInvariant() switch
        {
            "vacio" => System.Linq.Expressions.Expression.Equal(campo.Body, nulo),
            "no_vacio" => System.Linq.Expressions.Expression.NotEqual(campo.Body, nulo),
            "mayor_que" => System.Linq.Expressions.Expression.GreaterThan(
                campo.Body, System.Linq.Expressions.Expression.Constant((DateTime?)LeerFecha(f), typeof(DateTime?))),
            "menor_que" => System.Linq.Expressions.Expression.LessThan(
                campo.Body, System.Linq.Expressions.Expression.Constant((DateTime?)LeerFecha(f), typeof(DateTime?))),
            _ => throw new InvalidOperationException($"El operador «{f.Operador}» no vale para una fecha")
        };

        return consulta.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(cuerpo, parametro));
    }

    private static DateTime LeerFecha(FiltroDeInforme f)
    {
        // Se admite ISO y el formato español, porque el valor puede venir del constructor de la
        // pantalla o de una definición escrita a mano. Se fija UTC: sin eso, la misma definición
        // filtraría distinto según la zona horaria del servidor.
        if (DateTime.TryParse(f.Valor, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var iso))
            return iso;

        if (DateTime.TryParse(f.Valor, Espanol, DateTimeStyles.AdjustToUniversal, out var esp))
            return esp;

        throw new InvalidOperationException($"«{f.Valor}» no es una fecha");
    }

    #endregion

    /// <summary>
    /// Cómo se escribe una fecha según la granularidad pedida.
    ///
    /// El año va delante en todas para que el orden alfabético coincida con el cronológico: con
    /// «03/2026» antes que «12/2025», la gráfica saldría desordenada y nadie lo achacaría a esto.
    /// </summary>
    private static string PorFecha(DateTime fecha, string? granularidad) => granularidad switch
    {
        "dia" => fecha.ToString("yyyy-MM-dd", Espanol),
        "semana" => $"{ISOWeek.GetYear(fecha)}-S{ISOWeek.GetWeekOfYear(fecha):00}",
        "mes" => fecha.ToString("yyyy-MM", Espanol),
        "ano" => fecha.ToString("yyyy", Espanol),
        _ => fecha.ToString("yyyy-MM", Espanol)
    };

    private static string PorFecha(DateOnly fecha, string? granularidad)
        => PorFecha(fecha.ToDateTime(TimeOnly.MinValue), granularidad);

    /// <summary>
    /// El número con el que se guarda un estado de ticket, a partir de su nombre.
    ///
    /// Devuelve <c>null</c> si el nombre no existe, y quien lo use debe traducir eso a un error
    /// con nombre: filtrar por un estado inventado devolviendo cero filas sería un informe vacío
    /// sin explicación.
    /// </summary>
    private static int? CodigoDeEstado(string nombre)
        => Ticketing.Domain.ValueObjects.TicketStatus
            .FromName<Ticketing.Domain.ValueObjects.TicketStatus>(nombre)?.Value;

    private static int? CodigoDePrioridad(string nombre)
        => Ticketing.Domain.ValueObjects.TicketPriority
            .FromName<Ticketing.Domain.ValueObjects.TicketPriority>(nombre)?.Value;

    /// <summary>
    /// Filtra por un campo que se guarda como número pero se escribe con nombre.
    ///
    /// La traducción ocurre **aquí, sobre el valor del filtro**, no dentro de la consulta: una
    /// llamada a un método dentro de la expresión no la sabe traducir EF y la consulta falla al
    /// ejecutarse.
    /// </summary>
    private static IQueryable<T> AplicarCodigo<T>(
        IQueryable<T> consulta,
        FiltroDeInforme f,
        System.Linq.Expressions.Expression<Func<T, int>> campo,
        Func<string, int?> aCodigo)
    {
        var codigo = aCodigo(f.Valor ?? string.Empty);

        if (codigo is null)
            throw new InvalidOperationException($"«{f.Valor}» no es un valor válido para {f.Campo}");

        var parametro = campo.Parameters[0];
        var constante = System.Linq.Expressions.Expression.Constant(codigo.Value);
        var comparacion = System.Linq.Expressions.Expression.Equal(campo.Body, constante);

        System.Linq.Expressions.Expression cuerpo = f.Operador.ToLowerInvariant() switch
        {
            "es" => comparacion,
            "no_es" => System.Linq.Expressions.Expression.Not(comparacion),

            // «Contiene» sobre un estado se admite en el catálogo porque el campo es de tipo
            // texto, y aquí se resuelve como igualdad: los estados son una lista cerrada, así que
            // «contiene Open» y «es Open» quieren decir lo mismo para quien lo escribe.
            "contiene" => comparacion,

            _ => throw new InvalidOperationException($"El operador «{f.Operador}» no vale para {f.Campo}")
        };

        return consulta.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(cuerpo, parametro));
    }
}
