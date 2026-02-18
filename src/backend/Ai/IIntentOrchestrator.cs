namespace Mommey.Backend.Ai;

public enum UserIntent
{
    General,
    Calendar,
    Journal
}

public record OrchestrationResult(UserIntent Intent, string Response);

public interface IIntentOrchestrator
{
    Task<OrchestrationResult> DiscernIntentAsync(string userMessage);
}
