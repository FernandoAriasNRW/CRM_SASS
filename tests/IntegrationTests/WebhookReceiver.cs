using System.Collections.Concurrent;

namespace IntegrationTests;

/// <summary>
/// El servidor de fuera que recibe los webhooks, en memoria.
///
/// Las pruebas no mandan nada por la red: el cliente HTTP de los webhooks se conecta a esto en vez
/// de a internet. Responde 200, salvo si la ruta contiene <c>/fail</c>, que responde 500 para
/// probar los reintentos.
/// </summary>
public sealed class WebhookReceiver
{
    public sealed record Received(Uri Url, IReadOnlyDictionary<string, string> Headers, string Body);

    private readonly ConcurrentQueue<Received> _received = new();

    public IReadOnlyList<Received> All => _received.ToList();

    internal void Record(Received request) => _received.Enqueue(request);

    /// <summary>Espera a que llegue un envío que cumpla la condición. El trabajo de entrega mira la cola cada segundo.</summary>
    public async Task<Received> WaitForAsync(Func<Received, bool> match, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow < deadline)
        {
            var found = _received.FirstOrDefault(match);
            if (found is not null) return found;
            await Task.Delay(200);
        }

        throw new TimeoutException("No llegó el webhook esperado");
    }
}

/// <summary>
/// El manejador que usa el cliente HTTP de los webhooks en las pruebas. Es uno nuevo cada vez que la
/// fábrica de clientes lo pide —los recicla y los desecha—, y todos apuntan al mismo receptor.
/// </summary>
internal sealed class WebhookReceiverHandler(WebhookReceiver receiver) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        receiver.Record(new(request.RequestUri!, headers, body));

        return new HttpResponseMessage(request.RequestUri!.AbsolutePath.Contains("/fail")
            ? System.Net.HttpStatusCode.InternalServerError
            : System.Net.HttpStatusCode.OK);
    }
}
