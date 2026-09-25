using Server.Domain.Base;
using Server.Domain.Enums;

namespace Server.Domain;

/// <summary>
/// Candidature d'une entreprise à un appel d'offres pour un tour donné. Éphémère : le serveur
/// la résout en Won/Lost lors du calcul de tour. Une candidature gagnante engendre un Contract.
/// </summary>
public class TenderApplication : BaseModel
{
    public required Round Round { get; init; }
    public required Company Company { get; init; }
    public required Tender Tender { get; init; }
    public ICollection<Consultant> AssignedConsultants { get; init; } = [];
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public decimal? Bid { get; init; }
}
