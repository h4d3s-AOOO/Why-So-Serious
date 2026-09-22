using Client.Game;
using Client.Resources;
using Client.Services;
using Client.Services.Interfaces;
using Client.State;
using Client.UI;
using Microsoft.Extensions.Logging;

namespace Client;

/// <summary>
/// Pilote la navigation et possède toute l'E/S console : prompts, rendu des écrans,
/// détection clavier. Le transport hub et l'état vivent dans ILobbyServices/ClientSession.
/// </summary>
public class App
{
    // Un emoji occupe 2 colonnes dans le terminal : chaque frame garde le même nombre
    // d'espaces pour que le redessin par \r de ConsoleAnimator reste aligné.
    private static string[] Bounce => ["🚀     ", " 🚀    ", "  🚀   ", "   🚀  ", "    🚀 ", "     🚀", "    🚀 ", "   🚀  ", "  🚀   ", " 🚀    "];

    private readonly ILogger<App> _logger;
    private readonly ILobbyServices _lobbyServices;
    private readonly ClientSession _session;
    private readonly ConsoleAnimator _waitingAnim;

    // Garde d'affichage : les broadcasts WaitingForPlayers ne doivent rafraîchir l'écran
    // que pendant la phase d'attente, pas écraser le menu pendant la navigation.
    private bool _isWaitingForGameStart;
    private readonly IGameServices _gameServices;

    public App(ILogger<App> logger, ILobbyServices lobbyServices, IGameServices gameServices, ClientSession session)
    {
        _logger = logger;
        _lobbyServices = lobbyServices;
        _gameServices = gameServices;
        _session = session;
        _waitingAnim = new ConsoleAnimator(ClientResources.WaitingAnimationLabel, Bounce, 200);

        SubscribeToLobbyEvents();
    }

    private void SubscribeToLobbyEvents()
    {
        _lobbyServices.WaitingForPlayers += (game, playerNames) =>
        {
            if (!_isWaitingForGameStart) return;
            RenderWaitingRoom(game.Name, playerNames, game.MinimumPlayers);
        };

        _lobbyServices.GameStarting += game =>
        {
            _waitingAnim.Stop();
            ConsoleUI.WriteInfo(string.Format(ClientResources.GameStartingMessage, game.Name));
        };

        _gameServices.RoundStarted += catalog =>
        {
            ConsoleUI.DisplayTenders(catalog.AvailableTenders);
        };

        _lobbyServices.NotificationReceived += msg =>
        {
            ConsoleUI.WriteInfo(string.Format(ClientResources.ServerMessagePrefix, msg));
        };

        _lobbyServices.ConnectionLost += () =>
        {
            ConsoleUI.WriteError(ClientResources.ReconnectingWarning);
        };

        _lobbyServices.ConnectionRestored += () =>
        {
            ConsoleUI.WriteInfo(ClientResources.ReconnectedMessage);
        };
    }

    public async Task RunAsync()
    {
        var isSuccessfullyConnected = await _lobbyServices.ConnectAsync();

        if (!isSuccessfullyConnected)
        {
            ConsoleUI.WriteError(string.Format(ClientResources.FailedToConnectError, _lobbyServices.HubUrl));
            return;
        }

        ConsoleUI.WriteInfo(string.Format(ClientResources.ConnectedToHubMessage, _lobbyServices.HubUrl));

        ConsoleUI.WritePrompt(ClientResources.ChooseLoginPrompt);
        var login = ConsoleUI.ReadPrompt() ?? $"Player_{Guid.NewGuid().ToString()[..6]}";

        // Identification côté serveur
        await _lobbyServices.IdentifyClientAsync(login);

        _logger.LogDebug("Your client ID: {clientId}", _session.PlayerId);
        _logger.LogDebug("✅ Connected to the server!.");

        await MainMenuLoop();
    }

