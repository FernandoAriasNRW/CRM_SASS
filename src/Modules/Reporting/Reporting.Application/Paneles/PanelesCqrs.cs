using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Application.Dashboards;
using Reporting.Application.Definiciones;
using Reporting.Domain.Entities;
using Reporting.Domain.Paneles;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Paneles;

public sealed record PanelDto(
    Guid Id,
    string Titulo,
    bool EsMio,
    bool EsPublico,
    IReadOnlyList<Widget> Widgets);

/// <summary>
/// El panel propio de quien pregunta, creándolo si es su primera vez.
///
/// <b>Crearlo aquí y no en un proceso de alta</b> porque una persona puede existir desde antes de
/// que el panel existiera: un alta que rellene paneles sólo cubre a quien se dé de alta a partir
/// de mañana, y todos los demás verían una pantalla vacía sin saber por qué.
/// </summary>
public sealed record GetMiPanelQuery(Guid TenantId, Guid UserId) : IQuery<PanelDto>;

/// <summary>Guarda dónde está cada recuadro. Lo manda la pantalla al mover o redimensionar.</summary>
public sealed record GuardarDisposicionCommand(
    Guid TenantId, Guid UserId, Guid PanelId, IReadOnlyList<Widget> Widgets) : ICommand<bool>;

/// <summary>Quita un recuadro del panel. El informe al que apunta no se toca.</summary>
public sealed record QuitarWidgetCommand(Guid TenantId, Guid UserId, Guid PanelId, Guid WidgetId) : ICommand<bool>;

/// <summary>Pone un informe existente en el panel, abajo del todo.</summary>
public sealed record AnadirWidgetCommand(
    Guid TenantId, Guid UserId, Guid PanelId, Guid ReportId, string? Forma) : ICommand<Widget>;

