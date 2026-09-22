using Microsoft.AspNetCore.SignalR;
using Server.Application.Abstractions;
using Server.Domain;
using Server.Hubs;
using Server.Resources;
using Shared.Abstractions;
using Shared.Models.Dtos;

namespace Server.Application.Services;

/// <summary>
/// Unique implémentation du flux du lobby : toute la logique métier create/join/leave/disconnect vit ici,
/// LobbyHub n'étant qu'un adaptateur.
/// </summary>
public class LobbyFlowService : ILobbyFlowService
{
    private readonly GameService _gameService;
    private readonly PlayerService _playerService;
    private readonly TenderCatalogService _tenderCatalogService;
    private readonly IHubContext<LobbyHub, ILobbyHubClient> _hub;

    public LobbyFlowService(
        GameService gameService,
        PlayerService playerService,
        TenderCatalogService tenderCatalogService,
        IHubContext<LobbyHub, ILobbyHubClient> hubContext)
    {
        _gameService = gameService;
        _playerService = playerService;
        _tenderCatalogService = tenderCatalogService;
        _hub = hubContext;
    }

    public Task IdentifyNewPlayer(CreatePlayerCommand command, string connectionId)
    {
        _playerService.CreatePlayer(command.PlayerId, command.Nickname, connectionId);
        return Task.CompletedTask;
    }

    public Task UpdatePlayerConnectionId(string clientId, string connectionId)
    {
        if (_playerService.HasPlayer(clientId))
        {
            _playerService.UpdateConnectionId(clientId, connectionId);
        }
        return Task.CompletedTask;
    }

    public async Task<GameDto> CreateGame(CreateGameCommand command, string connectionId)
    {
        var player = _playerService.GetPlayerById(command.PlayerId);
        var game = _gameService.CreateGame(command.GameName, player);

        await _hub.Groups.AddToGroupAsync(connectionId, game.Id);
        await NotifyLobbyGamesUpdated();

        return Mapper.ToDto(game);
    }

    public async Task<GameDto?> JoinGame(JoinGameCommand command, string connectionId)
    {
        var player = _playerService.GetPlayerById(command.PlayerId);
        var joined = _gameService.JoinGame(command.GameId, player);
        if (!joined) return null;

        await _hub.Groups.AddToGroupAsync(connectionId, command.GameId);

        var game = _gameService.GetGame(command.GameId);
        if (game is null) return null;

        if (game.Players.Count < game.MinimumPlayers)
        {
            var waitingDto = Mapper.ToDto(game);
            await _hub.Clients.Group(game.Id)
                .WaitingForPlayers(waitingDto, game.Players.Select(p => p.Nickname).ToList());
            return waitingDto;
        }

        game.IsInProgress = true;
        _gameService.EnrollCompanies(game);
        _tenderCatalogService.OpenFirstRound(game);

        var startingDto = Mapper.ToDto(game);

        await NotifyLobbyGamesUpdated();
        await _hub.Clients.Group(game.Id).GameStarting(startingDto);

        return startingDto;
    }

    public async Task LeaveGame(string connectionId)
    {
        var game = _playerService.RemovePlayerFromGame(connectionId);
        if (game is null) return;

        // Contrairement au disconnect, le joueur reste connecté : il faut le retirer
        // explicitement du groupe SignalR, sinon il continuerait de recevoir les broadcasts.
        await _hub.Groups.RemoveFromGroupAsync(connectionId, game.Id);
        await NotifyLobbyGamesUpdated();

        var gameDto = Mapper.ToDto(game);

        if (game.IsInProgress)
        {
            // Fenêtre de course : la partie a démarré pendant que le joueur annulait.
            await _hub.Clients.Group(game.Id).UpdateGameInProgressWhenPlayerQuits(gameDto);
        }
        else if (game.Players.Count > 0)
        {
            await _hub.Clients.Group(game.Id)
                .WaitingForPlayers(gameDto, game.Players.Select(p => p.Nickname).ToList());
        }
    }

    public async Task HandlePlayerDisconnect(string connectionId)
    {
        var game = _playerService.RemovePlayerFromGame(connectionId);

        if (game is not null)
        {
            await NotifyLobbyGamesUpdated();

            var gameDto = Mapper.ToDto(game);

            if (game.IsInProgress)
            {
                await _hub.Clients.Group(game.Id).UpdateGameInProgressWhenPlayerQuits(gameDto);
            }
            else if (game.Players.Count > 0)
            {
                await _hub.Clients.Group(game.Id)
                    .WaitingForPlayers(gameDto, game.Players.Select(p => p.Nickname).ToList());
            }
        }

        _playerService.RemovePlayerByConnectionId(connectionId);

        await SafeNotifyClient(connectionId, ServerResources.PlayerDisconnectedMessage);
    }

    public Task NotifyLobbyOnConnection(string connectionId)
    {
        var games = _gameService.GetGamesNotStarted().Select(Mapper.ToDto).ToList();
        return _hub.Clients.Client(connectionId).GamesUpdated(games);
    }

    private Task NotifyLobbyGamesUpdated()
    {
        var games = _gameService.GetGamesNotStarted().Select(Mapper.ToDto).ToList();
        return _hub.Clients.All.GamesUpdated(games);
    }

    private async Task SafeNotifyClient(string connectionId, string message)
    {
        try
        {
            await _hub.Clients.Client(connectionId).Notify(message);
        }
        catch
        {
            // Ignoré - le client est peut-être déjà complètement déconnecté.
        }
    }
}
