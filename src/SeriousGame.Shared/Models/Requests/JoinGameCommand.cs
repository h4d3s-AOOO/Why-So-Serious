namespace Shared.Models.Requests;

public record JoinGameCommand
{
    public required string PlayerId { get; init; }
    public required string GameId { get; init; }
}