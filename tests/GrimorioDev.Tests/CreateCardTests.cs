using GrimorioDev.Application.DTOs;
using GrimorioDev.Application.Interfaces;
using GrimorioDev.Application.UseCases;
using GrimorioDev.Domain.Entities;

namespace GrimorioDev.Tests;

public sealed class CreateCardTests
{
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly IWorkspaceSessionService _session = Substitute.For<IWorkspaceSessionService>();
    private readonly CreateCard _sut;

    public CreateCardTests()
    {
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<CreateCard>>();
        _sut = new CreateCard(_cardRepo, _session, logger);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateAndReturnDto()
    {
        var wsId = Guid.NewGuid();
        _session.CurrentWorkspace.Returns(new WorkspaceDto(
            wsId, "WS", "", "", DateTime.UtcNow, DateTime.UtcNow,
            DateTime.UtcNow, DateTime.UtcNow, 0, 0));

        var request = new CreateCardRequest("New Card", "hello", 100, 200, 0);
        var result = await _sut.ExecuteAsync(request);

        result.ShouldNotBeNull();
        result.Title.ShouldBe("New Card");
        result.Content.ShouldBe("hello");
        result.X.ShouldBe(100);
        result.Y.ShouldBe(200);
        result.ZIndex.ShouldBe(0);
        result.Width.ShouldBe(300);
        result.Height.ShouldBe(200);
        result.IsPinned.ShouldBeFalse();
        result.Id.ShouldNotBe(Guid.Empty);

        await _cardRepo.Received(1).SaveAsync(wsId, Arg.Is<Card>(c =>
            c.Title == "New Card" &&
            c.Content == "hello" &&
            c.Position.X == 100 &&
            c.Position.Y == 200),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseCustomSize()
    {
        var wsId = Guid.NewGuid();
        _session.CurrentWorkspace.Returns(new WorkspaceDto(
            wsId, "WS", "", "", DateTime.UtcNow, DateTime.UtcNow,
            DateTime.UtcNow, DateTime.UtcNow, 0, 0));

        var request = new CreateCardRequest("Big", "", 0, 0, 0, 500, 400);
        var result = await _sut.ExecuteAsync(request);

        result.ShouldNotBeNull();
        result.Width.ShouldBe(500);
        result.Height.ShouldBe(400);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseDefaultContent()
    {
        var wsId = Guid.NewGuid();
        _session.CurrentWorkspace.Returns(new WorkspaceDto(
            wsId, "WS", "", "", DateTime.UtcNow, DateTime.UtcNow,
            DateTime.UtcNow, DateTime.UtcNow, 0, 0));

        var request = new CreateCardRequest("Empty", null!, 10, 20, 1);
        var result = await _sut.ExecuteAsync(request);

        result.ShouldNotBeNull();
        result.Content.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNull_WhenNoWorkspace()
    {
        _session.CurrentWorkspace.Returns((WorkspaceDto?)null);

        var result = await _sut.ExecuteAsync(new CreateCardRequest("Fail", "", 0, 0, 0));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOnEmptyTitle()
    {
        var wsId = Guid.NewGuid();
        _session.CurrentWorkspace.Returns(new WorkspaceDto(
            wsId, "WS", "", "", DateTime.UtcNow, DateTime.UtcNow,
            DateTime.UtcNow, DateTime.UtcNow, 0, 0));

        await Should.ThrowAsync<ArgumentException>(async () =>
            await _sut.ExecuteAsync(new CreateCardRequest("", "", 0, 0, 0)));
    }
}
