using System.Globalization;
using Client.Resources;
using Client.Services.Interfaces;
using Client.State;
using Client.UI;
using Shared.Models.Dtos;

namespace Client.Game;

/// <summary>
/// Boucle de partie côté client : parcourt les tours et, pour chacun, les phases de TurnPhase.
/// Les phases implémentées (analyse, décision, envoi) utilisent le catalogue du tour reçu du
/// serveur ; les autres affichent encore un placeholder.
/// </summary>
public class GameLoop
{
    private static readonly TurnPhase[] Phases = Enum.GetValues<TurnPhase>();

    private readonly ClientSession _session;
    private readonly IGameServices _gameServices;

    // Choix fait en phase Decision, envoyé en phase Submission
    private TenderDto? _chosenTender;
    private decimal _chosenBid;

    public GameLoop(ClientSession session, IGameServices gameServices)
    {
        _session = session;
        _gameServices = gameServices;
    }

    public async Task RunAsync()
    {
        var roundsNumber = _session.CurrentGame!.RoundsNumber;

        for (var round = 1; round <= roundsNumber; round++)
        {
            ConsoleUI.WriteHeader(string.Format(ClientResources.RoundHeaderFormat, round, roundsNumber));

            foreach (var phase in Phases)
                await RunPhaseAsync(phase, round);
        }

        ConsoleUI.WriteHeader(ClientResources.GameOverHeader);
        ConsoleUI.WriteInfo(ClientResources.GameOverMessage);
    }

    private async Task RunPhaseAsync(TurnPhase phase, int round)
    {
        // On ne joue une phase que si le serveur nous a envoyé le catalogue de CE tour-ci
        var handled = _session.CurrentRound is { } catalog
                      && catalog.RoundNumber == round
                      && await TryRunGamePhaseAsync(phase, catalog);

        if (!handled)
            ConsoleUI.WriteInfo(PlaceholderFor(phase));

        ConsoleUI.WritePrompt(ClientResources.PressEnterToContinuePrompt);
        Console.ReadLine();
    }

    /// <summary>Exécute une phase déjà implémentée ; false si elle reste un placeholder.</summary>
    private async Task<bool> TryRunGamePhaseAsync(TurnPhase phase, RoundCatalogDto catalog)
    {
        switch (phase)
        {
            case TurnPhase.MarketAnalysis:
                ConsoleUI.DisplayTenders(catalog.AvailableTenders);
                return true;

            case TurnPhase.Decision:
                ChooseTenderAndBid(catalog.AvailableTenders);
                return true;

            case TurnPhase.Submission when _chosenTender is not null:
                await SubmitApplicationAsync(catalog.PlayerCompany.Id);
                return true;

            default:
                return false;
        }
    }

    // --- Phase Decision : choisir un appel d'offre et un prix ---

    private void ChooseTenderAndBid(IReadOnlyList<TenderDto> tenders)
    {
        ConsoleUI.DisplayTenders(tenders);
        _chosenTender = tenders[AskTenderIndex(tenders.Count)];
        _chosenBid = AskBid(_chosenTender);
        ConsoleUI.WriteInfo($"Votre choix : {_chosenTender.Name} pour {_chosenBid:N0} €");
    }

    private static int AskTenderIndex(int count)
    {
        while (true)
        {
            ConsoleUI.WritePrompt($"Numéro de l'appel d'offre (1-{count}) :");
            if (int.TryParse(Console.ReadLine(), out var number) && number >= 1 && number <= count)
                return number - 1;
            ConsoleUI.WriteError("Numéro invalide.");
        }
    }

    private static decimal AskBid(TenderDto tender)
    {
        while (true)
        {
            ConsoleUI.WritePrompt($"Votre prix en € (budget annoncé : {tender.Budget:N0} €) :");
            if (TryParseAmount(Console.ReadLine(), out var bid))
                return bid;
            ConsoleUI.WriteError("Montant non reconnu, tapez un nombre (ex : 120000).");
        }
    }

    // --- Phase Submission : envoyer la candidature au serveur ---

    private async Task SubmitApplicationAsync(string companyId)
    {
        while (true)
        {
            var error = await _gameServices.SubmitApplicationAsync(companyId, _chosenTender!.Id, _chosenBid);
            if (error is null)
            {
                ConsoleUI.WriteInfo($"✅ Candidature envoyée : {_chosenTender.Name} pour {_chosenBid:N0} €");
                return;
            }

            // Refus du serveur (ex : prix invalide) : on laisse le joueur corriger ou renoncer
            ConsoleUI.WriteError(error);
            ConsoleUI.WritePrompt("Nouveau prix, ou Entrée pour ne pas candidater ce tour-ci :");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                ConsoleUI.WriteInfo("Pas de candidature ce tour-ci.");
                return;
            }
            if (TryParseAmount(input, out var newBid))
                _chosenBid = newBid;
        }
    }

    private static bool TryParseAmount(string? input, out decimal amount) =>
        // On retire les espaces pour accepter « 150 000 » ; la virgule décimale suit la langue du PC
        decimal.TryParse(input?.Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out amount);

    private static string PlaceholderFor(TurnPhase phase) => phase switch
    {
        TurnPhase.MarketAnalysis => ClientResources.MarketAnalysisPlaceholder,
        TurnPhase.Simulation => ClientResources.SimulationPlaceholder,
        TurnPhase.Decision => ClientResources.DecisionPlaceholder,
        TurnPhase.Submission => ClientResources.SubmissionPlaceholder,
        TurnPhase.Resolution => ClientResources.ResolutionPlaceholder,
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };
}