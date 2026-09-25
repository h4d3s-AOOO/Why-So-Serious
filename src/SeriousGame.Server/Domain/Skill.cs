using Server.Domain.Enums;

namespace Server.Domain;

public class Skill
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public Level Level { get; private set; } = Level.Basic;

    /// <summary>Fait progresser le niveau d'un cran (typiquement à la fin d'une formation), plafonné à Expert.</summary>
    public void LevelUp()
    {
        if (Level < Level.Expert) Level++;
    }
}