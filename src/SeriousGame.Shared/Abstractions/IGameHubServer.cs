using Shared.Models.Requests;

namespace Shared.Abstractions;

public interface IGameHubServer
{
    Task SubmitApplicationAsync(ApplyToTenderCommand command);
    Task EnrollConsultantAsync(EnrollTrainingCommand command);
    Task ReadyForNextRoundAsync(EndTurnCommand command);
}