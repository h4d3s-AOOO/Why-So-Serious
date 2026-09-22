using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Server.Application;
using Server.Application.Abstractions;
using Server.Application.Services;
using Server.Domain;
using Server.Domain.Enums;
using Shared.Abstractions;
using Shared.Models.Requests;

namespace Server.Hubs;

public class GameHub : Hub<IGameHubClient>, IGameHubServer
{
    private readonly GameService _gameService;
    private readonly PlayerService _playerService;
    private readonly ITurnService _turnService;
    private readonly ILogger<GameHub> _logger;

    public GameHub(GameService gameService, PlayerService playerService, ITurnService turnService)
    {
        _gameService = gameService;
        _playerService = playerService;
        _turnService = turnService;
    }

    public async Task SubmitApplicationAsync(ApplyToTenderCommand command)
    {
        var game = _gameService.GetGame(command.GameId);
        if (game is null) return;

        var currentRound = game.Rounds.LastOrDefault();
        if (currentRound is null || currentRound.IsCompleted) return;

        var company = game.Companies.FirstOrDefault(c => c.Id == command.CompanyId);
        var tender = currentRound.Tenders.FirstOrDefault(t => t.Id == command.TenderId);
        if (company is null || tender is null) return;

        var assignedConsultants = company.Staff
            .Where(c => command.ConsultantIds.Contains(c.Id))
            .ToList();

        var application = new TenderApplication
        {
            Round = currentRound,
            Company = company,
            Tender = tender,
            AssignedConsultants = assignedConsultants,
            Status = ApplicationStatus.Pending
        };

        currentRound.Applications.Add(application);
        await Clients.Caller.WaitingForOtherPlayers();
    }

    public async Task EnrollConsultantAsync(EnrollTrainingCommand command)
    {
        var game = _gameService.GetGame(command.GameId);
        if (game is null) return;

        var currentRound = game.Rounds.LastOrDefault();
        if (currentRound is null || currentRound.IsCompleted) return;

        var company = game.Companies.FirstOrDefault(c => c.Id == command.CompanyId);
        var training = currentRound.Trainings.FirstOrDefault(t => t.Id == command.TrainingId);
        if (company is null || training is null) return;

        var consultant = company.Staff.FirstOrDefault(c => c.Id == command.ConsultantId);
        if (consultant is null) return;

        company.Withdraw(training.Cost);

        var enrollment = new TrainingEnrollment
        {
            Company = company,
            Consultant = consultant,
            Training = training,
            RemainingRounds = training.RoundsNumber,
            Status = EnrollmentStatus.InProgress
        };

        company.TrainingEnrollments.Add(enrollment);
    }

    public async Task ReadyForNextRoundAsync(EndTurnCommand command)
    {
        var game = _gameService.GetGame(command.GameId);
        if (game is null) return;

        var currentRound = game.Rounds.LastOrDefault();
        if (currentRound is null || currentRound.IsCompleted) return;

        // Arbitrage du tour en cours
        _turnService.ResolveRound(currentRound);

        // Clôture définitive si le nombre max de rounds est atteint
        if (currentRound.Order >= game.RoundsNumber)
        {
            game.IsInProgress = false;
            var rankings = game.Companies
                .OrderByDescending(c => c.Treasury)
                .Select(Mapper.ToDto)
                .ToList();

            await Clients.Group(game.Id).GameOver(rankings);
            return;
        }

        // Notification du résultat à chaque entreprise du groupe
        foreach (var company in game.Companies)
        {
            var catalogDto = Mapper.ToDto(currentRound, company, game.RoundsNumber);
            await Clients.Group(game.Id).RoundResolved(catalogDto);
        }
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var gameId = httpContext?.Request.Query["gameId"].ToString();
        var playerId = httpContext?.Request.Query["playerId"].ToString();

        if (!string.IsNullOrEmpty(gameId) && !string.IsNullOrEmpty(playerId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

            var game = _gameService.GetGame(gameId);
            var company = game?.Companies.FirstOrDefault(c => c.PlayerOwner.Id == playerId);
            var round = game?.Rounds.LastOrDefault();

            if (game is not null && company is not null && round is not null)
            {
                var catalog = Mapper.ToDto(round, company, game.RoundsNumber);
                await Clients.Caller.RoundStarted(catalog);
            }
        }

        await base.OnConnectedAsync();
    }
}