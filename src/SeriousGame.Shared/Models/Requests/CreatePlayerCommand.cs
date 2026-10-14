namespace Shared.Models.Requests;

public record CreatePlayerCommand
{
    public required string PlayerId { get; init; }
    public required string Nickname { get; init; }
}