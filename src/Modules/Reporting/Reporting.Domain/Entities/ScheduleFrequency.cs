using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Reporting.Domain.ValueObjects;

namespace Reporting.Domain.Entities;

/// <summary>
/// Cada cuánto se genera un informe programado.
///
/// Tres y sólo tres. Añadir «cada hora» tentaría, y sería un generador de correo no deseado: un
/// informe que llega cada hora se deja de leer el segundo día y se convierte en ruido que además
/// esconde los que sí importan.
/// </summary>
public sealed class ScheduleFrequency : Enumeration
{
    public static readonly ScheduleFrequency Daily = new(1, "Daily");
    public static readonly ScheduleFrequency Weekly = new(2, "Weekly");
    public static readonly ScheduleFrequency Monthly = new(3, "Monthly");

    private ScheduleFrequency() : base(0, string.Empty) { }
    private ScheduleFrequency(int value, string name) : base(value, name) { }

    public static IReadOnlyList<ScheduleFrequency> All() => GetAll<ScheduleFrequency>();
}
