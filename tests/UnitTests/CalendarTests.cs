using FluentAssertions;
using Calendar.Application.Abstractions;
using Xunit;
using NSubstitute;
using Calendar.Application.Commands;
using Calendar.Application.Queries;
using Calendar.Application.Handlers.Commands;
using Calendar.Application.Handlers.Queries;
using Calendar.Application.Abstractions.Repositories;
using Calendar.Application.Abstractions.Queries;
using Calendar.Application.DTOs;
using Calendar.Domain.Entities;
using Calendar.Domain.ValueObjects;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;


namespace UnitTests;

public class CalendarTests
{
    private readonly ICalendarEventRepository _repositoryMock;
    private readonly Calendar.Application.Abstractions.ICalendarUnitOfWork _unitOfWorkMock;
    private readonly ICalendarEventQueries _queriesMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public CalendarTests()
    {
        _repositoryMock = Substitute.For<ICalendarEventRepository>();
        _unitOfWorkMock = Substitute.For<Calendar.Application.Abstractions.ICalendarUnitOfWork>();
        _queriesMock = Substitute.For<ICalendarEventQueries>();

        _unitOfWorkMock.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    [Fact]
    public async Task CreateEvent_WithValidCommand_ReturnsSuccessAndDto()
    {
        // Arrange
        var handler = new CreateCalendarEventHandler(_repositoryMock, _unitOfWorkMock);
        var now = DateTime.UtcNow;
        var command = new CreateCalendarEventCommand(
            TenantId: _tenantId,
            OrganizerId: _userId,
            Title: "Team Meeting",
            StartTime: now.AddHours(1),
            EndTime: now.AddHours(2),
            Type: "Meeting",
            Description: "Weekly sync"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().Be("Team Meeting");
        result.Value.Description.Should().Be("Weekly sync");
        result.Value.TenantId.Should().Be(_tenantId);
        result.Value.OrganizerId.Should().Be(_userId);

        await _repositoryMock.Received(1).AddAsync(Arg.Any<CalendarEvent>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }


    #region GetEventsQueryHandler Tests

    [Fact]
    public async Task GetEvents_ReturnsEventsForTenant()
    {
        // Arrange
        var evt1 = CalendarEvent.Create(_tenantId, _userId, "Event 1", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), CalendarEventType.FromName<CalendarEventType>("Meeting")!).Value!;
        var evt2 = CalendarEvent.Create(_tenantId, _userId, "Event 2", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), CalendarEventType.FromName<CalendarEventType>("Reminder")!).Value!;
        
        var pagedResult = PagedResult<CalendarEventDto>.Create(
            new List<CalendarEventDto>
            {
                evt1.ToDto(),
                evt2.ToDto()
            },
            2, 1, 10
        );

        _queriesMock.GetByTenantAsync(_tenantId, null, null, null, Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var getHandler = new GetEventsHandler(_queriesMock);
        var query = new GetEventsQuery(_tenantId, null, null, null, new PaginationRequest { Page = 1, PageSize = 10 });

        // Act
        var result = await getHandler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().Contain(e => e.Title == "Event 1");
    }

    #endregion
}