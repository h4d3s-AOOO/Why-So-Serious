namespace Shared.Models.Dtos;

public class TrainingDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public decimal Cost { get; init; }
    public int RoundsDuration { get; init; }
    public required SkillDto ProducedSkill { get; init; }
}