using FluentAssertions;
using WorkItems.Domain.Services;
using Xunit;
using Edge = WorkItems.Domain.Services.CycleDetector.Edge;

namespace UnitTests;

/// <summary>
/// El detector de ciclos de las dependencias.
///
/// Se prueba a fondo y sin base de datos porque es la regla que más caro sale equivocar: un
/// ciclo deja el Gantt de la 4C sin solución y hace que cualquier cálculo de «qué puedo
/// empezar» mienta o se cuelgue. Y es la clase de código que un refactor rompe sin que ningún
/// test de camino feliz se entere.
///
/// La arista se lee «Tarea está bloqueada por DependeDe».
/// </summary>
public sealed class CycleDetectorTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid D = Guid.Parse("dddddddd-0000-0000-0000-000000000004");

    [Fact]
    public void Without_previous_dependencies_there_is_no_cycle()
    {
        CycleDetector.WouldCloseCycle([], A, B).Should().BeFalse();
    }

    [Fact]
    public void A_task_cannot_depend_on_itself()
    {
        CycleDetector.WouldCloseCycle([], A, A).Should().BeTrue();
    }

    [Fact]
    public void A_direct_cycle_is_detected()
    {
        // A ya está bloqueada por B; que B dependa de A cerraría el ciclo.
        CycleDetector.WouldCloseCycle([new Edge(A, B)], B, A).Should().BeTrue();
    }

    [Fact]
    public void A_long_cycle_is_detected()
    {
        // A←B, B←C: añadir C←A cierra A→B→C→A.
        Edge[] edges = [new(A, B), new(B, C)];

        CycleDetector.WouldCloseCycle(edges, C, A).Should().BeTrue();
    }

    [Fact]
    public void A_long_open_chain_is_not_a_cycle()
    {
        Edge[] edges = [new(A, B), new(B, C)];

        // D no participa en la cadena: colgarla de A es legítimo.
        CycleDetector.WouldCloseCycle(edges, D, A).Should().BeFalse();
    }

    [Fact]
    public void A_diamond_is_not_a_cycle()
    {
        // A depende de B y de C, las dos dependen de D. Es un grafo dirigido acíclico
        // perfectamente válido, y un detector que sólo mirase «ya lo visité» sin dirección lo
        // rechazaría.
        Edge[] edges = [new(A, B), new(A, C), new(B, D), new(C, D)];

        CycleDetector.WouldCloseCycle(edges, D, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Repeating_an_existing_dependency_is_not_a_cycle()
    {
        // Que ya exista es otro rechazo distinto, y lo comprueba el handler: aquí sólo importa
        // que no se confunda con un ciclo, porque el mensaje al usuario no es el mismo.
        Edge[] edges = [new(A, B)];

        CycleDetector.WouldCloseCycle(edges, A, B).Should().BeFalse();
    }

    [Fact]
    public void A_graph_that_already_has_a_cycle_does_not_loop_the_detector()
    {
        // Datos corruptos o una escritura concurrente podrían dejar un ciclo ya guardado. Un
        // recorrido recursivo ingenuo se colgaría aquí dentro de una petición.
        Edge[] edges = [new(A, B), new(B, A)];

        var check = () => CycleDetector.WouldCloseCycle(edges, C, A);

        check.Should().NotThrow();
        check().Should().BeFalse("C no está en el ciclo, así que colgarla de A es legítimo");
    }

    /// <summary>
    /// Recorre todas las cadenas posibles sobre cuatro tareas: para cada par (x, y) con una
    /// cadena completa x←y←z←w ya guardada, sólo cerrar el círculo debe dar ciclo.
    /// </summary>
    [Fact]
    public void In_a_full_chain_only_the_closing_link_is_a_cycle()
    {
        Edge[] chain = [new(A, B), new(B, C), new(C, D)];

        // Cerrar por cualquiera de los extremos hacia atrás es ciclo.
        CycleDetector.WouldCloseCycle(chain, D, A).Should().BeTrue();
        CycleDetector.WouldCloseCycle(chain, D, B).Should().BeTrue();
        CycleDetector.WouldCloseCycle(chain, C, A).Should().BeTrue();

        // Y hacia delante no lo es: son atajos dentro del mismo orden.
        CycleDetector.WouldCloseCycle(chain, A, D).Should().BeFalse();
        CycleDetector.WouldCloseCycle(chain, B, D).Should().BeFalse();
        CycleDetector.WouldCloseCycle(chain, A, C).Should().BeFalse();
    }
}
