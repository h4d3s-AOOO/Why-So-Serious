using Shared.Models.Dtos;

namespace Client.UI;

public static class ConsoleUI
{
    // --- Méthodes existantes ---

    public static void WriteHeader(string text)
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n {text} \n");
        Console.ResetColor();
    }

    public static void WriteInfo(string text)
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void WriteError(string text)
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($@"❌ {text}");
        Console.ResetColor();
    }

    public static void WritePrompt(string text)
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static string? ReadPrompt()
    {
        Console.BackgroundColor = ConsoleColor.Black;
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? null : input;
    }

    // --- Nouveaux affichages pour la phase de jeu ---

    public static void DisplayCompanyDashboard(CompanyDto company, int roundNumber, int totalRounds)
    {
        WriteHeader($"=== ROUND {roundNumber} / {totalRounds} — {company.Name} ===");
        WriteInfo($"💰 Treasury : {company.Treasury:N0} €");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("\n👥 Staff :");
        for (var i = 0; i < company.Staff.Count; i++)
        {
            var c = company.Staff[i];
            var skills = string.Join(", ", c.Skills.Select(s => $"{s.Name} ({s.Level})"));
            var status = c.IsAvailable ? "[Available]" : "[Busy]";
            Console.WriteLine($"  [{i + 1}] {c.FullName} | Salary : {c.SalaryRequirement:N0} € | {status} | Skills : {skills}");
        }

        if (company.ActiveContracts.Count > 0)
        {
            Console.WriteLine("\n📋 Contrats en cours :");
            foreach (var contract in company.ActiveContracts)
            {
                var assigned = string.Join(", ", contract.AssignedConsultantNames);
                Console.WriteLine($"  - {contract.TenderName} | Budget : {contract.Budget:N0} € | Remaining : {contract.RemainingRounds} round(s) | Assigned : {assigned}");
            }
        }
        Console.ResetColor();
    }

    public static void DisplayTenders(IReadOnlyList<TenderDto> tenders)
    {
        WriteHeader("--- AVAILABLE TENDERS---");
        for (var i = 0; i < tenders.Count; i++)
        {
            var t = tenders[i];
            var skills = string.Join(", ", t.RequiredSkills.Select(s => $"{s.Name} ({s.Level})"));
            Console.WriteLine($"[{i + 1}] {t.Name} | Budget : {t.Budget:N0} € | Duration : {t.RoundsDuration} round(s)");
            Console.WriteLine($"    Required skills : {skills}");
        }
    }

    public static void DisplayTrainings(IReadOnlyList<TrainingDto> trainings)
    {
        WriteHeader("--- AVAILABLE TRAININGS ---");
        for (var i = 0; i < trainings.Count; i++)
        {
            var tr = trainings[i];
            Console.WriteLine($"[{i + 1}] {tr.Name} | Cost : {tr.Cost:N0} € | Duration : {tr.RoundsDuration} round(s) | Gain : {tr.ProducedSkill.Name} ({tr.ProducedSkill.Level})");
        }
    }
}