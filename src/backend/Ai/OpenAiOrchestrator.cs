using Microsoft.Extensions.AI;
using Mommey.Backend.Mcp;
using System.Text.Json;

namespace Mommey.Backend.Ai;

public class OpenAiOrchestrator : IIntentOrchestrator
{
    private readonly IChatClient _chatClient;
    private readonly IMcpClient _mcpClient;
    private readonly ILogger<OpenAiOrchestrator> _logger;

    public OpenAiOrchestrator(IChatClient chatClient, IMcpClient mcpClient, ILogger<OpenAiOrchestrator> logger)
    {
        _chatClient = chatClient;
        _mcpClient = mcpClient;
        _logger = logger;
    }

    public async Task<OrchestrationResult> DiscernIntentAsync(string userMessage)
    {
        _logger.LogInformation("Discerning intent for message: {Message}", userMessage);

        var systemPrompt = """
            You are the intent classifier for 'Mommey', an AI assistant.
            Categorize the user's message into one of three intents:
            - 'Calendar': For tasks related to Google Calendar, meetings, reminders, appointments, or scheduling.
            - 'Journal': For tasks related to Notion, note-taking, journaling, or writing down thoughts.
            - 'General': For anything else.

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

                _logger.LogInformation("Intent identified: {Intent}", intent);

                if (intent == UserIntent.Calendar || intent == UserIntent.Journal)
                {
                    return await HandleMcpIntentAsync(intent, userMessage);
                }

                return new OrchestrationResult(intent, content); // Fallback to raw LLM response or general message
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse intent or handle MCP call");
        }

        return new OrchestrationResult(UserIntent.General, "I'm here to help, but I'm not sure how to handle that specific request yet.");
    }

    private async Task<OrchestrationResult> HandleMcpIntentAsync(UserIntent intent, string userMessage)
    {
        string serverName = intent == UserIntent.Calendar ? "Google" : "Notion";
        _logger.LogInformation("Handling MCP intent for {ServerName}", serverName);

        // 1. List tools for the server
        var tools = await _mcpClient.ListToolsAsync(serverName);
        var aiTools = tools.Select(t => (AIFunction)t).ToList();
        _logger.LogInformation("Found {Count} tools for server {ServerName}", aiTools.Count, serverName);

        // 2. Wrap client with tool invocation support
        // Note: Using Microsoft.Extensions.AI's FunctionInvokingChatClient or similar
        // For simplicity, we can just pass tools to GetResponseAsync if the client supports it.
        // We'll use the builder to make sure tool calling works.
        
        var toolCallingClient = _chatClient.AsBuilder().UseFunctionInvocation().Build();

        var mcpSystemPrompt = $"""
            You are assisting the user with their {serverName} account.
            Use the provided tools to fulfill the user's request.
            If you need more information, ask the user.
            """;

        var response = await toolCallingClient.GetResponseAsync(new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, mcpSystemPrompt),
            new ChatMessage(ChatRole.User, userMessage)
        }, new ChatOptions { Tools = aiTools.Cast<AITool>().ToList() });

        return new OrchestrationResult(intent, response.ToString());
    }
}
