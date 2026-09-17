namespace Shared.Models.Dtos;

public class RoundCatalogDto
{
    public int RoundNumber { get; init;}
    public int TotalRounds { get; init;}
    public required IReadOnlyList<TenderDto> AvailableTenders { get; init;}
    public required IReadOnlyList<TrainingDto> AvailableTrainings { get; init;}
    public required CompanyDto PlayerCompany { get; init;}
};