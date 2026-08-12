using FluentAssertions;
using Projects.Application.Abstractions;
using Xunit;
using NSubstitute;
using Projects.Application.Commands;
using Projects.Application.Queries;
using Projects.Application.Handlers.Commands;
using Projects.Application.Handlers.Queries;
using Projects.Application.Abstractions.Repositories;
using Projects.Application.Abstractions.Queries;
using Projects.Application.DTOs;
using Projects.Domain.Entities;
using Projects.Domain.ValueObjects;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;


namespace UnitTests;

public class ProjectsTests
{
    private readonly IProjectRepository _repositoryMock;
    private readonly IProjectsUnitOfWork _unitOfWorkMock;
    private readonly IProjectQueries _queriesMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _spaceId = Guid.NewGuid();

    public ProjectsTests()
    {
        _repositoryMock = Substitute.For<IProjectRepository>();
        _unitOfWorkMock = Substitute.For<IProjectsUnitOfWork>();
        _queriesMock = Substitute.For<IProjectQueries>();
    }

    #region Project Domain Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsProject()
    {
        // Arrange & Act
        var project = Project.Create(
            _tenantId, _spaceId, null, "New Project", "Project description",
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)), _ownerId
        );

        // Assert
        project.Should().NotBeNull();
        project.Name.Value.Should().Be("New Project");
        project.Description.Should().Be("Project description");
        project.Status.Name.Should().Be("Planificado");
        project.TenantId.Should().Be(_tenantId);
        project.OwnerId.Should().Be(_ownerId);
    }

    [Fact]
    public void Patch_UpdatesFields()
    {
        // Arrange
        var project = Project.Create(
            _tenantId, _spaceId, null, "Original Name", "Original Description",
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)), _ownerId
        );

        // Act
        project.Update(
            "Updated Name",
            null, // Description
            null  // EndDate
        );

        // Assert
        project.Name.Value.Should().Be("Updated Name");
        project.Description.Should().Be("Original Description");
    }

    #endregion

    #region CreateProjectCommandHandler Tests

    [Fact]
    public async Task CreateProject_WithValidCommand_ReturnsProject()
    {
        // Arrange
        var handler = new CreateProjectCommandHandler(_repositoryMock, _unitOfWorkMock);
        var command = new CreateProjectCommand(
            TenantId: _tenantId,
            SpaceId: _spaceId,
            FolderId: null,
            OwnerId: _ownerId,
            Name: "Test Project",
            Description: "Project for testing",
            EstimatedEndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3))
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Value.Should().Be("Test Project");
        
        await _repositoryMock.Received(1).AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetProjectsQueryHandler Tests

    [Fact]
    public async Task GetProjects_ReturnsProjectsForTenant()
    {
        // Arrange
        var p1 = Project.Create(
            _tenantId, _spaceId, null, "Project 1", "Desc", DateOnly.FromDateTime(DateTime.UtcNow), _ownerId);
        var p2 = Project.Create(
            _tenantId, _spaceId, null, "Project 2", "Desc", DateOnly.FromDateTime(DateTime.UtcNow), _ownerId);

        var pagedResult = PagedResult<ProjectDto>.Create(
            new List<ProjectDto>
            {
                new ProjectDto(p1.Id, p1.TenantId, p1.SpaceId, p1.FolderId, p1.Name.Value, p1.Description, p1.StartDate, p1.EstimatedEndDate, p1.Status.Name, p1.OwnerId),
                new ProjectDto(p2.Id, p2.TenantId, p2.SpaceId, p2.FolderId, p2.Name.Value, p2.Description, p2.StartDate, p2.EstimatedEndDate, p2.Status.Name, p2.OwnerId)
            },
            2, 1, 10
        );

        _queriesMock.GetByTenantAsync(_tenantId, null, null, null, null, null, null, 1, 10, Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var getHandler = new GetProjectsQueryHandler(_queriesMock);
        var query = new GetProjectsQuery(_tenantId, null, null, null, null, null, null, new PaginationRequest { Page = 1, PageSize = 10 });

        // Act
        var result = await getHandler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.First().Name.Should().Be("Project 1");
    }

    #endregion

    #region PatchProjectCommandHandler Tests

    [Fact]
    public async Task PatchProject_UpdatesProject()
    {
        // Arrange
        var project = Project.Create(
            _tenantId, _spaceId, null, "Original Name", "Original Description",
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), _ownerId
        );
        _repositoryMock.GetByIdAsync(_tenantId, project.Id, false, Arg.Any<CancellationToken>())
            .Returns(project);

        var patchHandler = new PatchProjectCommandHandler(_repositoryMock, _unitOfWorkMock);
        var patchCommand = new PatchProjectCommand(
            TenantId: _tenantId,
            Id: project.Id,
            Name: "Updated Name",
            Description: null,
            Status: "Done",
            EstimatedEndDate: null
        );

        // Act
        var result = await patchHandler.Handle(patchCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Name.Value.Should().Be("Updated Name");
        project.Status.Name.Should().Be("Done");

        await _repositoryMock.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region DeleteProjectCommandHandler Tests

    [Fact]
    public async Task DeleteProject_RemovesProject()
    {
        // Arrange
        var project = Project.Create(
            _tenantId, _spaceId, null, "To Delete", "Description",
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), _ownerId
        );
        _repositoryMock.GetByIdAsync(_tenantId, project.Id, false, Arg.Any<CancellationToken>())
            .Returns(project);

        var deleteHandler = new DeleteProjectCommandHandler(_repositoryMock, _unitOfWorkMock);
        var deleteCommand = new DeleteProjectCommand(_tenantId, project.Id, _ownerId);

        // Act
        var result = await deleteHandler.Handle(deleteCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.IsDeleted.Should().BeTrue();

        await _repositoryMock.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
    }

    #endregion


}