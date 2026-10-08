using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Identity.Application.Abstractions.Services;
using Identity.Application.DTOs;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El tiempo real: que conecte, y que cada organización oiga sólo lo suyo.
///
/// Dos fallos que se tapaban el uno al otro:
/// <list type="bullet">
/// <item>La API no leía el token de la cadena de consulta, que es por donde lo manda SignalR en
/// WebSockets (el navegador no deja poner cabeceras al abrirlo). Los hubs exigen autenticación,
/// así que la conexión daba 401 y el tiempo real no funcionaba en ninguna pantalla.</item>
/// <item>Al conectar, <c>JoinTickets</c>, <c>JoinBoard</c> y <c>JoinChannel</c> metían la conexión
/// en el grupo que dijera el cliente: con el identificador de otra organización se oían sus
/// cambios. El primer fallo impedía explotar el segundo; arreglar sólo el primero lo abría.</item>
/// </list>
///
/// Las conexiones van por WebSockets con el token en la URL, como las del navegador. El «intruso»
/// es un usuario de otra organización con un token válido, firmado por la propia API.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RealtimeFlowTests(CrmApiFactory factory)
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private async Task<(HttpClient Client, string Token)> AuthenticateAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return (client, token);
    }

    /// <summary>Un token válido de una organización que no es la del administrador.</summary>
    private string ForeignToken()
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var stranger = new UserDto(Guid.NewGuid(), Guid.NewGuid(), "Otra organización", "intruso@otra.test", "Admin");
        return jwt.GenerateTokens(stranger).accessToken;
    }

    private HubConnection Connect(string hub, string? token)
    {
        var url = new Uri(factory.Server.BaseAddress, token is null ? hub : $"{hub}?access_token={token}");

        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, cancellationToken) =>
                    await factory.Server.CreateWebSocketClient().ConnectAsync(context.Uri, cancellationToken);
            })
            .Build();
    }

    [Fact]
    public async Task A_hub_accepts_the_token_in_the_query_string_and_rejects_no_token()
    {
        var (_, token) = await AuthenticateAsync();

        await using var withToken = Connect("hubs/tickets", token);
        await withToken.StartAsync();
        withToken.State.Should().Be(HubConnectionState.Connected,
            "el navegador manda el token como access_token en la URL; sin leerlo de ahí, 401");

        await using var anonymous = Connect("hubs/chat", null);
        var anonymousStart = () => anonymous.StartAsync();
        await anonymousStart.Should().ThrowAsync<Exception>(
            "el chat no pedía autenticación: cualquiera, sin sesión, podía escuchar un canal");
    }

    [Fact]
    public async Task A_ticket_move_reaches_my_organization_and_not_another()
    {
        var (client, token) = await AuthenticateAsync();

        await using var mine = Connect("hubs/tickets", token);
        await using var stranger = Connect("hubs/tickets", ForeignToken());

        var received = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var leaked = new List<Guid>();
        mine.On<JsonElement>("ticket_moved", t => received.TrySetResult(t.GetProperty("ticketId").GetGuid()));
        stranger.On<JsonElement>("ticket_moved", t => leaked.Add(t.GetProperty("ticketId").GetGuid()));

        await mine.StartAsync();
        await stranger.StartAsync();
        await mine.InvokeAsync("JoinTickets");
        await stranger.InvokeAsync("JoinTickets");

        var creation = await client.PostAsJsonAsync("/api/v1/tickets", new
        {
            Title = "Ticket que se mueve en tiempo real",
            Description = "Creado por las pruebas del tiempo real",
            Priority = "Medium"
        });
        creation.EnsureSuccessStatusCode();
        var ticketId = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await client.PatchAsJsonAsync($"/api/v1/tickets/{ticketId}", new { Status = "InProgress" }))
            .EnsureSuccessStatusCode();

        (await received.Task.WaitAsync(Wait)).Should().Be(ticketId);

        // Lo que se manda a un grupo sale a la vez para todos sus miembros; con un margen sobra.
        await Task.Delay(500);
        leaked.Should().NotContain(ticketId, "otra organización no debe ver moverse mis tickets");
    }

    /// <summary>
    /// Aquí el intruso pide el tablero <b>con el identificador de mi proyecto</b>, que es lo que
    /// antes bastaba para oírlo.
    /// </summary>
    [Fact]
    public async Task Joining_another_organizations_board_by_its_id_hears_nothing()
    {
        var (client, token) = await AuthenticateAsync();
        var projectId = Guid.NewGuid();

        var creation = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId,
            title = "Tarea que se mueve en el tablero",
            description = "Creada por las pruebas del tiempo real",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 1m,
            dueDate = "2026-12-01",
            priority = "Normal"
        });
        creation.EnsureSuccessStatusCode();
        var taskId = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using var mine = Connect("hubs/board", token);
        await using var stranger = Connect("hubs/board", ForeignToken());

        var received = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var leaked = new List<Guid>();
        mine.On<JsonElement>("task_moved", t => received.TrySetResult(t.GetProperty("taskId").GetGuid()));
        stranger.On<JsonElement>("task_moved", t => leaked.Add(t.GetProperty("taskId").GetGuid()));

        await mine.StartAsync();
        await stranger.StartAsync();
        await mine.InvokeAsync("JoinBoard", projectId);
        await stranger.InvokeAsync("JoinBoard", projectId);

        (await client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}", new { status = "In Progress" }))
            .EnsureSuccessStatusCode();

        (await received.Task.WaitAsync(Wait)).Should().Be(taskId);

        await Task.Delay(500);
        leaked.Should().BeEmpty("saber el identificador de un proyecto ajeno no da acceso a su tablero");
    }

    /// <summary>
    /// El mensaje nuevo llega a quien tiene el canal abierto. Communication guardaba sin repartir
    /// el evento, así que el aviso no salía nunca, ni siquiera dentro de la misma organización.
    /// </summary>
    [Fact]
    public async Task A_new_message_reaches_the_open_channel_and_not_another_organization()
    {
        var (client, token) = await AuthenticateAsync();

        var channel = await client.PostAsJsonAsync("/api/v1/channels", new { name = "#tiempo-real", type = "Channel" });
        channel.EnsureSuccessStatusCode();
        var channelId = (await channel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using var mine = Connect("hubs/chat", token);
        await using var stranger = Connect("hubs/chat", ForeignToken());

        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var leaked = new List<JsonElement>();
        mine.On<JsonElement>("message_received", m => received.TrySetResult(m));
        stranger.On<JsonElement>("message_received", m => leaked.Add(m));

        await mine.StartAsync();
        await stranger.StartAsync();
        await mine.InvokeAsync("JoinChannel", channelId);
        await stranger.InvokeAsync("JoinChannel", channelId);

        (await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", new { content = "Hola en tiempo real" }))
            .EnsureSuccessStatusCode();

        var message = await received.Task.WaitAsync(Wait);
        message.GetProperty("conversationId").GetGuid().Should().Be(channelId);
        message.GetProperty("content").GetString().Should().Be("Hola en tiempo real");

        await Task.Delay(500);
        leaked.Should().BeEmpty("el canal de otra organización no se oye aunque se sepa su identificador");
    }
}
