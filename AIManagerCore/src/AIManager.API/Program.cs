using AIManager.API.Hubs;
using AIManager.API.Middleware;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;
using AIManager.Core.Workers;
using AIManager.Core.WebAutomation;
using AIManager.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AI Manager API", Version = "v1" });
});

// SignalR for real-time communication
builder.Services.AddSignalR();

// CORS for Laravel
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLaravel", policy =>
    {
        policy.WithOrigins(
            builder.Configuration.GetValue<string>("Laravel:Url") ?? "http://localhost:8000"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// Configuration
var config = new OrchestratorConfig
{
    NumCores = builder.Configuration.GetValue<int>("Orchestrator:NumCores"),
    ApiPort = builder.Configuration.GetValue<int>("Orchestrator:ApiPort", 5000),
    WebSocketPort = builder.Configuration.GetValue<int>("Orchestrator:WebSocketPort", 5001),
    SignalRPort = builder.Configuration.GetValue<int>("Orchestrator:SignalRPort", 5002),
    RedisConnectionString = builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379"
};
builder.Services.AddSingleton(config);

// Workers
builder.Services.AddSingleton<WorkerFactory>();

// Web Automation Services
builder.Services.AddSingleton<WorkflowStorage>();
builder.Services.AddSingleton<AIElementAnalyzer>();
builder.Services.AddSingleton<WorkflowLearningEngine>();

// Orchestrator
builder.Services.AddSingleton<ProcessOrchestrator>();

// API Key Service
builder.Services.AddSingleton<ApiKeyService>();

var app = builder.Build();

// Generate master key if no keys exist (for initial setup)
var apiKeyService = app.Services.GetRequiredService<ApiKeyService>();
var masterKey = apiKeyService.GenerateMasterKeyIfNeeded();
if (masterKey != null)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("========================================");
    logger.LogWarning("INITIAL SETUP: Master API Key Generated");
    logger.LogWarning("Key: {PlainKey}", masterKey.PlainKey);
    logger.LogWarning("SAVE THIS KEY! It won't be shown again!");
    logger.LogWarning("========================================");
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowLaravel");

// API Key Authentication Middleware
app.UseApiKeyAuth();

app.UseAuthorization();
app.MapControllers();

// SignalR Hub
app.MapHub<AIManagerHub>("/hub/aimanager");

// Start orchestrator on startup
var orchestrator = app.Services.GetRequiredService<ProcessOrchestrator>();
app.Lifetime.ApplicationStarted.Register(async () =>
{
    await orchestrator.StartAsync();
});

app.Lifetime.ApplicationStopping.Register(async () =>
{
    await orchestrator.StopAsync();
});

app.Run();
