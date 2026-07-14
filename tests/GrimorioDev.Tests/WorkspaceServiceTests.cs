using GrimorioDev.Application.DTOs;
using GrimorioDev.Application.Services;
using GrimorioDev.Domain.Interfaces;
using NSubstitute;
using Shouldly;

namespace GrimorioDev.Tests;

public sealed class WorkspaceServiceTests
{
    private readonly IWorkspaceRepository _repo = Substitute.For<IWorkspaceRepository>();
    private readonly WorkspaceService _sut;

    public WorkspaceServiceTests()
    {
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<WorkspaceService>>();
        _sut = new WorkspaceService(_repo, logger);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ShouldReturnDto()
    {
        _repo.GetDefaultLocation().Returns(@"C:\workspaces");
        _repo.SaveAsync(Arg.Any<GrimorioDev.Domain.Entities.Workspace>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new CreateWorkspaceRequest("Test WS", "A test", @"C:\ws_test");
        var result = await _sut.CreateWorkspaceAsync(request);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("Test WS");
        result.Description.ShouldBe("A test");
        result.Location.ShouldBe(@"C:\ws_test");
        result.Id.ShouldNotBe(Guid.Empty);
        result.CreatedAt.ShouldBeGreaterThan(DateTime.MinValue);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_UsesDefaultLocation_WhenNull()
    {
        _repo.GetDefaultLocation().Returns(@"C:\default_ws");
        _repo.SaveAsync(Arg.Any<GrimorioDev.Domain.Entities.Workspace>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new CreateWorkspaceRequest("Default Loc");
        var result = await _sut.CreateWorkspaceAsync(request);

        result.Location.ShouldBe(@"C:\default_ws");
    }

    [Fact]
    public async Task OpenWorkspaceAsync_ShouldReturnDto()
    {
        var ws = GrimorioDev.Domain.Entities.Workspace.Create("Test", @"C:\ws", "desc");
        _repo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);
        _repo.SaveAsync(Arg.Any<GrimorioDev.Domain.Entities.Workspace>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _sut.OpenWorkspaceAsync(ws.Id);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("Test");
    }

    [Fact]
    public async Task OpenWorkspaceAsync_ShouldReturnNull_WhenNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((GrimorioDev.Domain.Entities.Workspace?)null);

        var result = await _sut.OpenWorkspaceAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListWorkspacesAsync_ShouldReturnAll()
    {
        var ws1 = GrimorioDev.Domain.Entities.Workspace.Create("A", @"C:\a", "");
        var ws2 = GrimorioDev.Domain.Entities.Workspace.Create("B", @"C:\b", "");
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([ws1, ws2]);

        var result = await _sut.ListWorkspacesAsync();

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_ShouldReturnTrue_WhenExists()
    {
        _repo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _repo.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await _sut.DeleteWorkspaceAsync(Guid.NewGuid());

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_ShouldReturnFalse_WhenNotExists()
    {
        _repo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.DeleteWorkspaceAsync(Guid.NewGuid());

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveWorkspaceAsync_ShouldUpdateCardCountAndSize()
    {
        var ws = GrimorioDev.Domain.Entities.Workspace.Create("Test", @"C:\ws", "");
        _repo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);
        _repo.SaveAsync(Arg.Any<GrimorioDev.Domain.Entities.Workspace>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new SaveWorkspaceRequest(ws.Id, 42, 1024);
        var result = await _sut.SaveWorkspaceAsync(request);

        result.ShouldNotBeNull();
        result.CardCount.ShouldBe(42);
        result.SizeBytes.ShouldBe(1024);
    }

    [Fact]
    public async Task GetWorkspaceAsync_ShouldReturnDto()
    {
        var ws = GrimorioDev.Domain.Entities.Workspace.Create("Test", @"C:\ws", "");
        _repo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);

        var result = await _sut.GetWorkspaceAsync(ws.Id);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("Test");
    }

    [Fact]
    public async Task GetWorkspaceAsync_ShouldReturnNull_WhenNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((GrimorioDev.Domain.Entities.Workspace?)null);

        var result = await _sut.GetWorkspaceAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }
}
