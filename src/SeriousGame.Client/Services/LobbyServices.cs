using Client.Options;
using Client.Services.Interfaces;
using Client.State;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared;
using Shared.Abstractions;
using Shared.Models.Dtos;
using Shared.Models.Requests;

namespace Client.Services;

/// <summary>
/// Transport hub du lobby : appels sortants, réception des broadcasts (relayés en événements)
/// et mise à jour de la session. Aucune E/S console ici - la présentation appartient à App.
/// </summary>
public class LobbyServices : ILobbyServices
{
    private readonly ILogger<LobbyServices> _logger;
    private readonly ClientSession _session;
    private readonly HubConnection _lobbyConnection;
    private TaskCompletionSource? _gameStartingSignal;

    public event Action<GameDto, IReadOnlyList<string>>? WaitingForPlayers;
    public event Action<GameDto>? GameStarting;
    public event Action<string>? NotificationReceived;
    public event Action? ConnectionLost;
    public event Action? ConnectionRestored;

    public string HubUrl { get; }

    public LobbyServices(IOptions<WebSocketServerOptions> webSocketServerOptions, ILogger<LobbyServices> logger, ClientSession session)
    {
        _logger = logger;
        _session = session;
        var options = webSocketServerOptions.Value;
        HubUrl = $"{options.Scheme}://{options.Domain}:{options.Port}{HubRoutes.Lobby}";
        _lobbyConnection = new HubConnectionBuilder()
            .WithUrl(HubUrl)
            .WithAutomaticReconnect()
            .Build();

        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        _lobbyConnection.On<List<GameDto>>(nameof(ILobbyHubClient.GamesUpdated), games =>
        {
            _session.UpdateGames(games);
        });

        _lobbyConnection.On<GameDto, List<string>>(nameof(ILobbyHubClient.WaitingForPlayers), (game, playerNames) =>
        {
            WaitingForPlayers?.Invoke(game, playerNames);
        });

        _lobbyConnection.On<GameDto>(nameof(ILobbyHubClient.GameStarting), game =>
        {
            GameStarting?.Invoke(game);
            _gameStartingSignal?.TrySetResult();
        });

        _lobbyConnection.On<string>(nameof(ILobbyHubClient.Notify), msg =>
        {
            NotificationReceived?.Invoke(msg);
        });

        _lobbyConnection.On<GameDto>(nameof(ILobbyHubClient.UpdateGameInProgressWhenPlayerQuits), game =>
        {
            _session.MarkPlayerInactiveAfterOtherQuit(game);
        });

        _lobbyConnection.Reconnecting += error =>
        {
            ConnectionLost?.Invoke();
            return Task.CompletedTask;
        };

        _lobbyConnection.Reconnected += async id =>
        {
            await _lobbyConnection.InvokeAsync(nameof(ILobbyHubServer.UpdatePlayerConnectionId), _session.PlayerId);
            ConnectionRestored?.Invoke();
        };
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            await _lobbyConnection.StartAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec de connexion au hub {HubUrl}", HubUrl);
            return false;
        }
    }

    public async Task IdentifyClientAsync(string nickname)
    {
        _session.Identify(nickname);
        var command = new CreatePlayerCommand { PlayerId = _session.PlayerId, Nickname = nickname };
        await _lobbyConnection.InvokeAsync(nameof(ILobbyHubServer.IdentifyNewPlayer), command);
    }

    public async Task<EnrollmentResult> CreateGameAsync(string gameName)
    {
        PrepareGameStartSignal();

        var command = new CreateGameCommand { PlayerId = _session.PlayerId, GameName = gameName };
        var game = await _lobbyConnection.InvokeAsync<GameDto>(nameof(ILobbyHubServer.CreateGame), command);
        _session.EnterGame(game);

        return game.IsInProgress
            ? EnrollmentResult.GameStarting
            : EnrollmentResult.WaitingForPlayers;
    }

    public async Task<EnrollmentResult> JoinGameAsync(string gameId)
    {
        PrepareGameStartSignal();

        var command = new JoinGameCommand { PlayerId = _session.PlayerId, GameId = gameId };
        var game = await _lobbyConnection.InvokeAsync<GameDto?>(nameof(ILobbyHubServer.JoinGame), command);

        if (game is null)
            return EnrollmentResult.Failed;

        _session.EnterGame(game);

        return game.IsInProgress
            ? EnrollmentResult.GameStarting
            : EnrollmentResult.WaitingForPlayers;
    }

    private void PrepareGameStartSignal()
    {
        // Créé avant l'invoke hub : le handler GameStarting peut arriver sur un thread SignalR
        // avant que l'appelant n'atteigne le await - le signal doit déjà exister.
        _gameStartingSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task<bool> WaitForGameStartAsync(CancellationToken cancellationToken)
    {
        if (_gameStartingSignal is null) return true;

        try
        {
            await _gameStartingSignal.Task.WaitAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public async Task LeaveGameAsync()
    {
        if (_session.CurrentGame is null) return;

        await _lobbyConnection.InvokeAsync(nameof(ILobbyHubServer.LeaveGame));
        _session.LeaveCurrentGame();
    }

    public async Task SendAsync(string methodName, params object[] args)
    {
        if (_lobbyConnection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Appel de '{MethodName}' impossible : état de connexion {State}", methodName, _lobbyConnection.State);
            return;
        }

        try
        {
            await _lobbyConnection.InvokeCoreAsync(methodName, args);
        }
        catch (HubException hex)
        {
            _logger.LogWarning(hex, "HubException lors de l'appel de '{MethodName}'", methodName);
        }
        catch (InvalidOperationException iox)
        {
            _logger.LogWarning(iox, "Opération invalide lors de l'appel de '{MethodName}'", methodName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur inattendue lors de l'appel de '{MethodName}'", methodName);
        }
    }

    public async Task<T?> SendAsync<T>(string methodName, params object[] args)
    {
        if (_lobbyConnection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Appel de '{MethodName}' impossible : état de connexion {State}", methodName, _lobbyConnection.State);
            return default;
        }

        try
        {
            return await _lobbyConnection.InvokeCoreAsync<T>(methodName, args);
        }
        catch (HubException hex)
        {
            _logger.LogWarning(hex, "HubException lors de l'appel de '{MethodName}'", methodName);
        }
        catch (InvalidOperationException iox)
        {
            _logger.LogWarning(iox, "Opération invalide lors de l'appel de '{MethodName}'", methodName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur inattendue lors de l'appel de '{MethodName}'", methodName);
        }

        return default;
    }

    public async Task DisconnectAsync()
    {
        await _lobbyConnection.StopAsync();
    }
}
