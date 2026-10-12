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

    // Choix de formation faits en phase Decision, envoyés en phase Submission
    private TrainingDto? _chosenTraining;
    private string? _chosenTrainingConsultantId;

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
                // La formation se choisit après l'appel d'offres : on sait ainsi quels
                // consultants sont déjà pris par la candidature de ce tour.
                ChooseTraining(catalog);
                return true;

            // On envoie ce que le joueur a choisi : une candidature, une inscription, ou les deux.
            // S'il n'a rien choisi, la phase reste un placeholder (return false).
            case TurnPhase.Submission when _chosenTender is not null || _chosenTraining is not null:
                if (_chosenTender is not null)
                    await SubmitApplicationAsync(catalog.PlayerCompany.Id);

                if (_chosenTraining is not null)
                    await SubmitTrainingAsync(catalog.PlayerCompany.Id);

                return true;

            default:
                return false;
        }
    }

    // --- Phase Decision : appel d'offre, consultants affectés, prix ---

    private void ChooseApplication(RoundCatalogDto catalog)
    {
        _chosenTender = null;
        // Aussi remis à zéro : sinon, si le joueur ne candidate pas ce tour-ci, les consultants
        // du tour précédent resteraient exclus du choix de formation.
        _chosenConsultantIds = [];
        var tenders = catalog.AvailableTenders;
        var staff = catalog.PlayerCompany.Staff;

        if (tenders.Count == 0)
        {
            ConsoleUI.WriteInfo("No tender this round.");
            return;
        }
        if (staff.Count == 0)
        {
            ConsoleUI.WriteError("No consultant in your company: you cannot apply this round.");
            return;
        }

        ConsoleUI.DisplayTenders(tenders);
        var tenderIndex = AskTenderIndex(tenders.Count);
        if (tenderIndex < 0)
        {
            ConsoleUI.WriteInfo("You are not applying this round.");
            return;
        }

        var tender = tenders[tenderIndex];
        _chosenConsultantIds = AskConsultants(staff);
        _chosenBid = AskBid(tender);
        _chosenTender = tender;

        ConsoleUI.WriteInfo($"Your choice: {tender.Name}, {_chosenConsultantIds.Count} consultant(s), {_chosenBid:N0} €");
    }

    /// <summary>Retourne l'index choisi, ou -1 si le joueur passe son tour (0).</summary>
    private static int AskTenderIndex(int count)
    {
        while (true)
        {
            ConsoleUI.WritePrompt($"Tender number (1-{count}, 0 to skip):");
            if (int.TryParse(Console.ReadLine(), out var number) && number >= 0 && number <= count)
                return number - 1;
            ConsoleUI.WriteError("Invalid number.");
        }
    }

    private static List<string> AskConsultants(IReadOnlyList<ConsultantDto> staff)
    {
        ConsoleUI.WriteInfo("\nStaff consultants:");
        for (var i = 0; i < staff.Count; i++)
            Console.WriteLine($"[{i + 1}] {staff[i].FullName}");

        while (true)
        {
            ConsoleUI.WritePrompt("Numbers of the consultants to assign (comma-separated, e.g. 1,2):");
            var ids = (Console.ReadLine() ?? string.Empty)
                .Split(',')
                .Select(entry => int.TryParse(entry.Trim(), out var n) ? n : 0)
                .Where(n => n >= 1 && n <= staff.Count)
                .Distinct()
                .Select(n => staff[n - 1].Id)
                .ToList();

            if (ids.Count > 0) return ids;
            ConsoleUI.WriteError("Choose at least one consultant.");
        }
    }

    private static decimal AskBid(TenderDto tender)
    {
        while (true)
        {
            ConsoleUI.WritePrompt($"Your price in € for \"{tender.Name}\":");
            if (TryParseAmount(Console.ReadLine(), out var bid))
                return bid;
            ConsoleUI.WriteError("Amount not recognized, type a number (e.g. 120000).");
        }
    }

    // --- Phase Decision (suite) : formation et consultant à former ---

    private void ChooseTraining(RoundCatalogDto catalog)
    {
        // On repart de zéro à chaque tour : le choix du tour précédent ne doit pas être renvoyé
        _chosenTraining = null;
        _chosenTrainingConsultantId = null;

        var trainings = catalog.AvailableTrainings;

        if (trainings.Count == 0)
        {
            ConsoleUI.WriteInfo("No training available this round.");
            return;
        }

        // Un consultant déjà affecté à l'appel d'offres de ce tour ne peut pas partir en formation.
        // On garde donc uniquement ceux qui ne sont pas dans la liste des consultants choisis.
        var freeConsultants = catalog.PlayerCompany.Staff
            .Where(consultant => !_chosenConsultantIds.Contains(consultant.Id))
            .ToList();

        if (freeConsultants.Count == 0)
        {
            ConsoleUI.WriteInfo("No consultant left to send on a training this round.");
            return;
        }

        ConsoleUI.DisplayTrainings(trainings);
        var trainingIndex = AskTrainingIndex(trainings.Count);
        if (trainingIndex < 0)
        {
            ConsoleUI.WriteInfo("You do not enroll anyone in a training this round.");
            return;
        }

        var training = trainings[trainingIndex];

        // La formation se paie tout de suite : on vérifie que la trésorerie suffit
        if (catalog.PlayerCompany.Treasury < training.Cost)
        {
            ConsoleUI.WriteError($"Not enough treasury for \"{training.Name}\" (cost: {training.Cost:N0} €).");
            return;
        }

        var consultantIndex = AskConsultantForTraining(freeConsultants);
        if (consultantIndex < 0)
        {
            ConsoleUI.WriteInfo("You do not enroll anyone in a training this round.");
            return;
        }

        _chosenTraining = training;
        _chosenTrainingConsultantId = freeConsultants[consultantIndex].Id;

        ConsoleUI.WriteInfo($"Your choice: {freeConsultants[consultantIndex].FullName} follows \"{training.Name}\" ({training.Cost:N0} €)");
    }

    /// <summary>Retourne l'index de la formation choisie, ou -1 si le joueur passe (0).</summary>
    private static int AskTrainingIndex(int count)
    {
        while (true)
        {
            ConsoleUI.WritePrompt($"Training number (1-{count}, 0 to skip):");
            if (int.TryParse(Console.ReadLine(), out var number) && number >= 0 && number <= count)
                return number - 1;
            ConsoleUI.WriteError("Invalid number.");
        }
    }

    /// <summary>Retourne l'index du consultant choisi dans la liste donnée, ou -1 si le joueur renonce (0).</summary>
    private static int AskConsultantForTraining(IReadOnlyList<ConsultantDto> consultants)
    {
        ConsoleUI.WriteInfo("\nConsultants available for a training:");
        for (var i = 0; i < consultants.Count; i++)
            Console.WriteLine($"[{i + 1}] {consultants[i].FullName}");

        while (true)
        {
            ConsoleUI.WritePrompt($"Consultant to enroll (1-{consultants.Count}, 0 to cancel):");
            if (int.TryParse(Console.ReadLine(), out var number) && number >= 0 && number <= consultants.Count)
                return number - 1;
            ConsoleUI.WriteError("Invalid number.");
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
                ConsoleUI.WriteInfo($"✅ Application sent: {_chosenTender.Name} for {_chosenBid:N0} €");
                return;
            }

            // Refus du serveur (ex : prix invalide) : le joueur corrige ou renonce
            ConsoleUI.WriteError(error);
            ConsoleUI.WritePrompt("New price, or Enter to not apply this round:");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                ConsoleUI.WriteInfo("No application this round.");
                return;
            }
            if (TryParseAmount(input, out var newBid))
                _chosenBid = newBid;
        }
    }

    private async Task SubmitTrainingAsync(string companyId)
    {
        var command = new EnrollTrainingCommand
        {
            GameId = _session.CurrentGame!.Id,
            CompanyId = companyId,
            TrainingId = _chosenTraining!.Id,
            ConsultantId = _chosenTrainingConsultantId!
        };

        // null = le serveur a accepté ; sinon on affiche le message d'erreur
        var error = await _gameServices.EnrollConsultantAsync(command);
        if (error is null)
            ConsoleUI.WriteInfo($"✅ Training enrollment sent: {_chosenTraining.Name} ({_chosenTraining.Cost:N0} €)");
        else
            ConsoleUI.WriteError(error);
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