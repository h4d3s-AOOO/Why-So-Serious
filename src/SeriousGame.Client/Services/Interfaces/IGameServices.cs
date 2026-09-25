using Shared.Models.Dtos;
using Shared.Models.Requests;

namespace Client.Services.Interfaces;

// Contrat pour la logique client du hub /game. Reflète GameServices.
public interface IGameServices
{
    event Action<RoundCatalogDto>? RoundStarted;
    event Action? WaitingForOtherPlayers;

    Task<bool> ConnectAsync(string gameId);

    /// <summary>
    /// Envoie une candidature (appel d'offre, consultants affectés, prix).
    /// Retourne null si le serveur l'a acceptée, sinon le message d'erreur à afficher.
    /// </summary>
    Task<string?> SubmitApplicationAsync(ApplyToTenderCommand command);
}