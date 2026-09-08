using FluentAssertions;
using WorkItems.Application.Abstractions;
using Xunit;
using NSubstitute;
using WorkItems.Application.Commands;
using WorkItems.Application.Queries;
using WorkItems.Application.Handlers.Commands;
using WorkItems.Application.Handlers.Queries;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Application.Abstractions.Queries;
using WorkItems.Application.DTOs;
using WorkItems.Domain.Entities;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;


namespace UnitTests;

public class WorkItemsTests
{
    private readonly ITaskRepository _repositoryMock;
    private readonly IWorkItemsUnitOfWork _unitOfWorkMock;
    private readonly ITaskQueries _queriesMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    public WorkItemsTests()
    {
        _repositoryMock = Substitute.For<ITaskRepository>();
        _unitOfWorkMock = Substitute.For<IWorkItemsUnitOfWork>();
        _queriesMock = Substitute.For<ITaskQueries>();
    }

    #region WorkTask Domain Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsTask()
    {
        // Arrange & Act
        var task = WorkTask.Create(
            _tenantId, _projectId, "Test Task", "Task description",
            _userId, _adminId, 8, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
        );

        // Assert
        task.Should().NotBeNull();
        task.Title.Value.Should().Be("Test Task");
        task.Status.Value.ToString().Should().Be("To Do");
        task.TenantId.Should().Be(_tenantId);
        task.ProjectId.Should().Be(_projectId);
    }

    [Fact]
    public void Move_ChangesStatus()
    {
        // Arrange
        var task = WorkTask.Create(
            _tenantId, _projectId, "Test Task", "Task description",
            _userId, _adminId, 8, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
        );

        // Act
        task.Move("In Progress");

        // Assert
        task.Status.Value.ToString().Should().Be("In Progress");
    }

    #endregion

    #region CreateTaskCommandHandler Tests

