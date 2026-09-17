namespace Shared.Models.Dtos;

// Appel d'offre 

public class TenderDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Budget { get; init; }
    public int RoundsDuration { get; init; }
    public required IReadOnlyList<SkillDto> RequiredSkills { get; init; }
}