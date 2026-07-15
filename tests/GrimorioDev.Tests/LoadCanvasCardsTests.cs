using GrimorioDev.Application.DTOs;
using GrimorioDev.Application.Interfaces;
using GrimorioDev.Application.UseCases;
using GrimorioDev.Domain.Entities;

namespace GrimorioDev.Tests;

public sealed class LoadCanvasCardsTests
{
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly IWorkspaceSessionService _session = Substitute.For<IWorkspaceSessionService>();
    private readonly LoadCanvasCards _sut;

    public LoadCanvasCardsTests()
    {
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<LoadCanvasCards>>();
        _sut = new LoadCanvasCards(_cardRepo, _session, logger);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnCards()
    {
        var wsId = Guid.NewGuid();
        _session.CurrentWorkspace.Returns(new WorkspaceDto(
            wsId, "WS", "", "", DateTime.UtcNow, DateTime.UtcNow,
            DateTime.UtcNow, DateTime.UtcNow, 0, 0));

        var cards = new List<Card>
        {
            Card.Create("Card A", new CardPosition(0, 0, 0)),
            Card.Create("Card B", new CardPosition(100, 50, 1))
        };
        _cardRepo.GetAllAsync(wsId, Arg.Any<CancellationToken>()).Returns(cards);

        var result = await _sut.ExecuteAsync();

        result.Count.ShouldBe(2);
        result[0].Title.ShouldBe("Card A");
        result[1].Title.ShouldBe("Card B");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmpty_WhenNoWorkspace()
    {
        _session.CurrentWorkspace.Returns((WorkspaceDto?)null);

        var result = await _sut.ExecuteAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmpty_WhenNoCards()
    {
        var wsId = Guid.NewGuid();
        _session.CurrentWorkspace.Returns(new WorkspaceDto(
            wsId, "Empty", "", "", DateTime.UtcNow, DateTime.UtcNow,
            DateTime.UtcNow, DateTime.UtcNow, 0, 0));
        _cardRepo.GetAllAsync(wsId, Arg.Any<CancellationToken>()).Returns(new List<Card>());

        var result = await _sut.ExecuteAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapAllProperties()
    {
        var wsId = Guid.NewGuid();
        _session.CurrentWorkspace.Returns(new WorkspaceDto(
            wsId, "WS", "", "", DateTime.UtcNow, DateTime.UtcNow,
            DateTime.UtcNow, DateTime.UtcNow, 0, 0));

        var card = Card.Create("Full", new CardPosition(10.5, 20.7, 2), 350, 250);
        card.UpdateContent("hello");
        _cardRepo.GetAllAsync(wsId, Arg.Any<CancellationToken>()).Returns(new List<Card> { card });

        var result = await _sut.ExecuteAsync();

        var dto = result[0];
        dto.Id.ShouldBe(card.Id);
        dto.Title.ShouldBe("Full");
        dto.Content.ShouldBe("hello");
        dto.X.ShouldBe(10.5);
        dto.Y.ShouldBe(20.7);
        dto.ZIndex.ShouldBe(2);
        dto.Width.ShouldBe(350);
        dto.Height.ShouldBe(250);
        dto.IsPinned.ShouldBeFalse();
    }
}
