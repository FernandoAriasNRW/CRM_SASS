using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Webhook.Application.Abstractions;

namespace Webhook.Infrastructure.Delivery;

/// <summary>Cómo se comporta el envío de webhooks. Sección <c>Webhooks</c> de la configuración.</summary>
public sealed class WebhookOptions
{
    public const string Section = "Webhooks";

    /// <summary>
    /// Si se puede mandar a direcciones de red privada o de bucle local. Apagado en producción;
    /// se enciende en desarrollo y en pruebas, donde el destino suele ser la propia máquina.
    /// </summary>
    public bool AllowPrivateNetworks { get; set; }

    /// <summary>Si se acepta <c>http://</c> además de <c>https://</c>. Mismo criterio que lo anterior.</summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>Cada cuánto mira la cola el trabajo de entrega, aunque nadie le avise.</summary>
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>Cuánto se espera a que el destino conteste antes de darlo por fallido.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>Qué direcciones IP son de dentro: bucle local, redes privadas, enlace local, sin especificar.</summary>
internal static class PrivateAddresses
{
    public static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;

        var b = address.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)   // enlace local: incluye el servicio de metadatos de la nube
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)   // CGNAT
            || b[0] == 0;
    }
}

/// <summary>
/// Responde <see cref="IWebhookUrlPolicy"/> al guardar una suscripción. Mira lo que se puede saber
/// sin resolver el nombre —el esquema, <c>localhost</c>, una IP escrita a mano—; lo demás lo vuelve
/// a mirar el cliente al conectar, con la IP ya resuelta (<see cref="WebhookHttpClient"/>).
/// </summary>
internal sealed class WebhookUrlPolicy(IOptions<WebhookOptions> options) : IWebhookUrlPolicy
{
    public string? Reject(string url)
    {
        if (!Uri.TryCreate((url ?? string.Empty).Trim(), UriKind.Absolute, out var uri))
            return "La URL tiene que ser una dirección http o https completa";

        var settings = options.Value;

        if (uri.Scheme != Uri.UriSchemeHttps && !(settings.AllowInsecureHttp && uri.Scheme == Uri.UriSchemeHttp))
            return "La URL tiene que empezar por https://: lo que se manda son datos de la organización";

        if (settings.AllowPrivateNetworks) return null;

        var host = uri.IdnHost;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var ip) && PrivateAddresses.IsPrivate(ip)))
            return "La URL apunta a una dirección interna, y desde aquí no se manda nada hacia dentro";

        return null;
    }
}

/// <summary>Despierta al trabajo de entrega en cuanto hay envíos nuevos.</summary>
internal sealed class WebhookDeliverySignal : IWebhookDeliverySignal
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        // Si ya estaba avisado, no se acumulan avisos: una vuelta recoge todo lo pendiente.
        if (_semaphore.CurrentCount == 0)
        {
            try { _semaphore.Release(); }
            catch (SemaphoreFullException) { }
        }
    }

    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => _semaphore.WaitAsync(timeout, ct);
}

/// <summary>El cliente HTTP con que se mandan los webhooks.</summary>
internal static class WebhookHttpClient
{
    public const string Name = "webhook";

    /// <summary>
    /// Comprueba la IP al conectar, no sólo al guardar: un nombre que al guardar apuntaba fuera
    /// podría apuntar dentro el día del envío (DNS rebinding). Se resuelve aquí y se conecta a la
    /// IP comprobada, no a lo que el nombre diga un instante después.
    /// </summary>
    public static SocketsHttpHandler CreateHandler(WebhookOptions options) => new()
    {
        AllowAutoRedirect = false,   // un 302 hacia una dirección interna sería la misma puerta
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            var allowed = options.AllowPrivateNetworks
                ? addresses
                : addresses.Where(a => !PrivateAddresses.IsPrivate(a)).ToArray();

            if (allowed.Length == 0)
                throw new HttpRequestException("El destino resuelve a una dirección interna: no se envía");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };
}
