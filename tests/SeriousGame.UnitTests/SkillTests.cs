using Server.Domain;
using Server.Domain.Enums;

namespace SeriousGame.UnitTests;

public class SkillTests
{
    [Fact]
    public void LevelUp_AdvancesOneStep()
    {
        var skill = new Skill { Id = 1, Name = "C#" };

        skill.LevelUp();

        Assert.Equal(Level.Intermediate, skill.Level);
    }

    [Fact]
    public void LevelUp_AtExpert_StaysAtExpert()
    {
        var skill = new Skill { Id = 1, Name = "C#" };
        for (var i = 0; i < 10; i++) skill.LevelUp();

        Assert.Equal(Level.Expert, skill.Level);
    }
}
