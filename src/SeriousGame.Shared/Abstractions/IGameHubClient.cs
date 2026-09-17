using Shared.Models.Dtos;

namespace Shared.Abstractions;

public interface IGameHubClient
{
    Task RoundStarted(RoundCatalogDto catalog);
    Task WaitingForOtherPlayers();
    Task RoundResolved(RoundCatalogDto nextRoundCatalog);
    Task GameOver(IReadOnlyList<CompanyDto> finalRankings);
}