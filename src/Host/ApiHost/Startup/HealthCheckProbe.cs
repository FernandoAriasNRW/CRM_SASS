namespace ApiHost.Startup;

/// <summary>
/// La sonda de salud del contenedor: <c>dotnet ApiHost.dll --health-check</c>.
///
/// Existe para que la imagen no necesite <c>curl</c> ni <c>wget</c>. La imagen de ASP.NET no trae
/// ninguno de los dos, y el <c>healthcheck</c> del compose invocaba <c>curl</c> igualmente: llevaba
/// 1.590 fallos consecutivos y el contenedor figuraba como «unhealthy» de forma permanente.
/// Con <c>depends_on: condition: service_healthy</c> eso deja el arranque colgado, y en un
/// orquestador es un servicio al que nunca se le enruta tráfico.
///
/// La alternativa era instalar <c>curl</c> con apt en la imagen. Se descartó: añade una descarga
/// de red a cada construcción —que ya falló una vez, dejando el build entero roto por algo
/// que no es del proyecto—, engorda la imagen y suma superficie de CVE para pedir una URL.
/// El proceso que ya sabe responder es el mismo que se está comprobando.
///
/// Se ejecuta antes de construir el host a propósito: no levanta servidor, no toca la base y no
/// aplica migraciones. Sólo pregunta y devuelve 0 o 1.
/// </summary>
public static class HealthCheckProbe
{
    public const string Argument = "--health-check";

    public static async Task<int> RunAsync()
    {
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';')[0] ?? "8080";

        // `/health/live` y no `/health/ready`: «vivo» pregunta si el proceso responde, «listo»
        // pregunta además por la base de datos. Reiniciar el contenedor porque la base está caída
        // no arregla la base y sí tira las conexiones que aún funcionaban.
        using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            // `127.0.0.1` y no `localhost`: dentro de un contenedor `localhost` puede resolver
            // primero a `::1`, y si el servidor sólo escucha en IPv4 la sonda falla mientras el
            // servicio funciona. Le pasa a la imagen del frontend con nginx.
            var response = await probe.GetAsync($"http://127.0.0.1:{port}/health/live");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex)
        {
            // A stderr: lo recoge `docker inspect` en el registro de la comprobación, y es lo
            // único que verá quien intente entender por qué el contenedor no está sano.
            await Console.Error.WriteLineAsync($"La sonda de salud falló: {ex.Message}");
            return 1;
        }
    }
}
