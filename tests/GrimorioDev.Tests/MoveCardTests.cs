using GrimorioDev.Application.DTOs;
using GrimorioDev.Application.Interfaces;
using GrimorioDev.Application.UseCases;
using GrimorioDev.Domain.Entities;

namespace GrimorioDev.Tests;

public sealed class MoveCardTests
{
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly IWorkspaceSessionService _session = Substitute.For<IWorkspaceSessionService>();
    private readonly MoveCard _sut;

    public MoveCardTests()
    {
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<MoveCard>>();
        _sut = new MoveCard(_cardRepo, _session, logger);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMoveCard()
    {
        var wsId = Guid.NewGuid();
        _session.CurrentWorkspace.Returns(new WorkspaceDto(
            wsId, "WS", "", "", DateTime.UtcNow, DateTime.UtcNow,
            DateTime.UtcNow, DateTime.UtcNow, 0, 0));

        var card = Card.Create("Movable", new CardPosition(0, 0, 0));
        _cardRepo.GetByIdAsync(wsId, card.Id, Arg.Any<CancellationToken>()).Returns(card);

        var request = new MoveCardRequest(card.Id, 200, 300, 5);
        var result = await _sut.ExecuteAsync(request);

        result.ShouldNotBeNull();
        result.X.ShouldBe(200);
        result.Y.ShouldBe(300);
        result.ZIndex.ShouldBe(5);
        result.Title.ShouldBe("Movable");

        await _cardRepo.Received(1).SaveAsync(wsId, Arg.Is<Card>(c =>
            c.Position.X == 200 && c.Position.Y == 300 && c.Position.ZIndex == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNull_WhenNoWorkspace()
    {
        _session.CurrentWorkspace.Returns((WorkspaceDto?)null);

        var result = await _sut.ExecuteAsync(new MoveCardRequest(Guid.NewGuid(), 0, 0, 0));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNull_WhenCardNotFound()
    {
        var wsId = Guid.NewGuid();
        _session.CurrentWorkspace.Returns(new WorkspaceDto(
            wsId, "WS", "", "", DateTime.UtcNow, DateTime.UtcNow,
            DateTime.UtcNow, DateTime.UtcNow, 0, 0));
        _cardRepo.GetByIdAsync(wsId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Card?)null);

        var result = await _sut.ExecuteAsync(new MoveCardRequest(Guid.NewGuid(), 10, 20, 1));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUpdateUpdatedAt()
    {
        var wsId = Guid.NewGuid();
        _session.CurrentWorkspace.Returns(new WorkspaceDto(
            wsId, "WS", "", "", DateTime.UtcNow, DateTime.UtcNow,
            DateTime.UtcNow, DateTime.UtcNow, 0, 0));

        var card = Card.Create("Time", new CardPosition(0, 0, 0));
        var originalUpdated = card.UpdatedAt;
        _cardRepo.GetByIdAsync(wsId, card.Id, Arg.Any<CancellationToken>()).Returns(card);

        Thread.Sleep(1);
        var result = await _sut.ExecuteAsync(new MoveCardRequest(card.Id, 50, 60, 2));

        result!.UpdatedAt.ShouldBeGreaterThan(originalUpdated);
    }
}
