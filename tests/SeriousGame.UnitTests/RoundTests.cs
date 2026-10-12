using Server.Domain;

namespace SeriousGame.UnitTests;

public class RoundTests
{
    private static Player MakePlayer(string id) =>
        new() { Id = id, Nickname = $"Player {id}", ConnectionId = $"c-{id}" };

    private static (Round round, Company a, Company b) BuildRoundWithTwoCompanies()
    {
        var owner = MakePlayer("p1");
        var game = new Game { Name = "Test Game", Owner = owner };

        var a = new Company { Name = "A & Co", PlayerOwner = owner };
        var b = new Company { Name = "B & Co", PlayerOwner = MakePlayer("p2") };
        game.Companies.Add(a);
        game.Companies.Add(b);

        return (new Round { Game = game, Order = 1 }, a, b);
    }

    [Fact]
    public void AllCompaniesReady_IsFalse_UntilEveryCompanyIsReady()
    {
        var (round, a, b) = BuildRoundWithTwoCompanies();

        round.MarkCompanyReady(a.Id);
        Assert.False(round.AllCompaniesReady);

        round.MarkCompanyReady(b.Id);
        Assert.True(round.AllCompaniesReady);
    }

    [Fact]
    public void MarkCompanyReady_Twice_CountsOnlyOnce()
    {
        var (round, a, _) = BuildRoundWithTwoCompanies();

        round.MarkCompanyReady(a.Id);
        round.MarkCompanyReady(a.Id);

        Assert.Single(round.ReadyCompanyIds);
        Assert.False(round.AllCompaniesReady);
    }
}