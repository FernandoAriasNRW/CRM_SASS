using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Reporting.Domain.Events;
using Reporting.Domain.ValueObjects;

namespace Reporting.Domain.Entities;

/// <summary>
/// El fichero generado, en su propia tabla.
///
/// <b>Separado de <see cref="Export"/> porque son bytes.</b> Listar las exportaciones de un
/// informe es una consulta que se hace cada pocos segundos mientras alguien espera; si los bytes
/// estuvieran en la misma fila, cada sondeo arrastraría los ficheros enteros aunque la pantalla
/// sólo pinte un estado.
///
/// <b>Y están en la base de datos, no en disco ni en Cloudinary.</b> Las tres opciones se
/// miraron:
///
/// - <i>Disco del contenedor</i>: se pierde al reiniciar, y con dos réplicas el fichero está en
///   una y la descarga llega a la otra.
/// - <i>Cloudinary</i>, que es el almacenamiento que ya hay configurado: sube por la ruta de
///   imágenes (<c>ImageUploadParams</c>), que no es donde va un <c>.xlsx</c>, y además exige
///   credenciales que en desarrollo no están puestas.
/// - <i>La base</i>: el fichero hereda el aislamiento por inquilino sin escribir una línea, se
///   borra con su exportación y no añade infraestructura nueva.
///
/// El límite de esta decisión, para saber cuándo cambiarla: son informes de esta aplicación, de
/// miles de filas, no de millones. Cuando un fichero pase de unas decenas de megas, esto debe
/// mudarse a almacenamiento de objetos —y entonces sólo cambia esta clase, porque nadie más toca
/// los bytes—.
/// </summary>
public sealed class ExportContent : Entity, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid ExportId { get; private set; }
    public byte[] Bytes { get; private set; } = [];
    public string ContentType { get; private set; } = string.Empty;

    private ExportContent() { }

    public static ExportContent Create(Guid tenantId, Guid exportId, byte[] bytes, string contentType)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExportId = exportId,
            Bytes = bytes,
            ContentType = contentType
        };
}