public sealed class GetMiPanelHandler(
    ICustomDashboardRepository paneles,
    IReportRepository informes,
    IReportingUnitOfWork unitOfWork) : IQueryHandler<GetMiPanelQuery, PanelDto>
{
    public async Task<Result<PanelDto>> Handle(GetMiPanelQuery request, CancellationToken ct)
    {
        var mios = await paneles.GetDashboardsAsync(request.TenantId, request.UserId, ct);

        var mio = mios.FirstOrDefault(p => p.IsDefault && p.CreatedById == request.UserId);

        if (mio is not null)
            return Result<PanelDto>.Success(ADto(mio, request.UserId));

        // Primera vez: se crea el panel con los informes de partida. Cada uno es un informe de
        // verdad, guardado y editable, no un recuadro fijo: quien no lo quiera lo cambia o lo
        // quita, en vez de mirar algo que no puede tocar.
        var nuevo = Dashboard.CrearPanelPersonal(request.TenantId, request.UserId);

        var creados = new List<(Guid ReportId, PanelDeInicio.Sugerido Informe)>();

        foreach (var sugerido in PanelDeInicio.Informes())
        {
            var informe = Report.Create(
                request.TenantId, request.UserId, sugerido.Nombre,
                ReportType.Custom, ReportFormat.Csv);

            if (informe.IsFailure) continue;

            var definido = informe.Value!.DefinirAMedida(sugerido.Definicion);

            // Un informe de partida que no valide es un fallo nuestro, no del usuario. Se salta
            // en vez de tumbar la creación del panel: es preferible un panel con cinco recuadros
            // que una pantalla que no abre. La prueba que recorre esta lista lo vigila.
            if (definido.IsFailure) continue;

            await informes.AddAsync(informe.Value!, ct);
            creados.Add((informe.Value!.Id, sugerido));
        }

        var colocacion = nuevo.Colocar(new DisposicionDelPanel(PanelDeInicio.Colocar(creados)));
        if (colocacion.IsFailure)
            return Result<PanelDto>.Failure(colocacion.Error!);

        await paneles.AddAsync(nuevo, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<PanelDto>.Success(ADto(nuevo, request.UserId));
    }

    internal static PanelDto ADto(Dashboard panel, Guid userId) => new(
        panel.Id, panel.Title, panel.CreatedById == userId, panel.IsPublic,
        panel.LeerDisposicion().Colocados);
}

public sealed class GuardarDisposicionHandler(
    ICustomDashboardRepository paneles,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<GuardarDisposicionCommand, bool>
{
    public async Task<Result<bool>> Handle(GuardarDisposicionCommand request, CancellationToken ct)
    {
        var panel = await paneles.GetByIdAsync(request.TenantId, request.PanelId, ct);
        if (panel is null)
            return Result<bool>.Failure("Ese panel no existe");

        // Sólo su dueño lo recoloca. Sin esto, un panel compartido lo movería cualquiera que lo
        // abriese, y quien lo montó vería su pantalla cambiada sin haber tocado nada.
        if (panel.CreatedById != request.UserId)
            return Result<bool>.Failure("Este panel es de otra persona");

        var resultado = panel.Colocar(new DisposicionDelPanel(request.Widgets));
        if (resultado.IsFailure)
            return Result<bool>.Failure(resultado.Error!);

        await paneles.UpdateAsync(panel, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

public sealed class AnadirWidgetHandler(
    ICustomDashboardRepository paneles,
    IReportRepository informes,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<AnadirWidgetCommand, Widget>
{
    public async Task<Result<Widget>> Handle(AnadirWidgetCommand request, CancellationToken ct)
    {
        var panel = await paneles.GetByIdAsync(request.TenantId, request.PanelId, ct);
        if (panel is null)
            return Result<Widget>.Failure("Ese panel no existe");

        if (panel.CreatedById != request.UserId)
            return Result<Widget>.Failure("Este panel es de otra persona");

        var informe = await informes.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (informe is null)
            return Result<Widget>.Failure("El informe no existe");

        var actuales = panel.LeerDisposicion().Colocados;

        // Va abajo del todo y a media anchura: aparece donde se mira al terminar de añadirlo, sin
        // desplazar nada de lo que ya estaba colocado.
        var siguienteFila = actuales.Count == 0 ? 0 : actuales.Max(w => w.Y + w.Alto);

        var widget = new Widget(
            Id: Guid.NewGuid(),
            ReportId: request.ReportId,
            X: 0, Y: siguienteFila,
            Ancho: 6, Alto: 4,
            Forma: request.Forma ?? informe.LeerDefinicion()?.Forma,
            Titulo: informe.Name);

        var resultado = panel.Colocar(new DisposicionDelPanel([.. actuales, widget]));
        if (resultado.IsFailure)
            return Result<Widget>.Failure(resultado.Error!);

        await paneles.UpdateAsync(panel, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Widget>.Success(widget);
    }
}

public sealed class QuitarWidgetHandler(
    ICustomDashboardRepository paneles,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<QuitarWidgetCommand, bool>
{
    public async Task<Result<bool>> Handle(QuitarWidgetCommand request, CancellationToken ct)
    {
        var panel = await paneles.GetByIdAsync(request.TenantId, request.PanelId, ct);
        if (panel is null)
            return Result<bool>.Failure("Ese panel no existe");

        if (panel.CreatedById != request.UserId)
            return Result<bool>.Failure("Este panel es de otra persona");

        var quedan = panel.LeerDisposicion().Colocados.Where(w => w.Id != request.WidgetId).ToList();

        // Quitar el informe al que apunta sería destruir trabajo por un gesto de colocación:
        // sacar algo del panel no es borrarlo, y el informe sigue en su lista.
        var resultado = panel.Colocar(new DisposicionDelPanel(quedan));
        if (resultado.IsFailure)
            return Result<bool>.Failure(resultado.Error!);

        await paneles.UpdateAsync(panel, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

/// <summary>
/// Los datos de <b>todos</b> los recuadros del panel, en una sola petición.
///
/// <b>Es la segunda decisión del plan, y la razón es de rendimiento y de coherencia a la vez:</b>
/// «un widget que trae sus propios datos con su propia llamada convierte el dashboard en veinte
/// peticiones; una consulta declarada permite pedirlas juntas». Además, pidiéndolas juntas todos
/// los recuadros son de la misma foto: con veinte llamadas escalonadas, el de arriba puede contar
/// tickets de antes de que llegara uno nuevo y el de abajo de después, y los números no cuadran
/// entre sí.
/// </summary>
public sealed record GetDatosDelPanelQuery(Guid TenantId, Guid PanelId) : IQuery<IReadOnlyList<DatosDeWidgetDto>>;

/// <summary>
/// Lo que se pinta en un recuadro, o por qué no se puede.
///
/// El error va <b>por widget</b>, no por panel: un informe roto apaga su recuadro y deja los otros
/// cinco funcionando. Si el fallo tumbara la petición entera, un solo informe mal configurado
/// dejaría la pantalla de inicio en blanco.
/// </summary>
public sealed record DatosDeWidgetDto(
    Guid WidgetId,
    Guid ReportId,
    string Titulo,
    string Forma,
    string? Subtitulo,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyList<string>> Filas,
    string? Error);

public sealed class GetDatosDelPanelHandler(
    ICustomDashboardRepository paneles,
    IReportRepository informes,
    IResolutorDeInformes resolutor) : IQueryHandler<GetDatosDelPanelQuery, IReadOnlyList<DatosDeWidgetDto>>
{
    /// <summary>
    /// Cuántas filas se mandan por recuadro.
    ///
    /// Una gráfica de más de treinta categorías no se lee, y el motor ya junta la cola en «Otros».
    /// El tope existe para que un widget mal configurado —agrupado por un campo con miles de
    /// valores— no mande un megabyte a la pantalla.
    /// </summary>
    private const int FilasPorWidget = 30;

    public async Task<Result<IReadOnlyList<DatosDeWidgetDto>>> Handle(
        GetDatosDelPanelQuery request, CancellationToken ct)
    {
        var panel = await paneles.GetByIdAsync(request.TenantId, request.PanelId, ct);
        if (panel is null)
            return Result<IReadOnlyList<DatosDeWidgetDto>>.Failure("Ese panel no existe");

        var datos = new List<DatosDeWidgetDto>();

        foreach (var widget in panel.LeerDisposicion().Colocados)
        {
            datos.Add(await UnWidgetAsync(request.TenantId, widget, ct));
        }

        return Result<IReadOnlyList<DatosDeWidgetDto>>.Success(datos);
    }

    private async Task<DatosDeWidgetDto> UnWidgetAsync(Guid tenantId, Widget widget, CancellationToken ct)
    {
        var titulo = widget.Titulo ?? "Informe";
        var forma = widget.Forma ?? "tabla";

        var informe = await informes.GetByIdAsync(tenantId, widget.ReportId, ct);

        if (informe is null)
        {
            // El informe se borró y el recuadro se quedó apuntando a nada. Se dice en el propio
            // recuadro para que quien lo vea sepa qué quitar, en vez de dejar un hueco mudo.
            return Vacio(widget, titulo, forma, "El informe de este recuadro ya no existe");
        }

        var definicion = informe.LeerDefinicion();

        if (definicion is null)
        {
            return Vacio(widget, informe.Name, forma,
                "Este informe no está configurado todavía. Ábrelo en el constructor.");
        }

        try
        {
            var tabla = await resolutor.ResolverAsync(informe.Name, tenantId, definicion, ct);

            return new DatosDeWidgetDto(
                widget.Id, widget.ReportId,
                widget.Titulo ?? informe.Name,
                widget.Forma ?? definicion.Forma,
                tabla.Subtitulo, tabla.Columnas,
                tabla.Filas.Take(FilasPorWidget).ToList(),
                Error: null);
        }
        catch (InvalidOperationException ex)
        {
            return Vacio(widget, informe.Name, forma, ex.Message);
        }
    }

    private static DatosDeWidgetDto Vacio(Widget widget, string titulo, string forma, string error)
        => new(widget.Id, widget.ReportId, titulo, forma, null, [], [], error);
}
