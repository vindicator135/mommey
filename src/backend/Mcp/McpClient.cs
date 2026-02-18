using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Collections.Concurrent;

namespace Mommey.Backend.Mcp;

public interface IMcpClient
{
    Task<string> ExecuteToolAsync(string serverName, string toolName, Dictionary<string, object?> arguments);
    Task<IEnumerable<AIFunction>> ListToolsAsync(string serverName);
}

public class McpClient : IMcpClient, IAsyncDisposable
{
    private readonly ILogger<McpClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, ModelContextProtocol.Client.McpClient> _clients = new();

    public McpClient(ILogger<McpClient> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private async Task<ModelContextProtocol.Client.McpClient> GetOrConnectClientAsync(string serverName)
    {
        if (_clients.TryGetValue(serverName, out var client))
        {
            return client;
        }

        _logger.LogInformation("Connecting to MCP server: {ServerName}", serverName);

        var section = _configuration.GetSection($"Mcp:{serverName}");
        var command = section["Command"];
        var args = section.GetSection("Arguments").Get<string[]>();

        if (string.IsNullOrEmpty(command))
        {
            throw new InvalidOperationException($"MCP server configuration for {serverName} not found.");
        }

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = serverName,
            Command = command,
            Arguments = args ?? []
        });

        var newClient = await ModelContextProtocol.Client.McpClient.CreateAsync(transport);
        _clients[serverName] = newClient;
        
        return newClient;
    }

    public async Task<string> ExecuteToolAsync(string serverName, string toolName, Dictionary<string, object?> arguments)
    {
        try
        {
            var client = await GetOrConnectClientAsync(serverName);
            _logger.LogInformation("Executing tool {ToolName} on {ServerName}", toolName, serverName);

            var result = await client.CallToolAsync(toolName, arguments);
            
            // Extract text content from the result
            var textBody = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
            return textBody;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName} on {ServerName}", toolName, serverName);
            return $"Error: {ex.Message}";
        }
    }

    public async Task<IEnumerable<AIFunction>> ListToolsAsync(string serverName)
    {
        try
        {
            var client = await GetOrConnectClientAsync(serverName);
            var tools = await client.ListToolsAsync();
            return tools.Cast<AIFunction>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing tools for {ServerName}", serverName);
            return Enumerable.Empty<AIFunction>();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients.Values)
        {
            await client.DisposeAsync();
        }
    }
}
