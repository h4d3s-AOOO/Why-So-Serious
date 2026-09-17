using Microsoft.Extensions.Options;
using Server.Application.Services;
using Server.Domain;
using Server.Infrastructure;
using Server.Options;

namespace SeriousGame.IntegrationTests;

public class GameServiceTests
{
    private static GameService BuildService(GameOptions options)
    {
        var appMemory = new AppMemory();
        var gameRepository = new InMemoryGameRepository(appMemory);
        return new GameService(gameRepository, Options.Create(options));
    }

    private static Player MakePlayer(string id) => new() { Id = id, Nickname = id, ConnectionId = $"conn-{id}" };

    [Fact]
    public void JoinGame_AddsPlayerWhenRoomAvailable()
    {
        var service = BuildService(new GameOptions { MaximumPlayers = 4 });
        var game = service.CreateGame("Test", MakePlayer("owner"));

        var joined = service.JoinGame(game.Id, MakePlayer("p2"));

        Assert.True(joined);
        Assert.Equal(2, game.Players.Count);
    }

    [Fact]
    public void JoinGame_RefusesWhenGameInProgress()
    {
        var service = BuildService(new GameOptions { MaximumPlayers = 8 });
        var game = service.CreateGame("Test", MakePlayer("owner"));
        game.IsInProgress = true;

        var joined = service.JoinGame(game.Id, MakePlayer("p2"));

        Assert.False(joined);
        Assert.Single(game.Players);
    }

    [Fact]
    public void JoinGame_RefusesWhenAtMaximumPlayers()
    {
        var service = BuildService(new GameOptions { MaximumPlayers = 2 });
        var game = service.CreateGame("Test", MakePlayer("owner")); // 1 joueur (owner)
        service.JoinGame(game.Id, MakePlayer("p2"));                 // 2 joueurs → plein

        var joined = service.JoinGame(game.Id, MakePlayer("p3"));

        Assert.False(joined);
        Assert.Equal(2, game.Players.Count);
    }

    [Fact]
    public void JoinGame_SamePlayerTwice_IsIdempotent()
    {
        var service = BuildService(new GameOptions { MaximumPlayers = 4 });
        var game = service.CreateGame("Test", MakePlayer("owner"));
        service.JoinGame(game.Id, MakePlayer("p2"));

        var joinedAgain = service.JoinGame(game.Id, MakePlayer("p2"));

        Assert.True(joinedAgain);
        Assert.Equal(2, game.Players.Count);
    }

    [Fact]
    public void CreateGame_StampsConfiguredParameters()
    {
        var service = BuildService(new GameOptions { MinimumPlayers = 2, MaximumPlayers = 5, RoundsNumber = 10 });

        var game = service.CreateGame("Test", MakePlayer("owner"));

        Assert.Equal(2, game.MinimumPlayers);
        Assert.Equal(5, game.MaximumPlayers);
        Assert.Equal(10, game.RoundsNumber);
    }

    [Fact]
    public void EnrollCompanies_CreatesOneCompanyPerPlayer()
    {
        var service = BuildService(new GameOptions { MaximumPlayers = 4 });
        var game = service.CreateGame("Test", MakePlayer("owner"));
        service.JoinGame(game.Id, MakePlayer("p2"));

        service.EnrollCompanies(game);

        Assert.Equal(2, game.Companies.Count);
        Assert.Contains(game.Companies, c => c.PlayerOwner.Id == "owner");
        Assert.Contains(game.Companies, c => c.PlayerOwner.Id == "p2");
    }
}

