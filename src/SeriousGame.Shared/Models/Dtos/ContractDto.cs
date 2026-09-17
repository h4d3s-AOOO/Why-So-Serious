namespace Shared.Models.Dtos;

public class ContractDto {
    public required string Id { get; init; }
    public required string TenderName { get; init; }
    public decimal Budget { get; init; }
    public int RemainingRounds { get; init; }
    public required string Status { get; init; }
    public IReadOnlyList<string> AssignedConsultantNames { get; init; } = [];
};

