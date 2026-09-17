using Server.Application.Abstractions;
using Server.Domain;
using Server.Domain.Enums;

namespace Server.Application.Services;

public class TurnService : ITurnService
{
    public void ResolveRound(Round round)
    {
        if (round.IsCompleted)
        {
            return;
        }

        // 1. Résolution des candidatures aux appels d'offres
        ResolveTenderApplications(round);

        // 2. Progression des contrats et des formations de chaque entreprise
        foreach (var company in round.Game.Companies)
        {
            ProgressContracts(company);
            ProgressTrainings(company);
            DeductSalaries(company);
        }

        // 3. Clôture du tour
        round.IsCompleted = true;
    }

    private static void ResolveTenderApplications(Round round)
    {
        // Regroupe les candidatures par appel d'offres
        var applicationsByTender = round.Applications
            .GroupBy(app => app.Tender.Id);

        foreach (var tenderGroup in applicationsByTender)
        {
            var applications = tenderGroup.ToList();

            // Règle d'attribution : meilleure couverture des compétences requises
            // En cas d'égalité, le premier à avoir postulé l'emporte
            var winningApplication = applications
                .OrderByDescending(app => CalculateSkillScore(app.Tender, app.AssignedConsultants))
                .FirstOrDefault();

            foreach (var application in applications)
            {
                if (application == winningApplication)
                {
                    application.Status = ApplicationStatus.Won;

                    // Création du contrat actif rattaché à l'entreprise gagnante
                    var contract = new Contract
                    {
                        Company = application.Company,
                        Tender = application.Tender,
                        AssignedConsultants = [.. application.AssignedConsultants],
                        RemainingRounds = application.Tender.RoundsNumber,
                        Status = ContractStatus.Active
                    };

                    application.Company.Contracts.Add(contract);
                }
                else
                {
                    application.Status = ApplicationStatus.Lost;
                }
            }
        }
    }

    private static int CalculateSkillScore(Tender tender, IEnumerable<Consultant> consultants)
    {
        var assignedSkills = consultants.SelectMany(c => c.Skills).ToList();
        var score = 0;

        foreach (var requiredSkill in tender.Skills)
        {
            var matchingSkill = assignedSkills.FirstOrDefault(s => s.Name == requiredSkill.Name);
            if (matchingSkill is not null)
            {
                score += (int)matchingSkill.Level >= (int)requiredSkill.Level ? 2 : 1;
            }
        }

        return score;
    }

    private static void ProgressContracts(Company company)
    {
        foreach (var contract in company.Contracts.Where(c => c.Status == ContractStatus.Active))
        {
            contract.RemainingRounds--;

            if (contract.RemainingRounds <= 0)
            {
                contract.Status = ContractStatus.Completed;
                company.Deposit(contract.Tender.Budget);
            }
        }
    }

    private static void ProgressTrainings(Company company)
    {
        foreach (var enrollment in company.TrainingEnrollments.Where(e => e.Status == EnrollmentStatus.InProgress))
        {
            enrollment.RemainingRounds--;

            if (enrollment.RemainingRounds <= 0)
            {
                enrollment.Status = EnrollmentStatus.Completed;

                // Montée de niveau ou ajout de la compétence acquise
                var consultant = enrollment.Consultant;
                var existingSkill = consultant.Skills.FirstOrDefault(s => s.Name == enrollment.Training.Skill.Name);

                if (existingSkill is not null)
                {
                    existingSkill.LevelUp();
                }
                else
                {
                    consultant.Skills.Add(enrollment.Training.Skill);
                }
            }
        }
    }

    private static void DeductSalaries(Company company)
    {
        var totalSalaries = company.Staff.Sum(c => c.SalaryRequirement);
        if (totalSalaries > 0)
        {
            company.Withdraw(totalSalaries);
        }
    }
}