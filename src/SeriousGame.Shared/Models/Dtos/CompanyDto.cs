namespace Shared.Models.Dtos;

// Entreprise
public class CompanyDto {
    public required string Id {get; init;}
    public required string Name {get; init;}
    public decimal Treasury {get; init;}
    public required IReadOnlyList<ConsultantDto> Staff {get; init;}
    public required IReadOnlyList<ContractDto> ActiveContracts {get; init;}
};
