using Microsoft.AspNetCore.SignalR;
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

    public GameHub(GameService gameService, PlayerService playerService, ITurnService turnService)
    {
        _gameService = gameService;
        _playerService = playerService;
        _turnService = turnService;
    }

    public async Task SubmitApplicationAsync(ApplyToTenderCommand command)
    {
        if (command.Bid is null or <= 0m)
            throw new HubException("Bid must be a positive amount.");

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
            Status = ApplicationStatus.Pending,
            Bid = command.Bid
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

        // US-07 : on refuse une entreprise qui n'appartient pas à cette partie
        var company = game.Companies.FirstOrDefault(c => c.Id == command.CompanyId);
        if (company is null) return;

        // US-07 : on note que ce joueur a terminé son tour ; on ne résout
        // le tour que lorsque TOUTES les entreprises ont validé.
        bool roundResolvedNow;
        lock (currentRound)
        {
            currentRound.MarkCompanyReady(company.Id);
            roundResolvedNow = currentRound.AllCompaniesReady && !currentRound.IsCompleted;

            if (roundResolvedNow)
                _turnService.ResolveRound(currentRound);   // passe IsCompleted à true
        }

        if (!roundResolvedNow)
        {
            await Clients.Caller.WaitingForOtherPlayers();
            return;
        }

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

        // US-07 : chaque joueur reçoit SON résultat, et pas celui des autres
        foreach (var gameCompany in game.Companies)
        {
            var catalogDto = Mapper.ToDto(currentRound, gameCompany, game.RoundsNumber);
            await Clients.Group(gameCompany.PlayerOwner.Id).RoundResolved(catalogDto);
        }
    }
}