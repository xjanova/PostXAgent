using AIManager.API.Hubs;
using AIManager.API.Middleware;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;
using AIManager.Core.Workers;
using AIManager.Core.WebAutomation;
using AIManager.Core.Services;
using AIManager.Core.Services.MusicGeneration;
using AIManager.Core.Services.VideoGeneration;

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

// AI Assistant Service (Status Bar messages)
builder.Services.AddSingleton<AIAssistantService>();

// Media Processing Services (Video/Audio)
builder.Services.AddSingleton<FFmpegService>();
builder.Services.AddSingleton<VideoProcessor>();
builder.Services.AddSingleton<AudioProcessor>();

// Browser Controller (Playwright-based) for Web Automation
builder.Services.AddSingleton<BrowserController>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<BrowserController>>();
    var config = new BrowserConfig
    {
        Headless = builder.Configuration.GetValue<bool>("WebAutomation:Headless", true),
        DefaultTimeout = builder.Configuration.GetValue<int>("WebAutomation:Timeout", 30000)
    };
    return new BrowserController(logger, config);
});

// Workflow Executor (Playwright-based) for executing learned workflows
builder.Services.AddSingleton<WorkflowExecutor>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WorkflowExecutor>>();
    var browserController = sp.GetRequiredService<BrowserController>();
    var learningEngine = sp.GetRequiredService<WorkflowLearningEngine>();
    var aiAnalyzer = sp.GetRequiredService<AIElementAnalyzer>();
    return new WorkflowExecutor(logger, browserController, learningEngine, aiAnalyzer);
});

// Freepik Automation Service (Image/Video Generation)
builder.Services.AddSingleton<FreepikAutomationService>(sp =>
{
    var browserController = sp.GetRequiredService<BrowserController>();
    var workflowStorage = sp.GetRequiredService<WorkflowStorage>();
    var workflowExecutor = sp.GetRequiredService<WorkflowExecutor>();
    var logger = sp.GetRequiredService<ILogger<FreepikAutomationService>>();
    return new FreepikAutomationService(logger, browserController, workflowExecutor, workflowStorage);
});

// Suno Automation Service (Music Generation via Web Automation)
builder.Services.AddSingleton<SunoAutomationService>(sp =>
{
    var browserController = sp.GetRequiredService<BrowserController>();
    var workflowStorage = sp.GetRequiredService<WorkflowStorage>();
    var workflowExecutor = sp.GetRequiredService<WorkflowExecutor>();
    var logger = sp.GetRequiredService<ILogger<SunoAutomationService>>();
    return new SunoAutomationService(logger, browserController, workflowExecutor, workflowStorage);
});

// Suno Session Manager (Music Generation Session/Token Management)
builder.Services.AddSingleton<SunoSessionManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SunoSessionManager>>();
    var sessionPath = builder.Configuration.GetValue<string>("MusicGeneration:Suno:SessionFilePath");
    return new SunoSessionManager(logger, sessionPath);
});

// Music Provider Factory (Music Generation via API - Suno, etc.)
builder.Services.AddSingleton<MusicProviderFactory>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var sessionManager = sp.GetRequiredService<SunoSessionManager>();
    var config = new MusicProviderConfig
    {
        SunoSessionToken = builder.Configuration.GetValue<string>("MusicGeneration:Suno:SessionToken"),
        DownloadPath = builder.Configuration.GetValue<string>("MusicGeneration:DownloadPath")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "PostXAgent", "Generated"),
        DefaultGenre = builder.Configuration.GetValue<string>("MusicGeneration:DefaultGenre") ?? "Pop",
        DefaultMood = builder.Configuration.GetValue<string>("MusicGeneration:DefaultMood") ?? "Energetic",
        DownloadAllSongs = builder.Configuration.GetValue<bool>("MusicGeneration:DownloadAllSongs", true),
        TimeoutSeconds = builder.Configuration.GetValue<int>("MusicGeneration:TimeoutSeconds", 180)
    };
    return new MusicProviderFactory(loggerFactory, config, sessionManager);
});

