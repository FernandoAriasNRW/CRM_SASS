using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Webhook.Application;

/// <summary>
/// El cuerpo que recibe un suscriptor.
///
/// <code>
/// { "id": "…", "event": "task.created", "occurredAtUtc": "…", "tenantId": "…",
///   "data": { "input": { …lo que se pidió… }, "result": { …lo que quedó… } } }
/// </code>
///
/// <b>Nunca lleva secretos.</b> Se mandaba el comando tal cual, y el de crear un usuario lleva la
/// contraseña en claro: cualquier suscriptor de «usuario creado» la recibía. Ahora se quitan, en
/// todo el árbol, las propiedades cuyo nombre habla de contraseñas, secretos, tokens o hashes, y
/// las de la maquinaria interna de las entidades (<c>DomainEvents</c>). Se filtra por nombre a
/// propósito: una lista de propiedades permitidas por comando sería más fina, pero el día que
/// alguien añada un campo nuevo a un comando, quedaría fuera sin avisar o —peor— dentro sin pensarlo.
///
/// <b>Lleva también el resultado</b>: el comando de crear una tarea no sabe el identificador que
/// tendrá, y un suscriptor que no lo recibe no puede hacer nada con el evento.
/// </summary>
public static partial class WebhookPayload
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        MaxDepth = 16,
        Converters = { new JsonStringEnumConverter() },
    };

    [GeneratedRegex("password|secret|token|hash|domainevents", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Sensitive();

    public static string Build(Guid deliveryId, string eventName, Guid tenantId, DateTime occurredAtUtc, object? input, object? result)
    {
        var envelope = new JsonObject
        {
            ["id"] = deliveryId,
            ["event"] = eventName,
            ["occurredAtUtc"] = occurredAtUtc,
            ["tenantId"] = tenantId,
            ["data"] = new JsonObject
            {
                ["input"] = Sanitize(input),
                ["result"] = Sanitize(result),
            },
        };

        return envelope.ToJsonString(Options);
    }

    /// <summary>
    /// Convierte a JSON y quita lo sensible. Si algo no se puede serializar —una entidad con algo
    /// raro dentro—, sale <c>null</c>: el evento se manda igual, con menos detalle, en vez de no
    /// mandarse.
    /// </summary>
    public static JsonNode? Sanitize(object? value)
    {
        if (value is null) return null;

        JsonNode? node;
        try { node = JsonSerializer.SerializeToNode(value, value.GetType(), Options); }
        catch (Exception) { return null; }

        Strip(node);
        return node;
    }

    private static void Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).Where(k => Sensitive().IsMatch(k)).ToList())
                    obj.Remove(key);
                foreach (var child in obj.Select(p => p.Value).ToList())
                    Strip(child);
                break;
            case JsonArray array:
                foreach (var child in array.ToList())
                    Strip(child);
                break;
        }
    }
}
