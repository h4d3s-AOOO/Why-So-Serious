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
/// Connexion du client au hub /game : réception du catalogue du tour (RoundStarted)
/// et envoi des actions du joueur (candidature à un appel d'offre).
/// </summary>
public class GameServices : IGameServices
{
    private readonly ILogger<GameServices> _logger;
    private readonly ClientSession _session;
    private readonly string _baseUrl;
    private HubConnection? _gameConnection;

    public event Action<RoundCatalogDto>? RoundStarted;

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

            await _gameConnection.StartAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec de connexion au hub /game");
            return false;
        }
    }

    public async Task<string?> SubmitApplicationAsync(string companyId, string tenderId, decimal bid)
    {
        if (_gameConnection is null)
            return "Vous n'êtes pas connecté à la partie.";

        var command = new ApplyToTenderCommand
        {
            GameId = _session.CurrentGame!.Id,
            CompanyId = companyId,
            TenderId = tenderId,
            ConsultantIds = [],      // le choix des consultants viendra avec US-06
            Bid = bid
        };

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
            _logger.LogWarning(ex, "Échec de l'envoi de la candidature");
            return "Impossible d'envoyer la candidature au serveur.";
        }
    }
}