using Serilog;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Mommey.Backend.Ai;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Configure OpenAI
var openAiConfig = builder.Configuration.GetSection("OpenAI");
var apiKey = openAiConfig["ApiKey"];
var model = openAiConfig["ModelId"] ?? "gpt-4o-mini";

if (!string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_OPENAI_API_KEY_HERE")
{
    var openAiClient = new OpenAIClient(apiKey);
    var chatClient = openAiClient.GetChatClient(model);
    builder.Services.AddChatClient(chatClient.AsIChatClient());
}
else
{
    // Fallback or warning if key is missing
    builder.Services.AddChatClient(new Samples.FakeChatClient());
}

builder.Services.AddSingleton<IIntentOrchestrator, OpenAiOrchestrator>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();

app.MapGet("/", () => "Mommey Backend is running!");

app.MapPost("/api/chat", async (ChatMessageRequest request, IIntentOrchestrator orchestrator) =>
{
    var result = await orchestrator.DiscernIntentAsync(request.Message);
    return Results.Ok(new { result.Intent, result.Response });
})
.WithName("PostChat");

app.Run();

public record ChatMessageRequest(string Message);
