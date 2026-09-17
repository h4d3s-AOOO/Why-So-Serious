using Microsoft.Extensions.Options;
using Server.Application.Abstractions;
using Server.Domain;
using Server.Options;

namespace Server.Application.Services;

public class GameService
{
    private readonly IGameRepository _gameRepository;
    private readonly GameOptions _gameOptions;

    public GameService(IGameRepository gameRepository, IOptions<GameOptions> gameOptions)
    {
        _gameRepository = gameRepository;
        _gameOptions = gameOptions.Value;
    }

    public ICollection<Game> GetGamesNotStarted() => _gameRepository.GetAll().Where(g => !g.IsInProgress).ToList();

    public Game CreateGame(string gameName, Player owner)
    {
        var game = GameFactory.Create(gameName, owner, _gameOptions);
        _gameRepository.Add(game);
        return game;
    }

    public bool JoinGame(string gameId, Player player)
    {
        var game = _gameRepository.GetAll().FirstOrDefault(r => r.Id == gameId);
        if (game is null) return false;

        // Déjà dans la partie : idempotent, on ne l'ajoute pas deux fois.
        if (game.Players.Any(p => p.Id == player.Id)) return true;

        // Une partie démarrée n'accepte plus de nouveaux joueurs, ni une partie pleine.
        if (game.IsInProgress) return false;
        if (game.Players.Count >= game.MaximumPlayers) return false;

        game.Players.Add(player);
        return true;
    }

    public void EnrollCompanies(Game game)
    {
        foreach (var enrolledPlayer in game.Players)
        {
            game.Companies.Add(new Company
            {
                Name = $"{enrolledPlayer.Nickname} & Co",
                PlayerOwner = enrolledPlayer
            });
        }
    }

    public bool DisconnectPlayer(string gameId, Player player)
    {
        var game = _gameRepository.GetAll().FirstOrDefault(r => r.Id == gameId);
        if (game == null) return false;
        game.Players.Remove(player);
        _gameRepository.Remove(game);
        return true;
    }

    public bool StartGame(string gameId)
    {
        var game = _gameRepository.GetAll().FirstOrDefault(r => r.Id == gameId);
        if (game == null) return false;
        game.IsInProgress = true;

        return true;
    }

    public Game? GetGame(string gameId) => _gameRepository.GetAll().FirstOrDefault(r => r.Id == gameId);
}
