using Server.Application.Services;
using Server.Domain;

namespace SeriousGame.UnitTests;

public class TenderCatalogServiceTests
{
    [Fact]
    public void OpenFirstRound_AddsOneRoundWithThreeTenders()
    {
        var owner = new Player { Id = "p1", Nickname = "Owner", ConnectionId = "c1" };
        var game = new Game { Name = "Test Game", Owner = owner };

        var service = new TenderCatalogService();
        var round = service.OpenFirstRound(game);

        Assert.Single(game.Rounds);
        Assert.Equal(3, round.Tenders.Count);
        Assert.Same(game, round.Game);
    }
}