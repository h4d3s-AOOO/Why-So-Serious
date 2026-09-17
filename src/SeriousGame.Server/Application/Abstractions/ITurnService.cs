using Server.Domain;

namespace Server.Application.Abstractions;

public interface ITurnService
{
    void ResolveRound(Round round);
}