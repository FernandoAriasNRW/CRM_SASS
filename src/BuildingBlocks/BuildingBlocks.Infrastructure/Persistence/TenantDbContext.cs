using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base de los <c>DbContext</c> de módulo. Aplica automáticamente el filtro de tenant, el de
/// papelera y el de archivado a toda entidad que implemente <c>ITenantEntity</c>,
/// <c>ISoftDeletable</c> o <c>IArchivable</c>.
///
/// Heredar de aquí es lo que sustituye al filtrado manual por <c>TenantId</c> repetido
/// en cada consulta: el aislamiento pasa a ser el comportamiento por defecto en lugar de
/// algo que cada handler debe acordarse de hacer.
/// </summary>
public abstract class TenantDbContext(DbContextOptions options, IUserContext? userContext) : DbContext(options)
{
    /// <summary>
    /// Tenant de la petición en curso. Se lee en cada consulta, no al construir el
    /// modelo, para que EF lo traduzca a un parámetro y el modelo cacheado sirva a
    /// todos los tenants.
    ///
    /// Sin contexto de usuario (workers en segundo plano, herramientas de diseño)
    /// devuelve <c>Guid.Empty</c>, que no casa con ninguna fila: el filtro cierra por
    /// defecto. Un proceso que legítimamente deba cruzar tenants ha de declararlo
    /// explícitamente con <c>IgnoreQueryFilters()</c>.
    /// </summary>
    public Guid CurrentTenantId => _forcedTenantId ?? userContext?.TenantId ?? Guid.Empty;

    private Guid? _forcedTenantId;

    /// <summary>
    /// Ejecuta las consultas de este contexto como si fueran de un inquilino concreto, y lo
    /// deshace al salir del <c>using</c>.
    ///
    /// <b>Existe para los trabajos de segundo plano</b>, que no tienen petición y por tanto no
    /// tienen usuario: sin esto, <see cref="CurrentTenantId"/> vale <c>Guid.Empty</c>, el filtro
    /// global no casa con ninguna fila y **todas las consultas devuelven cero sin dar ningún
    /// error**. Pasó exactamente eso: el generador de exportaciones producía ficheros correctos,
    /// con su aviso y todo, y **vacíos** —la API decía 15 tareas y el informe exportado decía 0—.
    ///
    /// La alternativa era <c>IgnoreQueryFilters()</c> con el <c>TenantId</c> repetido a mano en
    /// cada consulta. Se descartó porque apaga **todos** los filtros: lo archivado y lo borrado
    /// volverían a salir, y un informe con tareas de la papelera dentro es peor que uno vacío,
    /// porque nadie lo nota.
    ///
    /// Sólo debe usarlo un proceso que sepa de qué inquilino es el trabajo que está haciendo.
    /// </summary>
    public IDisposable AsTenant(Guid tenantId)
    {
        var previous = _forcedTenantId;
        _forcedTenantId = tenantId;

        return new RestoreOnDispose(() => _forcedTenantId = previous);
    }

    /// <summary>
    /// Si las consultas de este contexto deben dejar pasar lo que está en la papelera.
    ///
    /// No se toca a mano: se abre con <see cref="IncludeHidden"/>, que lo devuelve a su sitio al
    /// cerrar el ámbito. Dejarlo encendido por descuido enseñaría borrados en las listas
    /// normales, que es justo lo que este filtro existe para impedir.
    /// </summary>
    public bool IncludeDeleted { get; private set; }

    /// <summary>Si las consultas de este contexto deben dejar pasar lo archivado.</summary>
    public bool IncludeArchived { get; private set; }

    /// <summary>
    /// Abre un ámbito en el que las consultas ven además lo borrado o lo archivado, y lo cierra
    /// al salir del <c>using</c>.
    ///
    /// <b>Es la única forma de ver la papelera y el archivo, y eso es deliberado.</b> La
    /// alternativa evidente —<c>IgnoreQueryFilters()</c>— apaga <b>todos</b> los filtros, el de
    /// tenant incluido: la pantalla de la papelera habría enseñado los borrados de todos los
    /// inquilinos. Este proyecto ya pagó una fuga de tenant en la Fase 4A; no se repite por
    /// ahorrarse un método.
    ///
    /// Los ámbitos no se anidan: al cerrar se restaura el valor que había, así que un ámbito
    /// dentro de otro deja el de fuera como estaba.
    /// </summary>
    public IDisposable IncludeHidden(bool deleted = false, bool archived = false)
    {
        var previousDeleted = IncludeDeleted;
        var previousArchived = IncludeArchived;

        IncludeDeleted = deleted;
        IncludeArchived = archived;

        return new RestoreOnDispose(() =>
        {
            IncludeDeleted = previousDeleted;
            IncludeArchived = previousArchived;
        });
    }

    private sealed class RestoreOnDispose(Action onDispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            // Un `using` mal anidado puede llamar a Dispose dos veces; la segunda no debe
            // volver a escribir el estado del contexto.
            if (_disposed) return;
            _disposed = true;
            onDispose();
        }
    }

    /// <summary>
    /// Debe invocarse al final de <c>OnModelCreating</c> en las clases derivadas, después
    /// de <c>ApplyConfigurationsFromAssembly</c> y de cualquier <c>ComplexProperty</c>:
    /// el filtro recorre el modelo ya construido y necesita ver todas las entidades
    /// registradas. Aplicarlo antes dejaría entidades sin filtro.
    ///
    /// <c>TenantIsolationVerifier</c> comprueba al arrancar que ninguna se haya quedado
    /// fuera, de modo que olvidar esta llamada rompe el arranque en vez de filtrar datos.
    /// </summary>
    protected void ApplyTenantFilters(ModelBuilder modelBuilder)
        => TenantQueryFilter.ApplyGlobalFilters(
            modelBuilder,
            () => CurrentTenantId,
            () => IncludeDeleted,
            () => IncludeArchived);
}
