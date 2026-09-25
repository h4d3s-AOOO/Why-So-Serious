using Shared.Models.Dtos;

namespace Client.Services.Interfaces;

// Contrat placeholder pour la future logique client du hub /game - vide tant que les actions en jeu
// (tours, appels d'offres, formations) ne sont pas implémentées. Reflète GameServices.
public interface IGameServices
{
    event Action<RoundCatalogDto>? RoundStarted;
    Task<bool> ConnectAsync(string gameId);

    /// <summary>
    /// Envoie la candidature du joueur à un appel d'offre, avec son prix.
    /// Retourne null si le serveur l'a acceptée, sinon le message d'erreur à afficher.
    /// </summary>
    Task<string?> SubmitApplicationAsync(string companyId, string tenderId, decimal bid);
}