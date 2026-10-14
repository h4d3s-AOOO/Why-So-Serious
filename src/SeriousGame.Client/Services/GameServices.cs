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
/// Connexion du client au hub /game : réception du catalogue du tour (RoundStarted),
/// envoi des actions du joueur (candidature, fin de tour) et attente de la résolution du tour.
/// </summary>
public class GameServices : IGameServices
{
    private readonly ILogger<GameServices> _logger;
    private readonly ClientSession _session;
    private readonly string _baseUrl;
    private HubConnection? _gameConnection;

    // Terminée quand le serveur signale la fin du tour (RoundResolved ou GameOver)
    private TaskCompletionSource? _roundEnd;

    public event Action<RoundCatalogDto>? RoundStarted;
    public event Action? WaitingForOtherPlayers;

    public GameServices(IOptions<WebSocketServerOptions> webSocketServerOptions, ILogger<GameServices> logger, ClientSession session)
    {
        _logger = logger;
        _session = session;
        var options = webSocketServerOptions.Value;
        _baseUrl = $"{options.Scheme}://{options.Domain}:{options.Port}{HubRoutes.Game}";
    }

    public async Task<bool> ConnectAsync(string gameId)
    {
        try
        {
            var url = $"{_baseUrl}?gameId={Uri.EscapeDataString(gameId)}&playerId={Uri.EscapeDataString(_session.PlayerId)}";
            _gameConnection = new HubConnectionBuilder().WithUrl(url).WithAutomaticReconnect().Build();

            _gameConnection.On<RoundCatalogDto>(nameof(IGameHubClient.RoundStarted), catalog =>
            {
                RoundStarted?.Invoke(catalog);
            });

            _gameConnection.On(nameof(IGameHubClient.WaitingForOtherPlayers), () =>
            {
                WaitingForOtherPlayers?.Invoke();
            });

            // US-07 : la fin du tour débloque l'attente de EndTurnAsync
            _gameConnection.On<RoundCatalogDto>(nameof(IGameHubClient.RoundResolved), _ =>
            {
                _roundEnd?.TrySetResult();
            });

            _gameConnection.On<IReadOnlyList<CompanyDto>>(nameof(IGameHubClient.GameOver), _ =>
            {
                _roundEnd?.TrySetResult();
            });

            await _gameConnection.StartAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to the /game hub");
            return false;
        }
    }

    public async Task<string?> SubmitApplicationAsync(ApplyToTenderCommand command)
    {
        if (_gameConnection is null)
            return "You are not connected to the game.";

        try
        {
            await _gameConnection.InvokeAsync(nameof(IGameHubServer.SubmitApplicationAsync), command);
            return null;
        }
        catch (HubException ex)
        {
            // Erreur métier renvoyée par le serveur (ex : prix invalide)
            return ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send the application");
            return "Unable to send the application to the server.";
        }
    }

    public async Task<string?> EnrollConsultantAsync(EnrollTrainingCommand command)
    {
        // Même principe que SubmitApplicationAsync : on renvoie null si tout s'est bien passé,
        // sinon un message d'erreur que GameLoop affichera au joueur.
        if (_gameConnection is null)
            return "You are not connected to the game.";

        try
        {
            await _gameConnection.InvokeAsync(nameof(IGameHubServer.EnrollConsultantAsync), command);
            return null;
        }
        catch (HubException ex)
        {
            // Erreur renvoyée volontairement par le serveur
            return ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send the training enrollment");
            return "Unable to send the training enrollment to the server.";
        }
    }

    public async Task EndTurnAsync(string companyId)
    {
        if (_gameConnection is null) return;

        // Créée AVANT l'appel : pour le dernier joueur, RoundResolved peut arriver
        // avant même que InvokeAsync ne rende la main.
        _roundEnd = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var command = new EndTurnCommand
        {
            GameId = _session.CurrentGame!.Id,
            CompanyId = companyId
        };

        try
        {
            await _gameConnection.InvokeAsync(nameof(IGameHubServer.ReadyForNextRoundAsync), command);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to end the turn");
            return;   // on n'attend pas une résolution qui ne viendra jamais
        }

        await _roundEnd.Task;
    }
}