using Server.Domain;

namespace Server.Application.Services;

public class TenderCatalogService
{
    public Round OpenFirstRound(Game game)
    {
        var round = new Round { Game = game, Order = 1, Tenders = GenerateTenders() };
        game.Rounds.Add(round);
        return round;
    }

    private static List<Tender> GenerateTenders() => new()
    {
        new Tender { Name = "Refonte site e-commerce", Budget = 150000, RoundsNumber = 2 },
        new Tender { Name = "Migration Cloud Azure",   Budget = 220000, RoundsNumber = 3 },
        new Tender { Name = "App mobile de livraison", Budget = 90000,  RoundsNumber = 2 },
    };
}