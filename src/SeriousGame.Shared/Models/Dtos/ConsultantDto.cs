namespace Shared.Models.Dtos;

public class ConsultantDto
{
    public required string Id { get; init; }
    public required string FullName { get; init; }
    public decimal SalaryRequirement { get; init; }
    public ICollection<SkillDto> Skills { get; init; } = [];
    public bool IsAvailable { get; init; }
}