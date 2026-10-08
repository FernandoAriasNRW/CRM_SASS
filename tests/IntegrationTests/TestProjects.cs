using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationTests;

/// <summary>
/// Un proyecto de verdad para colgar tareas.
///
/// Toda tarea pertenece a un proyecto que existe en la organización. Las pruebas usaban un
/// <c>Guid</c> inventado como proyecto, que la API aceptaba sin mirar; ahora lo rechaza.
/// </summary>
internal static class TestProjects
{
    public static async Task<Guid> CreateAsync(HttpClient client, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/projects", new
        {
            spaceId = Guid.NewGuid(),
            name = name ?? "Proyecto de prueba " + Guid.NewGuid().ToString("N")[..8],
            description = "Creado por las pruebas para colgar tareas",
            estimatedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
        });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
