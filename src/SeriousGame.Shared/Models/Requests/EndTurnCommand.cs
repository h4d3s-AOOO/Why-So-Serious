namespace Shared.Models.Requests;

// Fin du tour
public record EndTurnCommand
{
    public required string GameId { get; init; }
    public required string CompanyId { get; init; }
}