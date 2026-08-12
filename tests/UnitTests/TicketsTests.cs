using FluentAssertions;
using Ticketing.Application.Abstractions;
using Xunit;
using NSubstitute;
using Ticketing.Application.Commands;
using Ticketing.Application.Queries;
using Ticketing.Application.Handlers.Commands;
using Ticketing.Application.Handlers.Queries;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Application.Abstractions.Queries;
using Ticketing.Application.DTOs;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;


namespace UnitTests;

public class TicketsTests
{
    private readonly ITicketRepository _repositoryMock;
    private readonly ITicketingUnitOfWork _unitOfWorkMock;
    private readonly ITicketQueries _queriesMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public TicketsTests()
    {
        _repositoryMock = Substitute.For<ITicketRepository>();
        _unitOfWorkMock = Substitute.For<ITicketingUnitOfWork>();
        _queriesMock = Substitute.For<ITicketQueries>();
    }

    #region Ticket Domain Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsTicket()
    {
        // Arrange & Act
        var priority = TicketPriority.FromName<TicketPriority>("High")!;
        var ticket = Ticket.Create(
            _tenantId, _customerId, "Test Subject", "Message", priority
        ).Value!;

        // Assert
        ticket.Should().NotBeNull();
        ticket.Title.Should().Be("Test Subject");
        ticket.Description.Should().Be("Message");
        ticket.Priority.Name.Should().Be("High");
        ticket.Status.Name.Should().Be("Open");
        ticket.AssignedAgentId.Should().BeNull();
    }

    [Fact]
    public void Assign_ChangesStatusAndAssignedUser()
    {
        // Arrange
        var priority = TicketPriority.FromName<TicketPriority>("High")!;
        var ticket = Ticket.Create(
            _tenantId, _customerId, "Ticket to Assign", "Message", priority
        ).Value!;

        // Act
        ticket.AssignTo(_agentId);

        // Assert
        ticket.AssignedAgentId.Should().Be(_agentId);
    }

    #endregion

    #region CreateTicketHandler Tests

    [Fact]
    public async Task CreateTicket_WithValidCommand_ReturnsTicket()
    {
        // Arrange
        var handler = new CreateTicketHandler(_repositoryMock, _unitOfWorkMock);
        var command = new CreateTicketCommand(
            TenantId: _tenantId,
            CustomerId: _customerId,
            Title: "New Ticket",
            Description: "I need help with my account",
            Priority: "High"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().Be("New Ticket");
        
        await _repositoryMock.Received(1).AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region AssignTicketHandler Tests

    [Fact]
    public async Task AssignTicket_WithValidTicket_ReturnsTrue()
    {
        // Arrange
        var priority = TicketPriority.FromName<TicketPriority>("High")!;
        var ticket = Ticket.Create(
            _tenantId, _customerId, "Ticket to Assign", "Message", priority
        ).Value!;

        _repositoryMock.GetByIdAsync(_tenantId, ticket.Id, Arg.Any<CancellationToken>())
            .Returns(ticket);

        var assignHandler = new AssignTicketHandler(_repositoryMock, _unitOfWorkMock);
        var assignCommand = new AssignTicketCommand(_tenantId, ticket.Id, _agentId);

        // Act
        var result = await assignHandler.Handle(assignCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ticket.AssignedAgentId.Should().Be(_agentId);

        await _repositoryMock.Received(1).UpdateAsync(ticket, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetTicketsQueryHandler Tests

    [Fact]
    public async Task GetTickets_ReturnsTicketsForTenant()
    {
        // Arrange
        var t1 = Ticket.Create(_tenantId, _customerId, "Ticket 1", "Desc", TicketPriority.FromName<TicketPriority>("High")!).Value!;
        
        var pagedResult = PagedResult<TicketDto>.Create(
            new List<TicketDto>
            {
                new TicketDto(t1.Id, t1.TenantId, t1.CustomerId, t1.AssignedAgentId, t1.Title, t1.Description, t1.Priority.ToString(), t1.Status.ToString(), t1.CreatedAt, t1.ResolvedAt)
            },
            1, 1, 10
        );

        // El handler usa GetByTenantWithPaginationAsync, no GetByTenantAsync.
        _queriesMock.GetByTenantWithPaginationAsync(
                _tenantId, null, null, null, null,
                Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var getHandler = new GetTicketsHandler(_queriesMock);
        var query = new GetTicketsQuery(_tenantId, null, null, null, null, new PaginationRequest { Page = 1, PageSize = 10 });

        // Act
        var result = await getHandler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.First().Title.Should().Be("Ticket 1");
    }

    [Fact]
    public async Task GetTicketById_ReturnsCorrectTicket()
    {
        // Arrange
        var priority = TicketPriority.FromName<TicketPriority>("High")!;
        var ticket = Ticket.Create(
            _tenantId, _customerId, "Specific Ticket", "Message", priority
        ).Value!;

        _repositoryMock.GetByIdAsync(_tenantId, ticket.Id, Arg.Any<CancellationToken>())
            .Returns(ticket);

        var getByIdHandler = new GetTicketByIdHandler(_repositoryMock);
        var query = new GetTicketByIdQuery(_tenantId, ticket.Id);

        // Act
        var result = await getByIdHandler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().Be("Specific Ticket");
    }

    #endregion


}