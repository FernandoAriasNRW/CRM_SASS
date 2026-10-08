using System.Text.Json;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Webhook.Application;
using Webhook.Domain;
using Webhook.Domain.Entities;
using Xunit;

namespace UnitTests;

/// <summary>
/// Las reglas de los webhooks que no necesitan base de datos: qué se puede suscribir, cuándo se
/// reintenta, qué se manda y cuándo se dispara.
/// </summary>
public sealed class WebhooksTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static WebhookSubscription New(params string[] events)
        => WebhookSubscription.Create(DateTime.UtcNow, Tenant, "Mi webhook", "https://example.com/hook", events, "whsec_x");

    // ── La suscripción ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_subscription_listens_to_the_events_it_names_and_no_others()
    {
        var subscription = New("task.created", "ticket.created");

        subscription.Subscribes("task.created").Should().BeTrue();
        subscription.Subscribes("project.created").Should().BeFalse();
    }

    /// <summary>Nunca «todo» por defecto: sin eventos no hay suscripción.</summary>
    [Fact]
    public void A_subscription_without_events_is_rejected()
    {
        var create = () => New();
        create.Should().Throw<InvalidOperationException>().WithMessage(WebhookSubscription.Rules.EventsRequired);
    }

    [Fact]
    public void Events_outside_the_catalog_and_wildcards_are_rejected()
    {
        var create = () => New("task.created", "*", "task.exploded");
        create.Should().Throw<InvalidOperationException>().WithMessage("*task.exploded*");
    }

    [Theory]
    [InlineData("ftp://example.com/hook")]
    [InlineData("example.com/hook")]
    [InlineData("")]
    public void Only_full_http_or_https_urls_are_accepted(string url)
    {
        var create = () => WebhookSubscription.Create(DateTime.UtcNow, Tenant, "x", url, ["task.created"], "whsec_x");
        create.Should().Throw<InvalidOperationException>().WithMessage(WebhookSubscription.Rules.InvalidUrl);
    }

    [Fact]
    public void A_deactivated_subscription_listens_to_nothing()
    {
        var subscription = New("task.created");
        subscription.Update(DateTime.UtcNow, subscription.Name, subscription.TargetUrl, subscription.EventTypes, isActive: false);

        subscription.Subscribes("task.created").Should().BeFalse();
    }

    // ── Los reintentos ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_failed_delivery_waits_longer_each_time_and_gives_up_in_the_end()
    {
        var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var delivery = WebhookDelivery.Create(now, Tenant, Guid.NewGuid(), "task.created", _ => "{}");

        delivery.RecordFailure(now, 500, "El destino respondió 500");
        delivery.Status.Should().Be(WebhookDeliveryStatus.Pending);
        delivery.NextAttemptAtUtc.Should().Be(now + WebhookDelivery.RetryDelays[0]);

        for (var i = 1; i < WebhookDelivery.MaxAttempts; i++)
            delivery.RecordFailure(now, 500, "El destino respondió 500");

        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);
        delivery.Attempts.Should().Be(WebhookDelivery.MaxAttempts);
    }

    [Fact]
    public void The_delivery_id_travels_inside_the_body()
    {
        var delivery = WebhookDelivery.Create(DateTime.UtcNow, Tenant, Guid.NewGuid(), "task.created",
            id => WebhookPayload.Build(id, "task.created", Tenant, DateTime.UtcNow, null, null));

        JsonDocument.Parse(delivery.Payload).RootElement.GetProperty("id").GetGuid().Should().Be(delivery.Id);
    }

    // ── Lo que se manda ──────────────────────────────────────────────────────────────────────

    private sealed record NewUser(Guid TenantId, string Name, string Email, string Password, string Role);
    private sealed record Created(Guid Id, string Name, string PasswordHash, Nested Settings);
    private sealed record Nested(string ApiToken, string Theme);

    /// <summary>
    /// El comando de crear un usuario lleva la contraseña en claro, y se mandaba tal cual a
    /// cualquier suscriptor de «usuario creado».
    /// </summary>
    [Fact]
    public void Passwords_secrets_tokens_and_hashes_never_leave()
    {
        var json = WebhookPayload.Build(Guid.NewGuid(), "user.created", Tenant, DateTime.UtcNow,
            new NewUser(Tenant, "Ana", "ana@acme.com", "S3creta!", "Member"),
            new Created(Guid.NewGuid(), "Ana", "hash", new Nested("tok_123", "dark")));

        json.Should().NotContain("S3creta!").And.NotContain("tok_123").And.NotContain("\"hash\"");

        var data = JsonDocument.Parse(json).RootElement.GetProperty("data");
        data.GetProperty("input").GetProperty("email").GetString().Should().Be("ana@acme.com");
        data.GetProperty("result").GetProperty("settings").GetProperty("theme").GetString().Should().Be("dark",
            "se quita lo sensible, no el resto: el suscriptor necesita el identificador y los datos");
    }

    // ── Cuándo se dispara ────────────────────────────────────────────────────────────────────

    private sealed record CreateThing(Guid TenantId) : IRequest<Result<Guid>>, IWebhookTriggered
    {
        public string WebhookEventName => "task.created";
    }

    /// <summary>
    /// Sólo se reconocían <c>Result</c> y <c>Result&lt;bool&gt;</c>: un <c>Result&lt;Guid&gt;</c>
    /// fallido se daba por bueno y el webhook anunciaba algo que no había pasado.
    /// </summary>
    [Fact]
    public async Task A_failed_command_does_not_fire_the_webhook()
    {
        var publisher = Substitute.For<IPublisher>();
        var behavior = new WebhookDispatchBehavior<CreateThing, Result<Guid>>(publisher, NullLogger<WebhookDispatchBehavior<CreateThing, Result<Guid>>>.Instance);

        await behavior.Handle(new CreateThing(Tenant), _ => Task.FromResult(Result<Guid>.Failure("no")), CancellationToken.None);

        await publisher.DidNotReceive().Publish(Arg.Any<WebhookEventNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_successful_command_fires_it_with_its_result()
    {
        var publisher = Substitute.For<IPublisher>();
        var behavior = new WebhookDispatchBehavior<CreateThing, Result<Guid>>(publisher, NullLogger<WebhookDispatchBehavior<CreateThing, Result<Guid>>>.Instance);
        var id = Guid.NewGuid();

        await behavior.Handle(new CreateThing(Tenant), _ => Task.FromResult(Result<Guid>.Success(id)), CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<WebhookEventNotification>(n => n.EventName == "task.created" && n.TenantId == Tenant && Equals(n.Result, id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void The_catalog_has_no_repeated_names()
    {
        WebhookEventCatalog.All.Select(e => e.Name).Should().OnlyHaveUniqueItems();
        WebhookEventCatalog.Exists(WebhookEventCatalog.Test).Should().BeFalse(
            "el evento de prueba no se puede suscribir: sólo lo manda «Enviar prueba»");
    }
}
