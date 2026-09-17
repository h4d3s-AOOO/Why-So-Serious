namespace Shared.Models.Dtos;

public class RoundCatalogDto
{
    public int RoundNumber { get; }
    public int TotalRounds { get; }
    public required IReadOnlyList<TenderDto> AvailableTenders { get; init;}
    public required IReadOnlyList<TrainingDto> AvailableTrainings { get; init;}
    public required CompanyDto PlayerCompany { get; init;}
};