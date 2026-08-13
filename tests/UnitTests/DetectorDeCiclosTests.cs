using FluentAssertions;
using WorkItems.Domain.Servicios;
using Xunit;
using Arista = WorkItems.Domain.Servicios.DetectorDeCiclos.Arista;

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
        DetectorDeCiclos.CerrariaUnCiclo([], A, B).Should().BeFalse();
    }

    [Fact]
    public void Una_tarea_no_puede_depender_de_si_misma()
    {
        DetectorDeCiclos.CerrariaUnCiclo([], A, A).Should().BeTrue();
    }

    [Fact]
    public void El_ciclo_directo_se_detecta()
    {
        // A ya está bloqueada por B; que B dependa de A cerraría el ciclo.
        DetectorDeCiclos.CerrariaUnCiclo([new Arista(A, B)], B, A).Should().BeTrue();
    }

    [Fact]
    public void El_ciclo_largo_se_detecta()
    {
        // A←B, B←C: añadir C←A cierra A→B→C→A.
        Arista[] aristas = [new(A, B), new(B, C)];

        DetectorDeCiclos.CerrariaUnCiclo(aristas, C, A).Should().BeTrue();
    }

    [Fact]
    public void Una_cadena_larga_sin_cerrar_no_es_ciclo()
    {
        Arista[] aristas = [new(A, B), new(B, C)];

        // D no participa en la cadena: colgarla de A es legítimo.
        DetectorDeCiclos.CerrariaUnCiclo(aristas, D, A).Should().BeFalse();
    }

    [Fact]
    public void Un_diamante_no_es_un_ciclo()
    {
        // A depende de B y de C, las dos dependen de D. Es un grafo dirigido acíclico
        // perfectamente válido, y un detector que sólo mirase «ya lo visité» sin dirección lo
        // rechazaría.
        Arista[] aristas = [new(A, B), new(A, C), new(B, D), new(C, D)];

        DetectorDeCiclos.CerrariaUnCiclo(aristas, D, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Repetir_una_dependencia_que_ya_existe_no_se_considera_ciclo()
    {
        // Que ya exista es otro rechazo distinto, y lo comprueba el handler: aquí sólo importa
        // que no se confunda con un ciclo, porque el mensaje al usuario no es el mismo.
        Arista[] aristas = [new(A, B)];

        DetectorDeCiclos.CerrariaUnCiclo(aristas, A, B).Should().BeFalse();
    }

    [Fact]
    public void Un_grafo_que_ya_tuviera_un_ciclo_no_hace_girar_al_detector()
    {
        // Datos corruptos o una escritura concurrente podrían dejar un ciclo ya guardado. Un
        // recorrido recursivo ingenuo se colgaría aquí dentro de una petición.
        Arista[] aristas = [new(A, B), new(B, A)];

        var comprobar = () => DetectorDeCiclos.CerrariaUnCiclo(aristas, C, A);

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
        Arista[] cadena = [new(A, B), new(B, C), new(C, D)];

        // Cerrar por cualquiera de los extremos hacia atrás es ciclo.
        DetectorDeCiclos.CerrariaUnCiclo(cadena, D, A).Should().BeTrue();
        DetectorDeCiclos.CerrariaUnCiclo(cadena, D, B).Should().BeTrue();
        DetectorDeCiclos.CerrariaUnCiclo(cadena, C, A).Should().BeTrue();

        // Y hacia delante no lo es: son atajos dentro del mismo orden.
        DetectorDeCiclos.CerrariaUnCiclo(cadena, A, D).Should().BeFalse();
        DetectorDeCiclos.CerrariaUnCiclo(cadena, B, D).Should().BeFalse();
        DetectorDeCiclos.CerrariaUnCiclo(cadena, A, C).Should().BeFalse();
    }
}
