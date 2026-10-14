using Server.Domain.Base;

namespace Server.Domain;

public class Round : BaseModel
{
    public required Game Game { get; init; }
    public int Order { get; init; }
    public bool IsCompleted { get; set; } = false;

    // Catalogue proposé ce tour.
    public ICollection<Tender> Tenders { get; init; } = [];
    public ICollection<Training> Trainings { get; init; } = [];

    // Décisions des entreprises ce tour, résolues en Won/Lost au calcul de tour.
    public ICollection<TenderApplication> Applications { get; init; } = [];

    // ← ajouts US-07 : entreprises qui ont validé leur tour
    public HashSet<string> ReadyCompanyIds { get; } = [];

    /// <summary>Enregistre qu'une entreprise a terminé son tour (sans effet si déjà fait).</summary>
    public void MarkCompanyReady(string companyId) => ReadyCompanyIds.Add(companyId);

    /// <summary>Vrai quand toutes les entreprises de la partie ont validé : le tour peut être résolu.</summary>
    public bool AllCompaniesReady => Game.Companies.All(c => ReadyCompanyIds.Contains(c.Id));
}