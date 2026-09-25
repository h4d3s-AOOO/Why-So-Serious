using System.Globalization;
using Client.Resources;
using Client.Services.Interfaces;
using Client.State;
using Client.UI;
using Shared.Models.Dtos;
using Shared.Models.Requests;

namespace Client.Game;

/// <summary>
/// Boucle de partie côté client : parcourt les tours et, pour chacun, les phases de TurnPhase.
/// Les phases implémentées (analyse, décision, envoi) utilisent le catalogue du tour reçu du
/// serveur ; les autres affichent encore un placeholder. Toute la saisie console passe ici,
/// sur le thread principal.
/// </summary>
public class GameLoop
{
    private static readonly TurnPhase[] Phases = Enum.GetValues<TurnPhase>();

    private readonly ClientSession _session;
    private readonly IGameServices _gameServices;

    // Choix faits en phase Decision, envoyés en phase Submission
    private TenderDto? _chosenTender;
    private List<string> _chosenConsultantIds = [];
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
                ConsoleUI.DisplayCompanyDashboard(catalog.PlayerCompany, catalog.RoundNumber, catalog.TotalRounds);
                ConsoleUI.DisplayTenders(catalog.AvailableTenders);
                ConsoleUI.DisplayTrainings(catalog.AvailableTrainings);
                return true;

            case TurnPhase.Decision:
                ChooseApplication(catalog);
                return true;

            case TurnPhase.Submission when _chosenTender is not null:
                await SubmitApplicationAsync(catalog.PlayerCompany.Id);
                return true;

            default:
                return false;
        }
    }

    // --- Phase Decision : appel d'offre, consultants affectés, prix ---

    private void ChooseApplication(RoundCatalogDto catalog)
    {
        _chosenTender = null;
        var tenders = catalog.AvailableTenders;
        var staff = catalog.PlayerCompany.Staff;

        if (tenders.Count == 0)
        {
            ConsoleUI.WriteInfo("Aucun appel d'offres ce tour-ci.");
            return;
        }
        if (staff.Count == 0)
        {
            ConsoleUI.WriteError("Aucun consultant dans votre entreprise : candidature impossible ce tour-ci.");
            return;
        }

        ConsoleUI.DisplayTenders(tenders);
        var tenderIndex = AskTenderIndex(tenders.Count);
        if (tenderIndex < 0)
        {
            ConsoleUI.WriteInfo("Vous ne candidatez pas ce tour-ci.");
            return;
        }

        var tender = tenders[tenderIndex];
        _chosenConsultantIds = AskConsultants(staff);
        _chosenBid = AskBid(tender);
        _chosenTender = tender;

        ConsoleUI.WriteInfo($"Votre choix : {tender.Name}, {_chosenConsultantIds.Count} consultant(s), {_chosenBid:N0} €");
    }

    /// <summary>Retourne l'index choisi, ou -1 si le joueur passe son tour (0).</summary>
    private static int AskTenderIndex(int count)
    {
        while (true)
        {
            ConsoleUI.WritePrompt($"Numéro de l'appel d'offres (1-{count}, 0 pour passer) :");
            if (int.TryParse(Console.ReadLine(), out var number) && number >= 0 && number <= count)
                return number - 1;
            ConsoleUI.WriteError("Numéro invalide.");
        }
    }

    private static List<string> AskConsultants(IReadOnlyList<ConsultantDto> staff)
    {
        ConsoleUI.WriteInfo("\nConsultants du staff :");
        for (var i = 0; i < staff.Count; i++)
            Console.WriteLine($"[{i + 1}] {staff[i].FullName}");

        while (true)
        {
            ConsoleUI.WritePrompt("Numéros des consultants à affecter (séparés par des virgules, ex. 1,2) :");
            var ids = (Console.ReadLine() ?? string.Empty)
                .Split(',')
                .Select(entry => int.TryParse(entry.Trim(), out var n) ? n : 0)
                .Where(n => n >= 1 && n <= staff.Count)
                .Distinct()
                .Select(n => staff[n - 1].Id)
                .ToList();

            if (ids.Count > 0) return ids;
            ConsoleUI.WriteError("Choisissez au moins un consultant.");
        }
    }

    private static decimal AskBid(TenderDto tender)
    {
        while (true)
        {
            ConsoleUI.WritePrompt($"Votre prix en € pour « {tender.Name} » :");
            if (TryParseAmount(Console.ReadLine(), out var bid))
                return bid;
            ConsoleUI.WriteError("Montant non reconnu, tapez un nombre (ex : 120000).");
        }
    }

    // --- Phase Submission : envoi au serveur ---

    private async Task SubmitApplicationAsync(string companyId)
    {
        while (true)
        {
            var command = new ApplyToTenderCommand
            {
                GameId = _session.CurrentGame!.Id,
                CompanyId = companyId,
                TenderId = _chosenTender!.Id,
                ConsultantIds = _chosenConsultantIds,
                Bid = _chosenBid
            };

            var error = await _gameServices.SubmitApplicationAsync(command);
            if (error is null)
            {
                ConsoleUI.WriteInfo($"✅ Candidature envoyée : {_chosenTender.Name} pour {_chosenBid:N0} €");
                return;
            }

            // Refus du serveur (ex : prix invalide) : le joueur corrige ou renonce
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