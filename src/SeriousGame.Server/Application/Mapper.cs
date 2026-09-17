using Server.Domain;
using Server.Domain.Enums;
using Shared.Models.Dtos;

namespace Server.Application;

public static class Mapper
{
    public static PlayerDto ToDto(Player player) => new()
    {
        Id = player.Id,
        Nickname = player.Nickname,
        IsActive = player.IsActive
    };

    public static GameDto ToDto(Game game) => new()
    {
        Id = game.Id,
        Name = game.Name,
        MinimumPlayers = game.MinimumPlayers,
        MaximumPlayers = game.MaximumPlayers,
        RoundsNumber = game.RoundsNumber,
        IsInProgress = game.IsInProgress,
        Owner = ToDto(game.Owner),
        Players = game.Players.Select(ToDto).ToList()
    };

    public static SkillDto ToDto(Skill skill) => new()
    {
        Id = skill.Id.ToString(),
        Name = skill.Name,
        Level = skill.Level.ToString()
    };

    public static ConsultantDto ToDto(Consultant consultant, Company company)
    {
        // Un consultant est occupé s'il est affecté à un contrat actif ou en formation en cours
        var isAssignedToActiveContract = company.Contracts.Any(c => 
            c.Status == ContractStatus.Active && 
            c.AssignedConsultants.Any(assigned => assigned.Id == consultant.Id));

        var isEnrolledInActiveTraining = company.TrainingEnrollments.Any(e => 
            e.Status == EnrollmentStatus.InProgress && 
            e.Consultant.Id == consultant.Id);

        var isAvailable = isAssignedToActiveContract || isEnrolledInActiveTraining;

        return new()
        {
            Id = consultant.Id,
            FullName = $"{consultant.Firstname} {consultant.Lastname}",
            SalaryRequirement = consultant.SalaryRequirement,
            Skills = consultant.Skills.Select(ToDto).ToList(),
            IsAvailable = isAssignedToActiveContract || isEnrolledInActiveTraining
        };
    }

    public static TenderDto ToDto(Tender tender) => new()
    {
        Id = tender.Id,
        Name = tender.Name,
        Budget = tender.Budget,
        RoundsDuration = tender.RoundsNumber,
        RequiredSkills = tender.Skills.Select(ToDto).ToList()
    };


    public static TrainingDto ToDto(Training training) => new()
    {
        Id = training.Id,
        Name = training.Name,
        Cost = training.Cost,
        RoundsDuration = training.RoundsNumber,
        ProducedSkill = ToDto(training.Skill)
    };

    public static ContractDto ToDto(Contract contract) => new()
    {
        Id = contract.Id,
        TenderName = contract.Tender.Name,
        Budget = contract.Tender.Budget,
        RemainingRounds = contract.RemainingRounds,
        Status = contract.Status.ToString(),
        AssignedConsultantNames = contract.AssignedConsultants.Select(c => $"{c.Firstname} {c.Lastname}").ToList()
    };

    public static CompanyDto ToDto(Company company) => new()
    {
        Id = company.Id,
        Name = company.Name,
        Treasury = company.Treasury,
        Staff = company.Staff.Select(c => ToDto(c, company)).ToList(),
        ActiveContracts = company.Contracts.Where(c => c.Status == ContractStatus.Active).Select(ToDto).ToList()
    };

    public static RoundCatalogDto ToDto(Round round, Company company, int totalRounds) => new()
    {
        RoundNumber = round.Order,
        TotalRounds = totalRounds,
        AvailableTenders = round.Tenders.Select(ToDto).ToList(),
        AvailableTrainings = round.Trainings.Select(ToDto).ToList(),
        PlayerCompany = ToDto(company)
    };
}