// Video Provider Factory (Video Generation via API - Runway, Pika, Luma)
builder.Services.AddSingleton<VideoProviderFactory>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var config = new VideoProviderConfig
    {
        RunwayApiKey = builder.Configuration.GetValue<string>("VideoGeneration:Runway:ApiKey"),
        PikaLabsApiKey = builder.Configuration.GetValue<string>("VideoGeneration:PikaLabs:ApiKey"),
        LumaAIApiKey = builder.Configuration.GetValue<string>("VideoGeneration:LumaAI:ApiKey"),
        DownloadPath = builder.Configuration.GetValue<string>("VideoGeneration:DownloadPath")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "PostXAgent", "Generated")
    };
    return new VideoProviderFactory(loggerFactory, config);
});

// Video Creation Pipeline (Full workflow: Images -> Videos -> Music -> Compose -> Post)
builder.Services.AddSingleton<VideoCreationPipeline>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<VideoCreationPipeline>>();
    var freepikService = sp.GetRequiredService<FreepikAutomationService>();
    var sunoService = sp.GetRequiredService<SunoAutomationService>();
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
});

// Thai TTS Service (Edge TTS + Google Cloud TTS)
builder.Services.AddSingleton<ThaiTtsService>();

// Lip Sync Service (SadTalker / Wav2Lip)
builder.Services.AddSingleton<LipSyncService>();

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

// Dynamic Code Executor (uses BrowserController for JS execution)
builder.Services.AddSingleton<DynamicCodeExecutor>(sp =>
{
    var browserController = sp.GetRequiredService<BrowserController>();
    return new DynamicCodeExecutor(browserController);
});

// Self-Healing Worker Factory
builder.Services.AddSingleton<SelfHealingWorker>();

// Local File Server (serves local media files as temporary URLs for platforms)
builder.Services.AddSingleton<LocalFileServerService>();

// GPU Pool Service (Distributed GPU Worker Management)
builder.Services.AddSingleton<GpuPoolService>();

// GPU Node Monitor Service (Advanced monitoring with quota, prestart, failover)
builder.Services.AddSingleton<GpuNodeMonitorService>();

// HuggingFace Model Service (Model Download and Management)
builder.Services.AddSingleton<HuggingFaceModelService>();

// Local GPU Service (GPU Detection and Monitoring)
builder.Services.AddSingleton<LocalGpuService>();

// ComfyUI Service (optional - for ComfyUI backend)
builder.Services.AddSingleton<ComfyUIService>();

// Unified Generation Engine (combines all image/video generation providers)
// Now includes GpuPoolService for distributed generation across multiple GPUs
builder.Services.AddSingleton<UnifiedGenerationEngine>(sp =>
{
    var modelService = sp.GetRequiredService<HuggingFaceModelService>();
    var gpuService = sp.GetRequiredService<LocalGpuService>();
    var comfyService = sp.GetRequiredService<ComfyUIService>();
    var gpuPoolService = sp.GetRequiredService<GpuPoolService>();
    var logger = sp.GetRequiredService<ILogger<UnifiedGenerationEngine>>();
    return new UnifiedGenerationEngine(modelService, gpuService, comfyService, gpuPoolService, logger);
});

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

// Rate Limiting Middleware (before auth to prevent DOS)
app.UseRateLimiting();

// API Key Authentication Middleware
app.UseApiKeyAuth();

app.UseAuthorization();
app.MapControllers();

// SignalR Hubs
app.MapHub<AIManagerHub>("/hub/aimanager");
app.MapHub<GpuPoolHub>("/hub/gpupool");

// Start Local File Server (serves local media as temporary URLs)
var fileServer = app.Services.GetRequiredService<LocalFileServerService>();
_ = fileServer.StartAsync();

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
