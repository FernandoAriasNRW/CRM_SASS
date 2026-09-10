namespace ApiHost.Calendar;

/// <summary>
/// La agenda de un día: eventos, tareas que vencen, tickets abiertos y proyectos que terminan.
///
/// Se mapea desde el host y no desde <c>Calendar.Presentation</c> porque lo que sirve viene de
/// cuatro módulos. Cuelga igualmente de <c>/api/v1/calendar</c>: para quien la consume es parte
/// del calendario, y partirla en otra ruta sólo enseñaría por dónde está cosida por dentro.
/// </summary>
public static class AgendaEndpoints
{
    public static IEndpointRouteBuilder MapAgendaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/calendar").WithTags("Calendar").RequireAuthorization();

        grupo.MapGet("/agenda/{dia:datetime}", async (
            DateTime dia, AgendaDelDia agenda, CancellationToken ct) =>
        {
            // Sólo la parte de fecha. La ruta acepta un `datetime` porque es el único
            // convertidor que trae el enrutador, pero la agenda es de un día: si llegara con
            // hora, «el 8 a las 15:00» y «el 8 a las 09:00» serían dos días distintos.
            return Results.Ok(await agenda.DeAsync(DateOnly.FromDateTime(dia), ct));
        });

        return app;
    }
}
