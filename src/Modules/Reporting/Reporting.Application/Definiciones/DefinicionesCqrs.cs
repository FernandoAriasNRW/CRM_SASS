using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definicion;

namespace Reporting.Application.Definiciones;

/// <summary>
/// Guarda la definición de un informe a medida.
///
/// La definición se valida en el dominio, y este comando sólo la traslada. La razón de que la
/// validación no viva aquí: un informe con una definición que el motor no sabe traducir se
/// guardaría bien y **fallaría al exportarlo**, cuando quien lo construyó ya no está delante.
/// </summary>
public sealed record GuardarDefinicionCommand(
    Guid TenantId,
    Guid ReportId,
    DefinicionDeInforme Definicion) : ICommand<bool>;

/// <summary>La definición guardada de un informe, para volver a abrirla en el constructor.</summary>
public sealed record GetDefinicionQuery(Guid TenantId, Guid ReportId) : IQuery<DefinicionDeInforme>;

public sealed class GuardarDefinicionHandler(
    IReportRepository informes,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<GuardarDefinicionCommand, bool>
{
    public async Task<Result<bool>> Handle(GuardarDefinicionCommand request, CancellationToken ct)
    {
        var informe = await informes.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (informe is null)
            return Result<bool>.Failure("El informe no existe");

        var resultado = informe.DefinirAMedida(request.Definicion);
        if (resultado.IsFailure)
            return Result<bool>.Failure(resultado.Error!);

        await informes.UpdateAsync(informe, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

public sealed class GetDefinicionHandler(IReportRepository informes)
    : IQueryHandler<GetDefinicionQuery, DefinicionDeInforme>
{
    public async Task<Result<DefinicionDeInforme>> Handle(GetDefinicionQuery request, CancellationToken ct)
    {
        var informe = await informes.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (informe is null)
            return Result<DefinicionDeInforme>.Failure("El informe no existe");

        var definicion = informe.LeerDefinicion();

        return definicion is null
            ? Result<DefinicionDeInforme>.Failure("Este informe no es a medida: no tiene definición")
            : Result<DefinicionDeInforme>.Success(definicion);
    }
}

/// <summary>
/// Resuelve una definición a filas.
///
/// <b>Es un puerto</b>, igual que <c>IDashboardRepository</c> y por la misma razón: resolver un
/// informe de tareas exige mirar WorkItems y uno de tickets exige mirar Ticketing, y ningún
/// módulo referencia a otro. Reporting dice qué necesita; el host, que los conoce a todos, lo
/// satisface.
/// </summary>
public interface IResolutorDeInformes
{
    /// <summary>
    /// Los valores admitidos de un campo de lista cerrada, o vacío si no lo es.
    ///
    /// Va en este puerto y no en el catálogo porque los valores viven en otros módulos —los
    /// estados de un ticket son de Ticketing— y Reporting no los puede conocer. El host, que los
    /// conoce a todos, los aporta.
    /// </summary>
    IReadOnlyList<string> ValoresDe(string origen, string campo);

    Task<Exportaciones.TablaDeInforme> ResolverAsync(
        string titulo, Guid tenantId, DefinicionDeInforme definicion, CancellationToken ct = default);
}

/// <summary>
/// La vista previa del constructor: enseña el resultado **antes** de guardar.
///
/// Sin ella, construir un informe es escribir a ciegas y descubrir el resultado al exportarlo,
/// que es cuando ya se ha guardado y quien lo hizo se ha ido. Se limita a unas pocas filas: es
/// una comprobación de que la definición dice lo que se pretendía, no el informe entero.
/// </summary>
public sealed record VistaPreviaQuery(
    Guid TenantId, string Titulo, DefinicionDeInforme Definicion) : IQuery<VistaPreviaDto>;

/// <summary>Lo que se enseña en la vista previa: la tabla resuelta, recortada.</summary>
public sealed record VistaPreviaDto(
    string Titulo,
    string? Subtitulo,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyList<string>> Filas,
    int TotalDeFilas);

public sealed class VistaPreviaHandler(IResolutorDeInformes resolutor)
    : IQueryHandler<VistaPreviaQuery, VistaPreviaDto>
{
    /// <summary>Cuántas filas se enseñan. Suficiente para reconocer el informe, no para leerlo entero.</summary>
    private const int FilasDeMuestra = 25;

    public async Task<Result<VistaPreviaDto>> Handle(VistaPreviaQuery request, CancellationToken ct)
    {
        var validacion = request.Definicion.Validar();
        if (validacion.IsFailure)
            return Result<VistaPreviaDto>.Failure(validacion.Error!);

        try
        {
            var tabla = await resolutor.ResolverAsync(request.Titulo, request.TenantId, request.Definicion, ct);

            return Result<VistaPreviaDto>.Success(new VistaPreviaDto(
                tabla.Titulo, tabla.Subtitulo, tabla.Columnas,
                tabla.Filas.Take(FilasDeMuestra).ToList(),
                tabla.Filas.Count));
        }
        catch (InvalidOperationException ex)
        {
            // El motor lanza con un mensaje que dice qué pieza no sabe traducir. Se devuelve tal
            // cual en vez de un «error al generar la vista previa»: en un constructor, saber qué
            // combinación no funciona es la mitad del trabajo.
            return Result<VistaPreviaDto>.Failure(ex.Message);
        }
    }
}

/// <summary>
/// El catálogo completo, tal como se sirve a la pantalla.
///
/// <b>La pantalla no escribe ninguna de estas listas.</b> Es la razón de que este endpoint
/// exista: dos veces en este módulo el desplegable ofreció opciones que el servidor no conocía
/// —los tipos de informe, y antes los filtros del menú— y en los dos casos el fallo no dio error,
/// sólo un 400 con un mensaje que no decía qué campo estaba mal. Con el catálogo servido, ofrecer
/// algo que no existe deja de ser posible.
/// </summary>
public sealed record CatalogoDto(
    IReadOnlyList<OrigenDto> Origenes,
    IReadOnlyList<OperadorDto> Operadores,
    IReadOnlyList<OpcionDto> Formas,
    IReadOnlyList<OpcionDto> Granularidades);

public sealed record OrigenDto(
    string Clave, string Nombre, IReadOnlyList<CampoDto> Campos, IReadOnlyList<OpcionDto> Medidas);

/// <param name="Operadores">
/// Los operadores aplicables a **este** campo, ya resueltos. Se mandan por campo y no sólo por
/// tipo porque la nullabilidad también decide: «está vacío» vale sobre la fecha de resolución de
/// un ticket y no sobre el vencimiento de una tarea. Con la lista por tipo, la pantalla ofrecía
/// una combinación que el servidor rechaza.
/// </param>
/// <param name="Valores">
/// Los valores admitidos, cuando el campo es una lista cerrada —un estado, una prioridad—, o
/// vacío si admite texto libre.
///
/// <b>Se sirven para que nadie los escriba a mano.</b> Sin ellos, filtrar por estado obliga a
/// teclear «Open» adivinando, y un valor mal escrito produce un informe vacío que parece un
/// informe sin datos. Es la misma clase de fallo que este módulo ya ha tenido tres veces.
/// </param>
public sealed record CampoDto(
    string Clave, string Nombre, string Tipo, IReadOnlyList<string> Operadores, IReadOnlyList<string> Valores);

public sealed record OperadorDto(string Clave, string Nombre, IReadOnlyList<string> Tipos, bool NecesitaValor);

public sealed record OpcionDto(string Clave, string Nombre);

public sealed record GetCatalogoQuery : IQuery<CatalogoDto>;

public sealed class GetCatalogoHandler(IResolutorDeInformes resolutor)
    : IQueryHandler<GetCatalogoQuery, CatalogoDto>
{
    public Task<Result<CatalogoDto>> Handle(GetCatalogoQuery request, CancellationToken ct)
    {
        var catalogo = new CatalogoDto(
            Origenes: CatalogoDeInformes.Origenes()
                .Select(o => new OrigenDto(
                    o.Clave, o.Nombre,
                    o.Campos.Select(c => new CampoDto(
                        c.Clave, c.Nombre, c.Tipo.ToString(),
                        CatalogoDeInformes.OperadoresPara(c).Select(op => op.Clave).ToList(),
                        resolutor.ValoresDe(o.Clave, c.Clave))).ToList(),
                    o.Medidas.Select(m => new OpcionDto(m.Clave, m.Nombre)).ToList()))
                .ToList(),

            Operadores: CatalogoDeInformes.Operadores()
                .Select(o => new OperadorDto(
                    o.Clave, o.Nombre, o.Tipos.Select(t => t.ToString()).ToList(), o.NecesitaValor))
                .ToList(),

            Formas: CatalogoDeInformes.Formas().Select(f => new OpcionDto(f.Clave, f.Nombre)).ToList(),

            Granularidades: CatalogoDeInformes.GranularidadesDeFecha()
                .Select(g => new OpcionDto(g.Clave, g.Nombre)).ToList());

        return Task.FromResult(Result<CatalogoDto>.Success(catalogo));
    }
}
