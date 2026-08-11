namespace BuildingBlocks.Domain.Primitives;

public sealed class OutboxMessageEntity
{
  public Guid Id { get; init; }

  // Nombre del evento para saber cómo deserializarlo luego (ej: "UserRegistered")
  public string Type { get; init; } = string.Empty;

  // El objeto serializado en JSON
  public string Payload { get; init; } = string.Empty;

  public DateTime CreatedAt { get; init; }

  // Null si no se ha procesado, fecha si ya se envió con éxito
  public DateTime? ProcessedAt { get; set; }

  // Para guardar el stacktrace si el envío falla
  public string? Error { get; set; }
}