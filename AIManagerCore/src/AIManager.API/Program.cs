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

// Core Database Service (SQLite + MySQL failover)
builder.Services.AddSingleton<CoreDatabaseService>();

// AI Learning Database Service (for SeekAndPost)
builder.Services.AddSingleton<AILearningDatabaseService>();

// Content Generator Service
builder.Services.AddSingleton<ContentGeneratorService>();

// Seek and Post Service
builder.Services.AddSingleton<SeekAndPostService>();

// Group Search and Post Publisher Services
builder.Services.AddSingleton<GroupSearchService>();
builder.Services.AddSingleton<PostPublisherService>();

// Comment Management Services
builder.Services.AddSingleton<CommentMonitorService>();
builder.Services.AddSingleton<CommentReplyService>();
builder.Services.AddSingleton<TonePersonalityService>();

// Viral Analysis Service
builder.Services.AddSingleton<ViralAnalysisService>();

// Image Generator Service
builder.Services.AddSingleton<ImageGeneratorService>();

// Media Processing Services (Video/Audio)
builder.Services.AddSingleton<FFmpegService>();
builder.Services.AddSingleton<VideoProcessor>();
builder.Services.AddSingleton<AudioProcessor>();

// Freepik Automation Service (Image/Video Generation)
builder.Services.AddSingleton<FreepikAutomationService?>(sp =>
{
    try
    {
        var browserController = sp.GetService<BrowserController>();
        var workflowStorage = sp.GetRequiredService<WorkflowStorage>();
        var workflowExecutor = sp.GetService<WorkflowExecutor>();
        var logger = sp.GetRequiredService<ILogger<FreepikAutomationService>>();
        if (browserController != null && workflowExecutor != null)
        {
            return new FreepikAutomationService(logger, browserController, workflowExecutor, workflowStorage);
        }
    }
    catch { }
    return null;
});

// Suno Automation Service (Music Generation)
builder.Services.AddSingleton<SunoAutomationService?>(sp =>
{
    try
    {
        var browserController = sp.GetService<BrowserController>();
        var workflowStorage = sp.GetRequiredService<WorkflowStorage>();
        var workflowExecutor = sp.GetService<WorkflowExecutor>();
        var logger = sp.GetRequiredService<ILogger<SunoAutomationService>>();
        if (browserController != null && workflowExecutor != null)
        {
            return new SunoAutomationService(logger, browserController, workflowExecutor, workflowStorage);
        }
    }
    catch { }
    return null;
});

// Video Creation Pipeline (Full workflow: Images -> Videos -> Music -> Compose -> Post)
builder.Services.AddSingleton<VideoCreationPipeline?>(sp =>
{
    try
    {
        var freepikService = sp.GetService<FreepikAutomationService>();
        var sunoService = sp.GetService<SunoAutomationService>();

        if (freepikService == null || sunoService == null)
        {
            return null;
        }

        var logger = sp.GetRequiredService<ILogger<VideoCreationPipeline>>();
        var videoProcessor = sp.GetRequiredService<VideoProcessor>();
        var audioProcessor = sp.GetRequiredService<AudioProcessor>();
        var contentGenerator = sp.GetRequiredService<ContentGeneratorService>();
        var postPublisher = sp.GetRequiredService<PostPublisherService>();

        return new VideoCreationPipeline(
            logger,
            freepikService,
            sunoService,
            videoProcessor,
            audioProcessor,
            contentGenerator,
            postPublisher);
    }
    catch { }
    return null;
});

// Content Workflow Services
builder.Services.AddSingleton<CloudDriveService>();
builder.Services.AddSingleton<VideoScriptGeneratorService>();
builder.Services.AddSingleton<AudioGeneratorService>();
builder.Services.AddSingleton<VideoAssemblyService>();
builder.Services.AddSingleton<ContentWorkflowOrchestrator>();

// Worker Manager (for managing all workers)
builder.Services.AddSingleton<WorkerManager>();

// Knowledge Base (shared knowledge between workers)
builder.Services.AddSingleton<KnowledgeBase>();

// Human Training Service (for human-in-the-loop training)
builder.Services.AddSingleton<HumanTrainingService>();

// Workflow Runtime Manager (workflow versioning and job management)
builder.Services.AddSingleton<WorkflowRuntimeManager>();

// AI Code Generation Configuration
var aiCodeConfig = new AIManager.Core.Models.AICodeGenerationConfig();
builder.Configuration.GetSection("AICodeGeneration").Bind(aiCodeConfig);
builder.Services.AddSingleton(aiCodeConfig);

// Self-Healing Configuration
var selfHealingConfig = new AIManager.Core.Models.SelfHealingConfig();
builder.Configuration.GetSection("SelfHealing").Bind(selfHealingConfig);
builder.Services.AddSingleton(selfHealingConfig);

// Claude Desktop Configuration
var claudeDesktopConfig = new AIManager.Core.Models.ClaudeDesktopConfig();
builder.Configuration.GetSection("ClaudeDesktop").Bind(claudeDesktopConfig);
builder.Services.AddSingleton(claudeDesktopConfig);

// Claude Desktop Service (for Local AI <-> Claude Desktop integration)
builder.Services.AddSingleton<ClaudeDesktopService>();

// Claude Code Integration Service (uses user's Claude account via CLI)
builder.Services.AddSingleton<ClaudeCodeIntegrationService>();

// AI Code Generator Service (for dynamic JS code generation)
builder.Services.AddSingleton<AICodeGeneratorService>();

// Dynamic Code Executor (optional - requires WebView)
// Note: DynamicCodeExecutor requires BrowserController which needs UI context
// It will be null in API-only mode
builder.Services.AddSingleton<DynamicCodeExecutor?>(sp =>
{
    try
    {
        var browserController = sp.GetService<BrowserController>();
        if (browserController != null)
        {
            return new DynamicCodeExecutor(browserController);
        }
    }
    catch { }
    return null;
});

// Self-Healing Worker Factory
builder.Services.AddSingleton<SelfHealingWorker>();

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

// Initialize Core Database (SQLite auto-creates, MySQL if configured)
var coreDb = app.Services.GetRequiredService<CoreDatabaseService>();

// Configure MySQL from settings if available
coreDb.MysqlHost = builder.Configuration.GetValue<string>("Database:MySQL:Host");
coreDb.MysqlPort = builder.Configuration.GetValue<int>("Database:MySQL:Port", 3306);
coreDb.MysqlDatabase = builder.Configuration.GetValue<string>("Database:MySQL:Database");
coreDb.MysqlUsername = builder.Configuration.GetValue<string>("Database:MySQL:Username");
coreDb.MysqlPassword = builder.Configuration.GetValue<string>("Database:MySQL:Password");

await coreDb.InitializeAsync();

// Initialize Knowledge Base schema
var knowledgeBase = app.Services.GetRequiredService<KnowledgeBase>();
await knowledgeBase.InitializeSchemaAsync();

// Initialize Workflow Runtime Manager (load all workflows)
var workflowRuntime = app.Services.GetRequiredService<WorkflowRuntimeManager>();
await workflowRuntime.LoadAllWorkflowsAsync();

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
