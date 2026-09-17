namespace Shared.Models.Requests;

// Postuler à un appel d'offre
public record ApplyToTenderCommand
{
    public required string GameId { get; init; }
    public required string CompanyId { get; init; }
    public required string TenderId { get; init; }
    public required IReadOnlyList<string> ConsultantIds { get; init; }
    public decimal? Bid { get; init; }
}