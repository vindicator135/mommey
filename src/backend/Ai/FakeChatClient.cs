using Microsoft.Extensions.AI;

namespace Samples;

public class FakeChatClient : IChatClient
{
    public ChatClientMetadata Metadata => new ChatClientMetadata("FakeClient");

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lastMessage = chatMessages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        string response;

        if (lastMessage.Contains("meeting", StringComparison.OrdinalIgnoreCase) || lastMessage.Contains("calendar", StringComparison.OrdinalIgnoreCase))
        {
            response = "{\"intent\": \"Calendar\", \"reasoning\": \"Mentioned meeting/calendar\"}";
        }
        else if (lastMessage.Contains("journal", StringComparison.OrdinalIgnoreCase) || lastMessage.Contains("note", StringComparison.OrdinalIgnoreCase))
        {
            response = "{\"intent\": \"Journal\", \"reasoning\": \"Mentioned journal/note\"}";
        }
        else
        {
            response = "{\"intent\": \"General\", \"reasoning\": \"No clear trigger keywords\"}";
        }

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Dispose() { }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
}
