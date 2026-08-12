using FluentAssertions;
using Webhook.Application.Abstractions;
using Xunit;
using NSubstitute;
using Webhook.Application.Commands;
using Webhook.Application.Queries;
using Webhook.Application.Handlers.Commands;
using Webhook.Application.Handlers.Queries;
using Webhook.Application.Abstractions.Repositories;
using Webhook.Application.DTOs;
using Webhook.Domain.Entities;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;

namespace UnitTests;

public class WebhooksTests
{
    private readonly IWebhookSubscriptionRepository _repositoryMock;
    private readonly IWebhookUnitOfWork _unitOfWorkMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _subscriptionId = Guid.NewGuid();

    public WebhooksTests()
    {
        _repositoryMock = Substitute.For<IWebhookSubscriptionRepository>();
        _unitOfWorkMock = Substitute.For<IWebhookUnitOfWork>();
    }

    #region WebhookSubscription Domain Tests

    [Fact]
    public void Create_ReturnsSubscription()
    {
        // Arrange & Act
        var subscription = WebhookSubscription.Create(_tenantId, "TestEvent", "https://test.com", "secret");

        // Assert
        subscription.Should().NotBeNull();
        subscription.EventName.Should().Be("TestEvent");
        subscription.TargetUrl.Should().Be("https://test.com");
    }

    #endregion

    #region CreateWebhookSubscriptionHandler Tests

    [Fact]
    public async Task CreateSubscription_WithValidCommand_ReturnsDto()
    {
        // Arrange
        var handler = new CreateWebhookSubscriptionHandler(_repositoryMock, _unitOfWorkMock);
        var command = new CreateWebhookCommand("https://test.com", "TestEvent", _tenantId, "secret");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.EventName.Should().Be("TestEvent");

        await _repositoryMock.Received(1).AddAsync(Arg.Any<WebhookSubscription>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region UpdateWebhookSubscriptionHandler Tests

    [Fact]
    public async Task UpdateSubscription_ReturnsUpdatedDto()
    {
        // Arrange
        var subscription = WebhookSubscription.Create(_tenantId, "TestEvent", "https://test.com", "secret");
        _repositoryMock.GetByIdAsync(_tenantId, _subscriptionId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        var handler = new UpdateWebhookSubscriptionHandler(_repositoryMock, _unitOfWorkMock);
        var command = new UpdateWebhookSubscriptionCommand(_tenantId, _subscriptionId, "https://new.com", "newsecret");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TargetUrl.Should().Be("https://new.com");

        await _repositoryMock.Received(1).UpdateAsync(subscription, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region DeleteWebhookSubscriptionHandler Tests

    [Fact]
    public async Task DeleteSubscription_ReturnsTrue()
    {
        // Arrange
        var subscription = WebhookSubscription.Create(_tenantId, "TestEvent", "https://test.com", "secret");
        _repositoryMock.GetByIdAsync(_tenantId, _subscriptionId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        var handler = new DeleteWebhookSubscriptionHandler(_repositoryMock, _unitOfWorkMock);
        var command = new DeleteWebhookSubscriptionCommand(_tenantId, _subscriptionId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _repositoryMock.Received(1).DeleteAsync(subscription, Arg.Any<CancellationToken>());
    }

    #endregion

    #region QueryHandlers Tests

    [Fact]
    public async Task GetSubscriptions_ReturnsList()
    {
        // Arrange
        var subscription = WebhookSubscription.Create(_tenantId, "TestEvent", "https://test.com", "secret");
        _repositoryMock.GetByTenantAsync(_tenantId, null, Arg.Any<CancellationToken>())
            .Returns(new List<WebhookSubscription> { subscription });

        var handler = new GetWebhookSubscriptionsHandler(_repositoryMock);
        var query = new GetWebhookSubscriptionsQuery(_tenantId, null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].EventName.Should().Be("TestEvent");
    }

    #endregion
}
