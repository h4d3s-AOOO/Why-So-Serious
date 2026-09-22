using Shared.Models.Dtos;

namespace Client.Services.Interfaces;

// Contrat placeholder pour la future logique client du hub /game - vide tant que les actions en jeu
// (tours, appels d'offres, formations) ne sont pas implémentées. Reflète GameServices.
public interface IGameServices
{
    event Action<RoundCatalogDto>? RoundStarted;

    Task<bool> ConnectAsync(string gameId);
}
