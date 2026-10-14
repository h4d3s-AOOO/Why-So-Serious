using Shared.Models.Dtos;
using Shared.Models.Requests;

namespace Server.Application.Abstractions;

/// <summary>
/// Flux du lobby : création/join/leave d'une partie et gestion des déconnexions.
/// </summary>
public interface ILobbyFlowService
{
    Task NotifyLobbyOnConnection(string connectionId);
    Task IdentifyNewPlayer(CreatePlayerCommand command, string connectionId);
    Task UpdatePlayerConnectionId(string clientId, string connectionId);
    Task<GameDto> CreateGame(CreateGameCommand command, string connectionId);
    Task<GameDto?> JoinGame(JoinGameCommand command, string connectionId);
    Task LeaveGame(string connectionId);
    Task HandlePlayerDisconnect(string connectionId);
}
