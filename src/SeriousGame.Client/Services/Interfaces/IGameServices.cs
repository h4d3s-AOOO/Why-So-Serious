using Shared.Models.Dtos;
using Shared.Models.Requests;

namespace Client.Services.Interfaces;

// Contrat pour la logique client du hub /game. Reflète GameServices.
public interface IGameServices
{
    event Action<RoundCatalogDto>? RoundStarted;
    event Action? WaitingForOtherPlayers;

    Task<bool> ConnectAsync(string gameId);
    Task SubmitApplicationAsync(ApplyToTenderCommand command);
}