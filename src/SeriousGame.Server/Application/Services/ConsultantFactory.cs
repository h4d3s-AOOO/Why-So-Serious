using Server.Domain;

namespace Server.Application.Services;

/// <summary>
/// Génère le staff de départ d'une entreprise (pattern Factory) : un petit pool de noms fixes,
/// un salaire et une compétence aléatoires pour chaque consultant.
/// </summary>
internal static class ConsultantFactory
{
    public const int StartingStaffSize = 4;

    private static readonly (string Firstname, string Lastname)[] NamePool =
    [
        ("Alice", "Martin"),
        ("Bruno", "Dubois"),
        ("Chloé", "Bernard"),
        ("Dominique", "David"),
        ("Emma", "Robert"),
        ("Farid", "Moreau"),
        ("Gaëlle", "Laurent"),
        ("Pierre", "Simon"),
    ];

    private static readonly string[] SkillNames = ["Développement", "Cloud", "Gestion de projet", "UX/UI", "Cybersécurité"];

    public static void StaffCompany(Company company, Random random)
    {
        var pickedNames = NamePool.OrderBy(_ => random.Next()).Take(StartingStaffSize);

        foreach (var (firstname, lastname) in pickedNames)
        {
            var consultant = new Consultant
            {
                Firstname = firstname,
                Lastname = lastname,
                Company = company
            };

            consultant.SetSalaryRequirement(random.Next(3000, 7001));
            consultant.Skills.Add(new Skill { Id = random.Next(1, 1000), Name = SkillNames[random.Next(SkillNames.Length)] });

            company.Staff.Add(consultant);
        }
    }
}