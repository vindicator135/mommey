using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Mommey.Backend.Ai;

public class OpenAiOrchestrator : IIntentOrchestrator
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<OpenAiOrchestrator> _logger;

    public OpenAiOrchestrator(IChatClient chatClient, ILogger<OpenAiOrchestrator> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<OrchestrationResult> DiscernIntentAsync(string userMessage)
    {
        _logger.LogInformation("Discerning intent for message: {Message}", userMessage);

        var systemPrompt = """
            You are the intent classifier for 'Mommey', an AI assistant.
            Categorize the user's message into one of three intents:
            - 'Calendar': For tasks related to Google Calendar, meetings, reminders, appointments, or scheduling.
            - 'Journal': For tasks related to Notion, note-taking, journaling, trading logs, or writing down thoughts.
            - 'General': For anything else

            Respond ONLY with a JSON object in the following format:
            {
              "intent": "Calendar" | "Journal" | "General",
              "reasoning": "Brief explanation of why"
            }
            """;

        var response = await _chatClient.GetResponseAsync(new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userMessage)
        });

        var content = response.ToString();
        _logger.LogDebug("LLM raw response: {Response}", content);

        try 
        {
            // Simple parsing for now, can be improved with structured output
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd >= 0)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(json);
                var intentStr = doc.RootElement.GetProperty("intent").GetString();

                var intent = intentStr switch
                {
                    "Calendar" => UserIntent.Calendar,
                    "Journal" => UserIntent.Journal,
                    _ => UserIntent.General
                };

                return new OrchestrationResult(intent, $"Intent identified as {intent}. Reasoning: {doc.RootElement.GetProperty("reasoning").GetString()}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse intent from LLM response");
        }

        return new OrchestrationResult(UserIntent.General, "Could not determine specific intent, defaulting to General.");
    }
}