    [Fact]
    public async Task CreateTask_WithValidCommand_ReturnsTask()
    {
        // Arrange
        var handler = new CreateTaskCommandHandler(_repositoryMock, _unitOfWorkMock);
        var command = new CreateTaskCommand(
            TenantId: _tenantId,
            CreatedById: _adminId,
            ProjectId: _projectId,
            Title: "New Task",
            Description: "Task description",
            AssigneeId: _userId,
            EstimatedHours: 8,
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Value.Should().Be("New Task");

        await _repositoryMock.Received(1).AddAsync(Arg.Any<WorkTask>(), Arg.Any<CancellationToken>());

        // El alta guarda **repartiendo los eventos en proceso**, y no sólo al outbox: de ahí
        // salen los disparos de las automatizaciones y el aviso al tablero. Comprobar el método
        // concreto no es puntillismo: con el otro, la tarea se guarda igual y nada de eso ocurre.
        await _unitOfWorkMock.Received(1).SaveChangesAndDispatchAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region MoveTaskCommandHandler Tests

    [Fact]
    public async Task MoveTask_WithValidStatus_ReturnsTrue()
    {
        // Arrange
        var task = WorkTask.Create(
            _tenantId, _projectId, "Task to Move", "Description",
            _userId, _adminId, 8, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
        );
        _repositoryMock.GetByIdAsync(_tenantId, task.Id, Arg.Any<CancellationToken>())
            .Returns(task);

        var moveHandler = new MoveTaskCommandHandler(_repositoryMock, _unitOfWorkMock);
        var moveCommand = new MoveTaskCommand(
            TenantId: _tenantId,
            Id: task.Id,
            ActorId: _userId,
            ActorRole: "Member",
            NewStatus: "In Progress"
        );

        // Act
        var result = await moveHandler.Handle(moveCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Status.Value.ToString().Should().Be("In Progress");

        await _repositoryMock.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveTask_NonAssigneeMemberCannotMoveOthersTask()
    {
        // Arrange
        var task = WorkTask.Create(
            _tenantId, _projectId, "Task to Move", "Description",
            _userId, _adminId, 8, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
        );
        _repositoryMock.GetByIdAsync(_tenantId, task.Id, Arg.Any<CancellationToken>())
            .Returns(task);

        var moveHandler = new MoveTaskCommandHandler(_repositoryMock, _unitOfWorkMock);
        var moveCommand = new MoveTaskCommand(
            TenantId: _tenantId,
            Id: task.Id,
            ActorId: Guid.NewGuid(), // Different user
            ActorRole: "Member",
            NewStatus: "In Progress"
        );

        // Act
        var result = await moveHandler.Handle(moveCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No tiene permisos");
    }

    #endregion

    #region ReparentTaskCommandHandler Tests

    /// <summary>
    /// Las dos reglas de anidamiento que el agregado no puede comprobar solo, porque hablan de
    /// otras filas. La tercera —no ser su propio padre— la cubre WorkTaskInvariantsTests.
    /// </summary>
    private WorkTask TareaDePrueba(string titulo = "Tarea", Guid? padre = null) => WorkTask.Create(
        _tenantId, _projectId, titulo, "Descripción", _userId, _adminId, 4,
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), null, padre);

    [Fact]
    public async Task Reparent_NoSePuedeColgarDeUnaSubtarea()
    {
        var subtarea = TareaDePrueba("Subtarea", padre: Guid.NewGuid());
        var tarea = TareaDePrueba();
        _repositoryMock.GetByIdAsync(_tenantId, tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        _repositoryMock.GetByIdAsync(_tenantId, subtarea.Id, Arg.Any<CancellationToken>()).Returns(subtarea);

        var handler = new ReparentTaskCommandHandler(_repositoryMock, _unitOfWorkMock);
        var result = await handler.Handle(
            new ReparentTaskCommand(_tenantId, tarea.Id, _adminId, "Admin", subtarea.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(WorkTask.ReglasDeAnidamiento.PadreEsSubtarea);
        tarea.ParentTaskId.Should().BeNull("un rechazo no debe dejar la tarea a medio colgar");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reparent_UnaTareaConSubtareasNoPuedeVolverseSubtarea()
    {
        var tarea = TareaDePrueba("Con subtareas");
        var futuroPadre = TareaDePrueba("Padre");
        _repositoryMock.GetByIdAsync(_tenantId, tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        _repositoryMock.GetByIdAsync(_tenantId, futuroPadre.Id, Arg.Any<CancellationToken>()).Returns(futuroPadre);
        _repositoryMock.CountSubtasksAsync(_tenantId, tarea.Id, Arg.Any<CancellationToken>()).Returns(3);

        var handler = new ReparentTaskCommandHandler(_repositoryMock, _unitOfWorkMock);
        var result = await handler.Handle(
            new ReparentTaskCommand(_tenantId, tarea.Id, _adminId, "Admin", futuroPadre.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(WorkTask.ReglasDeAnidamiento.TieneSubtareas);
    }

    [Fact]
    public async Task Reparent_ElPadreTieneQueSerDelMismoProyecto()
    {
        var tarea = TareaDePrueba();
        var deOtroProyecto = WorkTask.Create(
            _tenantId, Guid.NewGuid(), "De otro proyecto", "x", _userId, _adminId, 1,
            DateOnly.FromDateTime(DateTime.UtcNow));
        _repositoryMock.GetByIdAsync(_tenantId, tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        _repositoryMock.GetByIdAsync(_tenantId, deOtroProyecto.Id, Arg.Any<CancellationToken>()).Returns(deOtroProyecto);

        var handler = new ReparentTaskCommandHandler(_repositoryMock, _unitOfWorkMock);
        var result = await handler.Handle(
            new ReparentTaskCommand(_tenantId, tarea.Id, _adminId, "Admin", deOtroProyecto.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(WorkTask.ReglasDeAnidamiento.PadreDeOtroProyecto);
    }

    [Fact]
    public async Task Reparent_DesligarNoConsultaNadaYGuarda()
    {
        var tarea = TareaDePrueba("Subtarea", padre: Guid.NewGuid());
        _repositoryMock.GetByIdAsync(_tenantId, tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);

        var handler = new ReparentTaskCommandHandler(_repositoryMock, _unitOfWorkMock);
        var result = await handler.Handle(
            new ReparentTaskCommand(_tenantId, tarea.Id, _adminId, "Admin", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tarea.EsSubtarea.Should().BeFalse();
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearSubtarea_DeUnPadreQueNoExisteSeRechaza()
    {
        _repositoryMock.GetByIdAsync(_tenantId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WorkTask?)null);

        var handler = new CreateTaskCommandHandler(_repositoryMock, _unitOfWorkMock);
        var result = await handler.Handle(new CreateTaskCommand(
            TenantId: _tenantId, CreatedById: _adminId, ProjectId: _projectId,
            Title: "Subtarea huérfana", Description: "x", AssigneeId: _userId,
            EstimatedHours: 1, DueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Priority: null, ParentTaskId: Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(WorkTask.ReglasDeAnidamiento.PadreNoExiste);
        await _repositoryMock.DidNotReceive().AddAsync(Arg.Any<WorkTask>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetTasksQueryHandler Tests

    [Fact]
    public async Task GetTasks_ReturnsTasksForTenant()
    {
        // Arrange
        var t1 = WorkTask.Create(_tenantId, _projectId, "Task 1", "Desc", _userId, _adminId, 8, DateOnly.FromDateTime(DateTime.UtcNow));
        
        var pagedResult = PagedResult<TaskDto>.Create(
            new List<TaskDto>
            {
                new TaskDto(t1.Id, t1.TenantId, t1.ProjectId, t1.Title.Value, t1.Description, t1.Status.Value.ToString(), t1.Priority.Value, t1.AssigneeId, t1.CreatedById, t1.EstimatedHours, t1.DueDate)
            },
            1, 1, 10
        );

        // El handler usa GetByTenantWithPaginationAsync, no GetByTenantAsync.
        _queriesMock.GetByTenantWithPaginationAsync(
                _tenantId, null, null, null, null, null, null, null, false,
                Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var getHandler = new GetTasksQueryHandler(_queriesMock);
        var query = new GetTasksQuery(_tenantId, null, null, null, null, null, null, new PaginationRequest { Page = 1, PageSize = 10 });

        // Act
        var result = await getHandler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
    }

    #endregion


}
