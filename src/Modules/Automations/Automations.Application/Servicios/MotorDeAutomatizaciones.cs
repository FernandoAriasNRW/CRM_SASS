using Automations.Application.Abstractions;
using Automations.Domain.Servicios;
using Microsoft.Extensions.Logging;

namespace Automations.Application.Servicios;

/// <summary>
/// Ejecuta las reglas que corresponden a un evento.
///
/// Dos decisiones que definen el comportamiento:
///
/// **Una acción que falla no impide las demás.** Las reglas de un inquilino son independientes
/// entre sí, y que una esté mal configurada —un estado que ya no existe— no puede dejar sin
/// ejecutar a las otras. El fallo se registra; no se propaga.
///
/// **El motor nunca hace fallar la operación que disparó el evento.** Mover una tarea no puede
/// devolver un error porque una automatización esté rota: quien la movió no configuró esa regla
/// y no puede hacer nada al respecto. Por eso todo va dentro de un try y sale por el registro.
/// </summary>
public sealed class MotorDeAutomatizaciones(
    IAutomationRuleRepository repositorio,
    IAutomationsUnitOfWork unitOfWork,
    IEjecutorDeAcciones ejecutor,
    ILogger<MotorDeAutomatizaciones> log) : IMotorDeAutomatizaciones
{
    /// <summary>
    /// Si el hilo lógico actual ya está aplicando una automatización.
    ///
    /// Va en un <see cref="AsyncLocal{T}"/> porque las acciones se ejecutan dentro del mismo
    /// flujo asíncrono que el evento que las disparó: la acción cambia la tarea, eso emite otro
    /// evento, y ese evento vuelve aquí. Sin esta marca, dos reglas que se deshacen la una a la
    /// otra —una pone «En progreso», otra al verlo lo devuelve a «Por hacer»— se llamarían hasta
    /// tumbar el proceso.
    /// </summary>
    private static readonly AsyncLocal<bool> _aplicandoAcciones = new();

    public async Task<int> EjecutarAsync(DisparoDeAutomatizacion disparo, CancellationToken ct = default)
    {
        // Las acciones de una automatización no disparan otras automatizaciones. Encadenarlas
        // exigiría detectar ciclos y un presupuesto de profundidad, y prometerlo a medias sería
        // peor: la cascada funcionaría casi siempre y un día se comería la base de datos.
        if (_aplicandoAcciones.Value)
        {
            log.LogDebug(
                "Se ignora el disparador {Disparador} sobre {Entidad}: viene de otra automatización",
                disparo.Disparador, disparo.EntityId);
            return 0;
        }

        var reglas = await repositorio.GetActivasPorDisparadorAsync(
            disparo.TenantId, disparo.Disparador, ct);

        if (reglas.Count == 0) return 0;

        var ejecutadas = 0;

        foreach (var regla in reglas)
        {
            if (!EvaluadorDeCondiciones.Cumple(regla.Condiciones, disparo.Datos)) continue;

            var alguna = false;

            foreach (var accion in regla.Acciones)
            {
                _aplicandoAcciones.Value = true;
                try
                {
                    await ejecutor.EjecutarAsync(
                        disparo.TenantId, disparo.EntityId, accion.Tipo, accion.Valor, ct);
                    alguna = true;
                }
                catch (Exception ex)
                {
                    log.LogError(ex,
                        "La automatización {Regla} no pudo aplicar {Accion} sobre {Entidad}",
                        regla.Id, accion.Tipo, disparo.EntityId);
                }
                finally
                {
                    // En el `finally` y no después: si una acción falla, la marca tiene que
                    // levantarse igual o el resto de la petición se quedaría sin automatizaciones.
                    _aplicandoAcciones.Value = false;
                }
            }

            // Sólo cuenta como ejecutada si algo se llegó a aplicar. Un contador que sube cuando
            // todas las acciones fallaron diría que la regla funciona justo cuando no funciona.
            if (!alguna) continue;

            regla.AnotarEjecucion(DateTime.UtcNow);
            await repositorio.UpdateAsync(regla, ct);
            ejecutadas++;
        }

        if (ejecutadas > 0)
            await unitOfWork.SaveChangesAsync(ct);

        return ejecutadas;
    }
}
