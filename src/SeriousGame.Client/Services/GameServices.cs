// Transport hub du hub /game : connexion et envoi des candidatures à un appel d'offres. Reflète
// GameHub côté serveur (SubmitApplicationAsync y est déjà implémenté).
using Client.Options;
using Client.Services.Interfaces;
using Client.State;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared;
using Shared.Abstractions;
using Shared.Models.Dtos;
using Shared.Models.Requests;

namespace Client.Services;

public class GameServices : IGameServices
{
    private readonly ILogger<GameServices> _logger;
    private readonly ClientSession _session;
    private readonly string _baseUrl;
    private HubConnection? _gameConnection;

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

            await _gameConnection.StartAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec de connexion au hub /game");
            return false;
        }
    }

    public async Task SubmitApplicationAsync(ApplyToTenderCommand command)
    {
        if (_gameConnection is null) return;
        await _gameConnection.InvokeAsync(nameof(IGameHubServer.SubmitApplicationAsync), command);
    }
}