namespace Shared.Models.Requests;

public record CreateGameCommand
{
    public required string PlayerId { get; init; }
    public required string GameName { get; init; }
}