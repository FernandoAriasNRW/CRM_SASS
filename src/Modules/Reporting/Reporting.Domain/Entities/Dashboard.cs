using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.Entities;

public sealed class Dashboard : Entity, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public bool IsPublic { get; private set; }
    public Guid CreatedById { get; private set; }
    /// <summary>
    /// Los widgets del panel, serializados. Ver <see cref="Paneles.DisposicionDelPanel"/>.
    ///
    /// <b>Hasta ahora esta columna no la leía nadie.</b> Se podía crear un panel, ponerle nombre y
    /// marcarlo público, y pulsarlo no hacía nada: la pantalla guardaba la selección en una señal
    /// que no pintaba nada. Nunca llegó a haber una fila en la tabla.
    /// </summary>
    public string WidgetsJson { get; private set; } = string.Empty;
    public List<Guid> TagIds { get; private set; } = new();

    private Dashboard() { }

    public static Dashboard Create(Guid tenantId, string title, bool isDefault, bool isPublic, Guid createdById, string widgetsJson)
    {
        return new Dashboard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            IsDefault = isDefault,
            IsPublic = isPublic,
            CreatedById = createdById,
            WidgetsJson = widgetsJson
        };
    }

    public void Update(string title, bool isDefault, bool isPublic, string widgetsJson)
    {
        Title = title;
        IsDefault = isDefault;
        IsPublic = isPublic;
        WidgetsJson = widgetsJson;
    }

    /// <summary>
    /// El panel propio de una persona, el que ve al entrar.
    ///
    /// <b>Uno por persona, no uno por inquilino.</b> Es la primera decisión del plan: «un dashboard
    /// es de quien lo mira; si se guarda por inquilino, dos personas se pisan la configuración».
    /// Nace privado por lo mismo: lo que alguien coloca para sí no aparece en la pantalla de los
    /// demás hasta que decida compartirlo.
    /// </summary>
    public static Dashboard CrearPanelPersonal(Guid tenantId, Guid userId, string titulo = "Mi panel")
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = titulo,
            IsDefault = true,
            IsPublic = false,
            CreatedById = userId,
            WidgetsJson = new Paneles.DisposicionDelPanel().ASerializar()
        };

    /// <summary>
    /// Coloca los widgets, validándolos antes.
    ///
    /// La validación va aquí y no sólo en el borde porque un panel con un widget fuera de la
    /// rejilla se guarda bien y **se rompe al pintarlo**, que es cuando quien lo movió ya ha
    /// cerrado la pantalla.
    /// </summary>
    public Result Colocar(Paneles.DisposicionDelPanel disposicion)
    {
        var validacion = disposicion.Validar();
        if (validacion.IsFailure) return validacion;

        WidgetsJson = disposicion.ASerializar();
        return Result.Success();
    }

    /// <summary>Los widgets ya leídos. Vacío si el panel no tiene ninguno todavía.</summary>
    public Paneles.DisposicionDelPanel LeerDisposicion() => Paneles.DisposicionDelPanel.Leer(WidgetsJson);

    public void AddTag(Guid tagId)
    {
        if (!TagIds.Contains(tagId))
        {
            TagIds.Add(tagId);
        }
    }

    public void RemoveTag(Guid tagId)
    {
        if (TagIds.Contains(tagId))
        {
            TagIds.Remove(tagId);
        }
    }
}
