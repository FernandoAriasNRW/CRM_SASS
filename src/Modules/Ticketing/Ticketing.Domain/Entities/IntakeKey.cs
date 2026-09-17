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
public sealed class IntakeKey : Entity, ITenantEntity
{
    /// <summary>Lo que va delante de toda clave, para reconocerla en un registro o en un repositorio.</summary>
    public const string KeyPrefix = "tke_";

    public Guid TenantId { get; private set; }

    /// <summary>Para quién o para qué es: «Web de soporte», «Backend de facturación».</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Los primeros caracteres de la clave, para distinguirlas en la lista sin enseñarlas.</summary>
    public string Prefix { get; private set; } = string.Empty;

    /// <summary>SHA-256 de la clave, en hexadecimal. Es lo único con lo que se busca.</summary>
    public string Hash { get; private set; } = string.Empty;

    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsActive => RevokedAtUtc is null;

    private IntakeKey() { }

    /// <summary>
    /// Crea una clave nueva. Devuelve también la clave en claro, que no se vuelve a poder leer.
    /// </summary>
    public static (IntakeKey Key, string PlainText) Generate(Guid tenantId, string name, Guid createdBy, DateTime nowUtc)
    {
        // 32 bytes de aleatoriedad criptográfica: adivinarla por fuerza bruta no es un riesgo
        // aunque el endpoint sea público.
        var plainText = KeyPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var key = new IntakeKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Prefix = plainText[..(KeyPrefix.Length + 6)],
            Hash = HashOf(plainText),
            CreatedBy = createdBy,
            CreatedAtUtc = nowUtc
        };

        return (key, plainText);
    }

    public static string HashOf(string plainText)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainText))).ToLowerInvariant();

    public void Revoke(DateTime nowUtc) => RevokedAtUtc ??= nowUtc;

    public void MarkUsed(DateTime nowUtc) => LastUsedAtUtc = nowUtc;
}
