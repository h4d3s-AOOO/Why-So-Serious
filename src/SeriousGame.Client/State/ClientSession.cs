using Shared.Models.Dtos;

namespace Client.State;

/// <summary>
/// État de session du client : identité du joueur (persistée dans un fichier client_id_&lt;pseudo&gt;.txt)
/// et état du lobby. Injectée en Singleton - une instance pour toute l'exécution ; les mutations
/// passent par des méthodes nommées plutôt que des setters publics, pour que chaque transition
/// (entrer/quitter une partie...) soit un point d'appel explicite.
/// </summary>
public class ClientSession
{
    public string PlayerId { get; private set; } = string.Empty;
    public string? Nickname { get; private set; }
    public List<GameDto> Games { get; private set; } = [];
    public GameDto? CurrentGame { get; private set; }
    public RoundCatalogDto? CurrentRound {  get; private set; }

    public void Identify(string nickname)
    {
        Nickname = nickname;
        PlayerId = ResolvePlayerId(nickname);
    }

    private static string ResolvePlayerId(string nickname)
    {
        // Fichier par pseudo (et non par process) : plusieurs instances lancées depuis le même
        // dossier - cas du test local multi-joueurs, tous via `dotnet run` dans le même répertoire -
        // restent des joueurs distincts, tout en gardant un identifiant stable d'une exécution à
        // l'autre pour un même pseudo (reconnexion après redémarrage).
        var fileName = string.Format(Constants.ClientIdFilePattern, SanitizeForFileName(nickname));

        if (File.Exists(fileName))
        {
            return File.ReadAllText(fileName);
        }

        var playerId = Guid.NewGuid().ToString();
        File.WriteAllText(fileName, playerId);
        return playerId;
    }

    private static string SanitizeForFileName(string nickname)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(nickname.Select(c => invalidChars.Contains(c) ? '_' : c));
    }

    public void UpdateGames(List<GameDto> games) => Games = games;

    public void EnterGame(GameDto game) => CurrentGame = game;

    public void LeaveCurrentGame() => CurrentGame = null;

    public void MarkPlayerInactiveAfterOtherQuit(GameDto updatedGame)
    {
        if (CurrentGame is null) return;
        CurrentGame.Players.First(p => !updatedGame.Players.Select(player => player.Id).Contains(p.Id)).IsActive = false;
    }

    public void StartRound(RoundCatalogDto catalog) => CurrentRound = catalog;
}
