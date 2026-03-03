using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Mommey.Backend.Mcp;
using System.Text.Json;

namespace Mommey.Backend.Ai;

public class OpenAiOrchestrator : IIntentOrchestrator
{
    private readonly IChatClient _chatClient;
    private readonly IMcpClient _mcpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OpenAiOrchestrator> _logger;
    private readonly int _sessionTimeoutMinutes;

    public OpenAiOrchestrator(IChatClient chatClient, IMcpClient mcpClient, IMemoryCache cache, IConfiguration configuration, ILogger<OpenAiOrchestrator> logger)
    {
        _chatClient = chatClient;
        _mcpClient = mcpClient;
        _cache = cache;
        _logger = logger;
        _sessionTimeoutMinutes = configuration.GetValue<int>("SessionTimeoutMinutes", 30);
    }

    public async Task<OrchestrationResult> DiscernIntentAsync(string userMessage, string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
            _logger.LogWarning("SessionId was null or empty, generated fallback session ID: {SessionId}", sessionId);
        }

        _logger.LogInformation("Discerning intent for message: {Message} [Session: {SessionId}]", userMessage, sessionId);

        bool isNewSession = false;
        if (!_cache.TryGetValue(sessionId, out List<ChatMessage>? history) || history == null)
        {
            history = new List<ChatMessage>();
            isNewSession = true;
        }

        // Check if history exceeds 2000 chars and summarize
        int historyLength = history.Sum(m => m.Text?.Length ?? 0);
        if (historyLength > 2000)
        {
            _logger.LogInformation("History length {Length} > 2000, summarizing...", historyLength);
            var summarizePrompt = new ChatMessage(ChatRole.System, "Summarize this conversation briefly, focusing only on the active context and facts.");
            var msgsToSummarize = new List<ChatMessage> { summarizePrompt };
            msgsToSummarize.AddRange(history);
            
            var summaryResponse = await _chatClient.GetResponseAsync(msgsToSummarize);
            history = new List<ChatMessage> { new ChatMessage(ChatRole.Assistant, $"Context Summary: {summaryResponse.ToString()}") };
        }

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

        var intentClassificationMessages = new List<ChatMessage> { new ChatMessage(ChatRole.System, systemPrompt) };
        intentClassificationMessages.AddRange(history);
        intentClassificationMessages.Add(new ChatMessage(ChatRole.User, userMessage));

        var response = await _chatClient.GetResponseAsync(intentClassificationMessages);
        var content = response.ToString();
        _logger.LogDebug("LLM raw response: {Response}", content);

        UserIntent intent = UserIntent.General;
        try 
        {
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd >= 0)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(json);
                var intentStr = doc.RootElement.GetProperty("intent").GetString();

                intent = intentStr switch
                {
                    "Calendar" => UserIntent.Calendar,
                    "Journal" => UserIntent.Journal,
                    _ => UserIntent.General
                };
                _logger.LogInformation("Intent identified: {Intent}", intent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse intent");
        }

        string finalResponseText;
        if (intent == UserIntent.Calendar || intent == UserIntent.Journal)
        {
            finalResponseText = await HandleMcpIntentAsync(intent, userMessage, history);
        }
        else
        {
            var generalPrompt = "You are Mommey, a helpful AI assistant. Answer the user's query.";
            var generalMsgs = new List<ChatMessage> { new ChatMessage(ChatRole.System, generalPrompt) };
            generalMsgs.AddRange(history);
            generalMsgs.Add(new ChatMessage(ChatRole.User, userMessage));
            
            var generalResponse = await _chatClient.GetResponseAsync(generalMsgs);
            finalResponseText = generalResponse.ToString() ?? "";
        }

        // Append to history
        history.Add(new ChatMessage(ChatRole.User, userMessage));
        history.Add(new ChatMessage(ChatRole.Assistant, finalResponseText));

        // Save back to cache
        _cache.Set(sessionId, history, TimeSpan.FromMinutes(_sessionTimeoutMinutes));

        if (isNewSession)
        {
            finalResponseText = "_Note: A new session has started. Previous context was cleared._\n\n" + finalResponseText;
        }

        return new OrchestrationResult(intent, finalResponseText);
    }

    private async Task<string> HandleMcpIntentAsync(UserIntent intent, string userMessage, List<ChatMessage> history)
    {
        string serverName = intent == UserIntent.Calendar ? "Google" : "Notion";
        _logger.LogInformation("Handling MCP intent for {ServerName}", serverName);

        var tools = await _mcpClient.ListToolsAsync(serverName);
        var aiTools = tools.Select(t => (AIFunction)t).ToList();
        
        var toolCallingClient = _chatClient.AsBuilder().UseFunctionInvocation().Build();

        var mcpSystemPrompt = $"""
            You are assisting the user with their {serverName} account.
            Use the provided tools to fulfill the user's request.
            If you need more information, ask the user.
            """;

        var msgs = new List<ChatMessage> { new ChatMessage(ChatRole.System, mcpSystemPrompt) };
        msgs.AddRange(history);
        msgs.Add(new ChatMessage(ChatRole.User, userMessage));

        var response = await toolCallingClient.GetResponseAsync(msgs, new ChatOptions { Tools = aiTools.Cast<AITool>().ToList() });

        return response.ToString() ?? "";
    }
}