    private async Task MainMenuLoop()
    {
        // Boucle d'enrôlement : chaque issue (partie jouée, échec, retour) ramène au menu,
        // seule l'option Quitter sort de la boucle.
        while (true)
        {
            ConsoleUI.WriteHeader(ClientResources.MainMenuHeader);
            ConsoleUI.WritePrompt(ClientResources.MenuOptionCreateGame);
            ConsoleUI.WritePrompt(ClientResources.MenuOptionJoinGame);
            ConsoleUI.WritePrompt(ClientResources.MenuOptionQuit);

            ConsoleUI.WritePrompt(ClientResources.YourChoicePrompt);
            var input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    await CreateGameFlowAsync();
                    break;
                case "2":
                    await JoinGameFlowAsync();
                    break;
                case "3":
                    await _lobbyServices.DisconnectAsync();
                    ConsoleUI.WriteInfo(ClientResources.DisconnectedFromLobbyMessage);
                    return;
                default:
                    ConsoleUI.WriteError(ClientResources.InvalidChoiceError);
                    break;
            }
        }
    }

    private async Task CreateGameFlowAsync()
    {
        ConsoleUI.WritePrompt(ClientResources.ChooseGameNamePrompt);
        var gameName = ConsoleUI.ReadPrompt() ?? $"Game_{Guid.NewGuid().ToString()[..6]}";

        var result = await _lobbyServices.CreateGameAsync(gameName);
        ConsoleUI.WriteInfo(string.Format(ClientResources.GameCreatedMessage, gameName));

        await HandleEnrollmentAsync(result);
    }

    private async Task JoinGameFlowAsync()
    {
        // Snapshot local : la session peut recevoir une liste rafraîchie pendant la saisie.
        var games = _session.Games;

        if (games.Count == 0)
        {
            ConsoleUI.WriteInfo(ClientResources.NoGamesAvailableMessage);
            return;
        }

        ConsoleUI.WriteInfo(ClientResources.GamesAvailableHeader);
        for (int i = 0; i < games.Count; i++)
            ConsoleUI.WritePrompt(string.Format(ClientResources.GameListItemFormat, i + 1, games[i].Name, games[i].Players.Count, games[i].MinimumPlayers));

        var returnToMenuChoice = games.Count + 1;
        ConsoleUI.WritePrompt(string.Format(ClientResources.ReturnToMainMenuFormat, returnToMenuChoice));

        int choice;
        while (true)
        {
            ConsoleUI.WritePrompt(ClientResources.EnterGameNumberPrompt);
            if (int.TryParse(Console.ReadLine(), out choice) && choice > 0 && choice <= returnToMenuChoice)
                break;
            ConsoleUI.WriteError(ClientResources.InvalidChoiceMessage);
        }

        if (choice == returnToMenuChoice)
            return;

        var selectedGame = games[choice - 1];
        var result = await _lobbyServices.JoinGameAsync(selectedGame.Id);

        if (result == EnrollmentResult.Failed)
        {
            ConsoleUI.WriteError(string.Format(ClientResources.CannotJoinGameError, selectedGame.Name));
            return;
        }

        ConsoleUI.WriteInfo(string.Format(ClientResources.JoinedGameMessage, selectedGame.Name));

        await HandleEnrollmentAsync(result);
    }

    private async Task HandleEnrollmentAsync(EnrollmentResult result)
    {
        switch (result)
        {
            case EnrollmentResult.WaitingForPlayers:
                _isWaitingForGameStart = true;
                RenderCurrentWaitingRoom();

                var started = await WaitForGameStartWithEscapeAsync();

                _isWaitingForGameStart = false;

                if (!started)
                {
                    // Le joueur a annulé l'attente (Échap) : quitter proprement la partie côté serveur.
                    _waitingAnim.Stop();
                    var gameName = _session.CurrentGame?.Name;
                    await _lobbyServices.LeaveGameAsync();
                    ConsoleUI.WriteInfo(string.Format(ClientResources.LeftGameMessageFormat, gameName));
                }
                else
                {
                    await _gameServices.ConnectAsync(_session.CurrentGame!.Id);
                    await new GameLoop(_session).RunAsync();
                }
                break;
            case EnrollmentResult.GameStarting:
                await _gameServices.ConnectAsync(_session.CurrentGame!.Id);
                await new GameLoop(_session).RunAsync();
                break;
        }
    }

    /// <summary>
    /// Rend l'écran d'attente depuis l'état de session - utilisé en entrant dans la phase
    /// d'attente, car le broadcast WaitingForPlayers du join peut être arrivé avant la garde.
    /// </summary>
    private void RenderCurrentWaitingRoom()
    {
        var game = _session.CurrentGame;
        if (game is null) return;

        RenderWaitingRoom(game.Name, game.Players.Select(p => p.Nickname).ToList(), game.MinimumPlayers);
    }

    private void RenderWaitingRoom(string gameName, IReadOnlyList<string> playerNames, int minimumPlayers)
    {
        ConsoleUI.WriteHeader(string.Format(ClientResources.WaitingForGameHeader, gameName));
        ConsoleUI.WritePrompt(string.Format(ClientResources.PlayersWaitingPrompt, playerNames.Count, minimumPlayers, string.Join(", ", playerNames)));
        _waitingAnim.Start();
    }

    private async Task<bool> WaitForGameStartWithEscapeAsync()
    {
        // KeyAvailable lève une exception quand stdin est redirigé (exécution scriptée) :
        // dans ce cas la détection clavier est désactivée et on attend uniquement le signal.
        var canReadKeys = !Console.IsInputRedirected;
        if (canReadKeys) ConsoleUI.WritePrompt(ClientResources.CancelWaitingHint);

        using var cancellation = new CancellationTokenSource();
        var waitTask = _lobbyServices.WaitForGameStartAsync(cancellation.Token);

        while (!waitTask.IsCompleted)
        {
            if (canReadKeys && Console.KeyAvailable)
            {
                // intercept: true - la touche n'est pas affichée dans la console.
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Escape)
                    cancellation.Cancel();
                // Toute autre touche est consommée et ignorée (n'encombre pas la saisie du menu).
            }

            // Sondage léger : 10 vérifications/s, le thread dort entre deux.
            await Task.Delay(100);
        }

        return await waitTask;
    }


}
