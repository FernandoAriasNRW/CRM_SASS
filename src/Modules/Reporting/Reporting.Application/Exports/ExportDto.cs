using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Exports;

/// <summary>
/// Cómo se ve una exportación desde fuera.
///
/// Lleva el error dentro, no en un canal aparte: la pantalla que pinta la lista es la misma que
/// tiene que explicar por qué una falló, y obligarla a una segunda llamada para eso garantiza
/// que alguien se la salte y enseñe «Fallida» a secas.
/// </summary>
public sealed record ExportDto(
    Guid Id,
    Guid ReportId,
    string Format,
    string Status,
    DateTime RequestedAtUtc,
    DateTime? FinishedAtUtc,
    string? FileName,
    long SizeBytes,
    string? Error,
    int Attempts);
