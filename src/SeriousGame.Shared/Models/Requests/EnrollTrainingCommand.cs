namespace Shared.Models.Requests;

// Inscrire un consultant à une formation
public record EnrollTrainingCommand
{
    public required string GameId { get; init; }
    public required string CompanyId { get; init; }
    public required string TrainingId { get; init; }
    public required string ConsultantId { get; init; }
}