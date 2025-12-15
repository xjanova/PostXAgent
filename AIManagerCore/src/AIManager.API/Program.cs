using AIManager.API.Hubs;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;

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

// Orchestrator
builder.Services.AddSingleton<ProcessOrchestrator>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowLaravel");
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
