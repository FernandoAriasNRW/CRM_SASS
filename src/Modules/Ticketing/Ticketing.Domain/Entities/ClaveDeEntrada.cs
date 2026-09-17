using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Domain.Primitives;

namespace Ticketing.Domain.Entities;

/// <summary>
/// Una clave con la que alguien de fuera de la aplicación abre tickets en una organización: el
/// formulario de soporte de su web, o su propio backend.
///
/// <b>Sólo sirve para crear tickets.</b> No es una sesión ni un token: no lee nada, no edita nada
/// y no pasa ninguna comprobación de <c>RequireAuthorization()</c>. Por eso puede ir escrita en
/// una página pública sin abrir el resto de la API, que es lo que hacía el token de invitado que
/// sustituye.
///
/// <b>La organización sale de la clave, no de la petición.</b> Quien envía el ticket no puede
/// elegir en qué organización cae, y no hace falta ningún identificador público de organización.
///
/// <b>Se guarda el hash, nunca la clave.</b> Se enseña una sola vez al crearla; si se pierde, se
/// revoca y se crea otra. Con la base de datos en la mano no se pueden abrir tickets en nombre de
/// nadie.
/// </summary>
public sealed class ClaveDeEntrada : Entity, ITenantEntity
{
    /// <summary>Lo que va delante de toda clave, para reconocerla en un registro o en un repositorio.</summary>
    public const string Prefijo = "tke_";

    public Guid TenantId { get; private set; }

    /// <summary>Para quién o para qué es: «Web de soporte», «Backend de facturación».</summary>
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Los primeros caracteres de la clave, para distinguirlas en la lista sin enseñarlas.</summary>
    public string Inicio { get; private set; } = string.Empty;

    /// <summary>SHA-256 de la clave, en hexadecimal. Es lo único con lo que se busca.</summary>
    public string Hash { get; private set; } = string.Empty;

    public Guid CreadaPor { get; private set; }
    public DateTime CreadaUtc { get; private set; }
    public DateTime? UltimoUsoUtc { get; private set; }
    public DateTime? RevocadaUtc { get; private set; }

    public bool EstaActiva => RevocadaUtc is null;

    private ClaveDeEntrada() { }

    /// <summary>
    /// Crea una clave nueva. Devuelve también la clave en claro, que no se vuelve a poder leer.
    /// </summary>
    public static (ClaveDeEntrada Clave, string EnClaro) Generar(Guid tenantId, string nombre, Guid creadaPor, DateTime ahoraUtc)
    {
        // 32 bytes de aleatoriedad criptográfica: adivinarla por fuerza bruta no es un riesgo
        // aunque el endpoint sea público.
        var enClaro = Prefijo + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var clave = new ClaveDeEntrada
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Nombre = nombre.Trim(),
            Inicio = enClaro[..(Prefijo.Length + 6)],
            Hash = HashDe(enClaro),
            CreadaPor = creadaPor,
            CreadaUtc = ahoraUtc
        };

        return (clave, enClaro);
    }

    public static string HashDe(string enClaro)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(enClaro))).ToLowerInvariant();

    public void Revocar(DateTime ahoraUtc) => RevocadaUtc ??= ahoraUtc;

    public void Usada(DateTime ahoraUtc) => UltimoUsoUtc = ahoraUtc;
}
