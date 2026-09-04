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
    public Guid CurrentTenantId => userContext?.TenantId ?? Guid.Empty;

    /// <summary>
    /// Si las consultas de este contexto deben dejar pasar lo que está en la papelera.
    ///
    /// No se toca a mano: se abre con <see cref="VerTambien"/>, que lo devuelve a su sitio al
    /// cerrar el ámbito. Dejarlo encendido por descuido enseñaría borrados en las listas
    /// normales, que es justo lo que este filtro existe para impedir.
    /// </summary>
    public bool IncluirBorrados { get; private set; }

    /// <summary>Si las consultas de este contexto deben dejar pasar lo archivado.</summary>
    public bool IncluirArchivados { get; private set; }

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
    public IDisposable VerTambien(bool borrados = false, bool archivados = false)
    {
        var previoBorrados = IncluirBorrados;
        var previoArchivados = IncluirArchivados;

        IncluirBorrados = borrados;
        IncluirArchivados = archivados;

        return new AmbitoDeAlcance(() =>
        {
            IncluirBorrados = previoBorrados;
            IncluirArchivados = previoArchivados;
        });
    }

    private sealed class AmbitoDeAlcance(Action alCerrar) : IDisposable
    {
        private bool _cerrado;

        public void Dispose()
        {
            // Un `using` mal anidado puede llamar a Dispose dos veces; la segunda no debe
            // volver a escribir el estado del contexto.
            if (_cerrado) return;
            _cerrado = true;
            alCerrar();
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
            () => IncluirBorrados,
            () => IncluirArchivados);
}
