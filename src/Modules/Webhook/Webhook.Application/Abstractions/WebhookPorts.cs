namespace Webhook.Application.Abstractions;

/// <summary>
/// Avisa al trabajo de entrega de que hay envíos nuevos, para que no espere a su próxima vuelta.
/// Si el aviso se pierde no pasa nada: el trabajo también mira la cola cada pocos segundos.
/// </summary>
public interface IWebhookDeliverySignal
{
    void Notify();
}

/// <summary>
/// A qué direcciones se puede mandar.
///
/// Un webhook hace que el servidor llame a una URL que escribe un usuario. Sin límites, esa URL
/// podría ser la de un servicio interno —la base de datos, el panel de la nube en
/// <c>169.254.169.254</c>— y el servidor haría de puente hacia dentro (SSRF). Por eso se rechazan
/// las direcciones de red privada y de bucle local, salvo en desarrollo y en pruebas, donde la
/// configuración lo permite. La comprobación se repite al conectar, con la IP ya resuelta: un
/// nombre que hoy apunta fuera podría apuntar dentro mañana.
/// </summary>
public interface IWebhookUrlPolicy
{
    /// <summary>El motivo por el que no se acepta la URL, o <c>null</c> si se acepta.</summary>
    string? Reject(string url);
}
