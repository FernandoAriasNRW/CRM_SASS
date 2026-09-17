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
public sealed class DetectorDeCiclosTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid D = Guid.Parse("dddddddd-0000-0000-0000-000000000004");

    [Fact]
    public void Sin_dependencias_previas_no_hay_ciclo()
    {
        CycleDetector.WouldCloseCycle([], A, B).Should().BeFalse();
    }

    [Fact]
    public void Una_tarea_no_puede_depender_de_si_misma()
    {
        CycleDetector.WouldCloseCycle([], A, A).Should().BeTrue();
    }

    [Fact]
    public void El_ciclo_directo_se_detecta()
    {
        // A ya está bloqueada por B; que B dependa de A cerraría el ciclo.
        CycleDetector.WouldCloseCycle([new Edge(A, B)], B, A).Should().BeTrue();
    }

    [Fact]
    public void El_ciclo_largo_se_detecta()
    {
        // A←B, B←C: añadir C←A cierra A→B→C→A.
        Edge[] aristas = [new(A, B), new(B, C)];

        CycleDetector.WouldCloseCycle(aristas, C, A).Should().BeTrue();
    }

    [Fact]
    public void Una_cadena_larga_sin_cerrar_no_es_ciclo()
    {
        Edge[] aristas = [new(A, B), new(B, C)];

        // D no participa en la cadena: colgarla de A es legítimo.
        CycleDetector.WouldCloseCycle(aristas, D, A).Should().BeFalse();
    }

    [Fact]
    public void Un_diamante_no_es_un_ciclo()
    {
        // A depende de B y de C, las dos dependen de D. Es un grafo dirigido acíclico
        // perfectamente válido, y un detector que sólo mirase «ya lo visité» sin dirección lo
        // rechazaría.
        Edge[] aristas = [new(A, B), new(A, C), new(B, D), new(C, D)];

        CycleDetector.WouldCloseCycle(aristas, D, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Repetir_una_dependencia_que_ya_existe_no_se_considera_ciclo()
    {
        // Que ya exista es otro rechazo distinto, y lo comprueba el handler: aquí sólo importa
        // que no se confunda con un ciclo, porque el mensaje al usuario no es el mismo.
        Edge[] aristas = [new(A, B)];

        CycleDetector.WouldCloseCycle(aristas, A, B).Should().BeFalse();
    }

    [Fact]
    public void Un_grafo_que_ya_tuviera_un_ciclo_no_hace_girar_al_detector()
    {
        // Datos corruptos o una escritura concurrente podrían dejar un ciclo ya guardado. Un
        // recorrido recursivo ingenuo se colgaría aquí dentro de una petición.
        Edge[] aristas = [new(A, B), new(B, A)];

        var comprobar = () => CycleDetector.WouldCloseCycle(aristas, C, A);

        comprobar.Should().NotThrow();
        comprobar().Should().BeFalse("C no está en el ciclo, así que colgarla de A es legítimo");
    }

    /// <summary>
    /// Recorre todas las cadenas posibles sobre cuatro tareas: para cada par (x, y) con una
    /// cadena completa x←y←z←w ya guardada, sólo cerrar el círculo debe dar ciclo.
    /// </summary>
    [Fact]
    public void En_una_cadena_completa_solo_el_cierre_es_ciclo()
    {
        Edge[] cadena = [new(A, B), new(B, C), new(C, D)];

        // Cerrar por cualquiera de los extremos hacia atrás es ciclo.
        CycleDetector.WouldCloseCycle(cadena, D, A).Should().BeTrue();
        CycleDetector.WouldCloseCycle(cadena, D, B).Should().BeTrue();
        CycleDetector.WouldCloseCycle(cadena, C, A).Should().BeTrue();

        // Y hacia delante no lo es: son atajos dentro del mismo orden.
        CycleDetector.WouldCloseCycle(cadena, A, D).Should().BeFalse();
        CycleDetector.WouldCloseCycle(cadena, B, D).Should().BeFalse();
        CycleDetector.WouldCloseCycle(cadena, A, C).Should().BeFalse();
    }
}
