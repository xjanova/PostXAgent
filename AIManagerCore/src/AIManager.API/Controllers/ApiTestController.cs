using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;
using AIManager.Core.Models;
using System.Reflection;

namespace AIManager.API.Controllers;

/// <summary>
/// API Test Controller - ทดสอบทุก API endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ApiTestController : ControllerBase
{
    private readonly ILogger<ApiTestController> _logger;
    private readonly ContentGeneratorService _contentGenerator;
    private readonly ViralAnalysisService _viralAnalysis;
    private readonly CommentMonitorService _commentMonitor;
    private readonly CommentReplyService _commentReply;
    private readonly TonePersonalityService _toneService;
    private readonly SeekAndPostService _seekAndPost;
    private readonly GroupSearchService _groupSearch;

    public ApiTestController(
        ILogger<ApiTestController> logger,
        ContentGeneratorService contentGenerator,
        ViralAnalysisService viralAnalysis,
        CommentMonitorService commentMonitor,
        CommentReplyService commentReply,
        TonePersonalityService toneService,
        SeekAndPostService seekAndPost,
        GroupSearchService groupSearch)
    {
        _logger = logger;
        _contentGenerator = contentGenerator;
        _viralAnalysis = viralAnalysis;
        _commentMonitor = commentMonitor;
        _commentReply = commentReply;
        _toneService = toneService;
        _seekAndPost = seekAndPost;
        _groupSearch = groupSearch;
    }

    /// <summary>
    /// Get HTML test page
    /// </summary>
    [HttpGet("page")]
    [Produces("text/html")]
    public ContentResult GetTestPage()
    {
        var html = GetTestPageHtml();
        return Content(html, "text/html");
    }

    /// <summary>
    /// Health check for all services
    /// </summary>
    [HttpGet("health")]
    public ActionResult<object> HealthCheck()
    {
        var services = new Dictionary<string, object>
        {
            ["api_status"] = "online",
            ["timestamp"] = DateTime.UtcNow,
            ["services"] = new Dictionary<string, bool>
            {
                ["ContentGeneratorService"] = _contentGenerator != null,
                ["ViralAnalysisService"] = _viralAnalysis != null,
                ["CommentMonitorService"] = _commentMonitor != null,
                ["CommentReplyService"] = _commentReply != null,
                ["TonePersonalityService"] = _toneService != null,
                ["SeekAndPostService"] = _seekAndPost != null,
                ["GroupSearchService"] = _groupSearch != null
            }
        };

        return Ok(services);
    }

    /// <summary>
    /// List all available API endpoints
    /// </summary>
    [HttpGet("endpoints")]
    public ActionResult<object> ListEndpoints()
    {
        var endpoints = new List<object>
        {
            // Comment Management
            new { controller = "CommentManagement", method = "POST", path = "/api/CommentManagement/fetch", description = "ดึง comments จาก platform" },
            new { controller = "CommentManagement", method = "POST", path = "/api/CommentManagement/generate-reply", description = "สร้าง reply ด้วย AI" },
            new { controller = "CommentManagement", method = "POST", path = "/api/CommentManagement/post-reply", description = "โพสต์ reply" },
            new { controller = "CommentManagement", method = "POST", path = "/api/CommentManagement/auto-reply", description = "auto-reply หลาย comments" },
            new { controller = "CommentManagement", method = "POST", path = "/api/CommentManagement/analyze-sentiment", description = "วิเคราะห์ sentiment" },
            new { controller = "CommentManagement", method = "GET", path = "/api/CommentManagement/tones/presets", description = "tone presets" },

            // Group Posting
            new { controller = "GroupPosting", method = "POST", path = "/api/GroupPosting/groups/search", description = "ค้นหากลุ่ม" },
            new { controller = "GroupPosting", method = "POST", path = "/api/GroupPosting/groups/recommend", description = "แนะนำกลุ่ม" },
            new { controller = "GroupPosting", method = "POST", path = "/api/GroupPosting/post", description = "โพสต์ไปยังกลุ่ม" },
            new { controller = "GroupPosting", method = "POST", path = "/api/GroupPosting/post/batch", description = "โพสต์หลายกลุ่ม" },
            new { controller = "GroupPosting", method = "POST", path = "/api/GroupPosting/loop/start", description = "เริ่ม posting loop" },
            new { controller = "GroupPosting", method = "POST", path = "/api/GroupPosting/loop/stop/{taskId}", description = "หยุด posting loop" },
            new { controller = "GroupPosting", method = "GET", path = "/api/GroupPosting/loop/active", description = "ดู active loops" },
            new { controller = "GroupPosting", method = "POST", path = "/api/GroupPosting/content/generate", description = "สร้างเนื้อหา viral" },

            // Viral Analysis
            new { controller = "ViralAnalysisApi", method = "POST", path = "/api/ViralAnalysisApi/analyze", description = "วิเคราะห์ viral potential" },
            new { controller = "ViralAnalysisApi", method = "GET", path = "/api/ViralAnalysisApi/keywords/trending", description = "trending keywords" },
            new { controller = "ViralAnalysisApi", method = "POST", path = "/api/ViralAnalysisApi/keywords/track", description = "track keyword" },
            new { controller = "ViralAnalysisApi", method = "POST", path = "/api/ViralAnalysisApi/keywords/update", description = "update keyword metrics" },
            new { controller = "ViralAnalysisApi", method = "GET", path = "/api/ViralAnalysisApi/optimal-times", description = "เวลาโพสต์ที่เหมาะสม" },
            new { controller = "ViralAnalysisApi", method = "POST", path = "/api/ViralAnalysisApi/analyze-emotions", description = "วิเคราะห์ emotional triggers" },

            // API Test
            new { controller = "ApiTest", method = "GET", path = "/api/ApiTest/page", description = "หน้าทดสอบ API" },
            new { controller = "ApiTest", method = "GET", path = "/api/ApiTest/health", description = "Health check" },
            new { controller = "ApiTest", method = "GET", path = "/api/ApiTest/endpoints", description = "List all endpoints" }
        };

        return Ok(new { count = endpoints.Count, endpoints });
    }

    /// <summary>
    /// Test content generation
    /// </summary>
    [HttpPost("test/content-generate")]
    public async Task<ActionResult<object>> TestContentGeneration([FromBody] TestContentRequest request)
    {
        try
        {
            var result = await _contentGenerator.GenerateAsync(
                request.Prompt ?? "สร้างโพสต์โปรโมทสินค้าสำหรับ Facebook",
                null,
                request.Platform ?? "facebook",
                "th",
                default);

            return Ok(new
            {
                success = true,
                data = new
                {
                    text = result?.Text,
                    hashtags = result?.Hashtags,
                    provider = result?.Provider
                }
            });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test viral analysis
    /// </summary>
    [HttpPost("test/viral-analyze")]
    public async Task<ActionResult<object>> TestViralAnalysis([FromBody] TestViralRequest request)
    {
        try
        {
            var result = await _viralAnalysis.PredictViralPotentialAsync(
                request.Content ?? "ทดสอบเนื้อหา viral",
                request.Platform ?? "facebook",
                request.Hashtags ?? new List<string>());

            return Ok(new
            {
                success = true,
                data = new
                {
                    score = result.Score,
                    factors = result.Factors,
                    recommendations = result.Recommendations
                }
            });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test sentiment analysis
    /// </summary>
    [HttpPost("test/sentiment")]
    public async Task<ActionResult<object>> TestSentiment([FromBody] TestSentimentRequest request)
    {
        try
        {
            var result = await _commentMonitor.AnalyzeSentimentAsync(
                request.Content ?? "สินค้าดีมากครับ ชอบมาก",
                request.Context ?? "");

            return Ok(new
            {
                success = true,
                data = new
                {
                    sentiment = result.Sentiment,
                    score = result.Score,
                    isQuestion = result.IsQuestion,
                    priority = result.Priority
                }
            });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test reply generation
    /// </summary>
    [HttpPost("test/reply-generate")]
    public async Task<ActionResult<object>> TestReplyGeneration([FromBody] TestReplyRequest request)
    {
        try
        {
            var toneConfig = _toneService.GetPreset(request.Tone ?? "friendly")?.Configuration
                ?? new ToneConfiguration();

            var replyRequest = new ReplyGenerationRequest
            {
                Comment = new SocialComment
                {
                    ContentText = request.CommentContent ?? "สินค้านี้ราคาเท่าไหร่ครับ",
                    AuthorName = request.AuthorName ?? "ลูกค้า"
                },
                Tone = toneConfig,
                PostContent = request.PostContent,
                Platform = request.Platform ?? "facebook"
            };

            var result = await _commentReply.GenerateReplyAsync(replyRequest);

            return Ok(new
            {
                success = true,
                data = new
                {
                    replyContent = result.Content,
                    tone = request.Tone ?? "friendly"
                }
            });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test emotional triggers analysis
    /// </summary>
    [HttpPost("test/emotions")]
    public async Task<ActionResult<object>> TestEmotions([FromBody] TestEmotionsRequest request)
    {
        try
        {
            var result = await _viralAnalysis.AnalyzeEmotionalTriggersAsync(
                request.Content ?? "โปรโมชั่นพิเศษ! ลดราคา 50% เฉพาะวันนี้เท่านั้น รีบด่วน!");

            return Ok(new
            {
                success = true,
                data = new
                {
                    emotionalScore = result.Score,
                    triggers = result.Triggers,
                    recommendations = result.Recommendations
                }
            });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get tone presets
    /// </summary>
    [HttpGet("test/tone-presets")]
    public ActionResult<object> GetTonePresets()
    {
        try
        {
            var presets = _toneService.GetPresets();
            return Ok(new
            {
                success = true,
                data = presets
            });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get trending keywords
    /// </summary>
    [HttpGet("test/trending")]
    public async Task<ActionResult<object>> GetTrending([FromQuery] string? platform = null)
    {
        try
        {
            var keywords = await _viralAnalysis.GetTrendingKeywordsAsync(platform, 20);
            return Ok(new
            {
                success = true,
                data = keywords
            });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get optimal posting times
    /// </summary>
    [HttpGet("test/optimal-times")]
    public ActionResult<object> GetOptimalTimes([FromQuery] string platform = "facebook")
    {
        try
        {
            var times = _viralAnalysis.GetOptimalPostingTimesAsync(platform);
            return Ok(new
            {
                success = true,
                data = times
            });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    private string GetTestPageHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""th"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>AI Manager API Test Page</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: 'Segoe UI', Tahoma, sans-serif;
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
            min-height: 100vh;
            color: #e0e0e0;
        }
        .container { max-width: 1400px; margin: 0 auto; padding: 20px; }
        h1 {
            text-align: center;
            margin-bottom: 30px;
            color: #00d4ff;
            font-size: 2.5rem;
            text-shadow: 0 0 20px rgba(0,212,255,0.5);
        }
        .status-bar {
            background: rgba(0,212,255,0.1);
            border: 1px solid #00d4ff;
            border-radius: 10px;
            padding: 15px;
            margin-bottom: 30px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .status-item { display: flex; align-items: center; gap: 10px; }
        .status-dot { width: 12px; height: 12px; border-radius: 50%; }
        .status-dot.online { background: #00ff88; box-shadow: 0 0 10px #00ff88; }
        .status-dot.offline { background: #ff4444; box-shadow: 0 0 10px #ff4444; }
        .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(400px, 1fr)); gap: 20px; }
        .card {
            background: rgba(255,255,255,0.05);
            border: 1px solid rgba(255,255,255,0.1);
            border-radius: 15px;
            padding: 20px;
            backdrop-filter: blur(10px);
            transition: transform 0.3s, box-shadow 0.3s;
        }
        .card:hover {
            transform: translateY(-5px);
            box-shadow: 0 10px 30px rgba(0,212,255,0.2);
        }
        .card h3 {
            color: #00d4ff;
            margin-bottom: 15px;
            display: flex;
            align-items: center;
            gap: 10px;
            font-size: 1.2rem;
        }
        .card h3 .icon { font-size: 1.5rem; }
        .form-group { margin-bottom: 15px; }
        label { display: block; margin-bottom: 5px; color: #a0a0a0; font-size: 0.9rem; }
        input, textarea, select {
            width: 100%;
            padding: 12px;
            border: 1px solid rgba(255,255,255,0.2);
            border-radius: 8px;
            background: rgba(0,0,0,0.3);
            color: #fff;
            font-size: 14px;
            transition: border-color 0.3s;
        }
        input:focus, textarea:focus, select:focus {
            outline: none;
            border-color: #00d4ff;
            box-shadow: 0 0 10px rgba(0,212,255,0.3);
        }
        textarea { min-height: 80px; resize: vertical; }
        button {
            background: linear-gradient(135deg, #00d4ff 0%, #0099cc 100%);
            color: #000;
            border: none;
            padding: 12px 24px;
            border-radius: 8px;
            cursor: pointer;
            font-weight: bold;
            font-size: 14px;
            transition: all 0.3s;
            width: 100%;
        }
        button:hover {
            transform: scale(1.02);
            box-shadow: 0 5px 20px rgba(0,212,255,0.4);
        }
        button:disabled {
            background: #555;
            cursor: not-allowed;
            transform: none;
        }
        .result {
            margin-top: 15px;
            padding: 15px;
            border-radius: 8px;
            background: rgba(0,0,0,0.3);
            border: 1px solid rgba(255,255,255,0.1);
            max-height: 300px;
            overflow-y: auto;
            font-family: 'Consolas', monospace;
            font-size: 12px;
            white-space: pre-wrap;
            word-break: break-all;
        }
        .result.success { border-color: #00ff88; }
        .result.error { border-color: #ff4444; }
        .loading {
            display: none;
            color: #00d4ff;
            font-size: 0.9rem;
            margin-top: 10px;
        }
        .loading.active { display: block; }
        .endpoints-list {
            max-height: 400px;
            overflow-y: auto;
        }
        .endpoint-item {
            padding: 10px;
            margin-bottom: 5px;
            background: rgba(0,0,0,0.2);
            border-radius: 5px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            font-size: 0.85rem;
        }
        .method {
            padding: 3px 8px;
            border-radius: 4px;
            font-size: 0.7rem;
            font-weight: bold;
        }
        .method.GET { background: #00ff88; color: #000; }
        .method.POST { background: #ff9900; color: #000; }
        .tabs {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }
        .tab {
            padding: 10px 20px;
            background: rgba(255,255,255,0.1);
            border: 1px solid rgba(255,255,255,0.2);
            border-radius: 8px;
            cursor: pointer;
            transition: all 0.3s;
        }
        .tab:hover, .tab.active {
            background: rgba(0,212,255,0.2);
            border-color: #00d4ff;
        }
        .tab-content { display: none; }
        .tab-content.active { display: block; }
    </style>
</head>
<body>
    <div class=""container"">
        <h1>🤖 AI Manager API Test</h1>

        <div class=""status-bar"">
            <div class=""status-item"">
                <span class=""status-dot online"" id=""apiStatus""></span>
                <span>API Status: <strong id=""apiStatusText"">Online</strong></span>
            </div>
            <div class=""status-item"">
                <span>Last Check: <span id=""lastCheck"">-</span></span>
            </div>
            <button onclick=""checkHealth()"" style=""width: auto; padding: 8px 16px;"">🔄 Refresh</button>
        </div>

        <div class=""tabs"">
            <div class=""tab active"" onclick=""showTab('content')"">📝 Content Generation</div>
            <div class=""tab"" onclick=""showTab('viral')"">📊 Viral Analysis</div>
            <div class=""tab"" onclick=""showTab('comments')"">💬 Comments</div>
            <div class=""tab"" onclick=""showTab('groups')"">👥 Group Posting</div>
            <div class=""tab"" onclick=""showTab('endpoints')"">📋 All Endpoints</div>
        </div>

        <!-- Content Generation Tab -->
        <div class=""tab-content active"" id=""content-tab"">
            <div class=""grid"">
                <div class=""card"">
                    <h3><span class=""icon"">✨</span> Generate Content</h3>
                    <div class=""form-group"">
                        <label>Prompt</label>
                        <textarea id=""contentPrompt"" placeholder=""สร้างโพสต์โปรโมทสินค้าสำหรับ Facebook"">สร้างโพสต์โปรโมทสินค้าใหม่ ลดราคา 50%</textarea>
                    </div>
                    <div class=""form-group"">
                        <label>Platform</label>
                        <select id=""contentPlatform"">
                            <option value=""facebook"">Facebook</option>
                            <option value=""instagram"">Instagram</option>
                            <option value=""twitter"">Twitter</option>
                            <option value=""tiktok"">TikTok</option>
                            <option value=""line"">LINE</option>
                        </select>
                    </div>
                    <button onclick=""testContentGenerate()"" id=""btnContentGen"">🚀 Generate</button>
                    <div class=""loading"" id=""loadingContent"">⏳ กำลังสร้างเนื้อหา...</div>
                    <div class=""result"" id=""resultContent""></div>
                </div>

                <div class=""card"">
                    <h3><span class=""icon"">🎭</span> Emotional Analysis</h3>
                    <div class=""form-group"">
                        <label>Content to Analyze</label>
                        <textarea id=""emotionContent"" placeholder=""ข้อความที่ต้องการวิเคราะห์"">โปรโมชั่นพิเศษ! ลดราคา 50% เฉพาะวันนี้เท่านั้น รีบด่วน!</textarea>
                    </div>
                    <button onclick=""testEmotions()"" id=""btnEmotions"">🎯 Analyze Emotions</button>
                    <div class=""loading"" id=""loadingEmotions"">⏳ กำลังวิเคราะห์...</div>
                    <div class=""result"" id=""resultEmotions""></div>
                </div>
            </div>
        </div>

        <!-- Viral Analysis Tab -->
        <div class=""tab-content"" id=""viral-tab"">
            <div class=""grid"">
                <div class=""card"">
                    <h3><span class=""icon"">📊</span> Viral Potential Analysis</h3>
                    <div class=""form-group"">
                        <label>Content</label>
                        <textarea id=""viralContent"" placeholder=""เนื้อหาที่ต้องการวิเคราะห์"">สินค้าใหม่มาแล้ว! ดีที่สุดในปี 2024</textarea>
                    </div>
                    <div class=""form-group"">
                        <label>Platform</label>
                        <select id=""viralPlatform"">
                            <option value=""facebook"">Facebook</option>
                            <option value=""instagram"">Instagram</option>
                            <option value=""twitter"">Twitter</option>
                            <option value=""tiktok"">TikTok</option>
                        </select>
                    </div>
                    <div class=""form-group"">
                        <label>Hashtags (comma separated)</label>
                        <input id=""viralHashtags"" placeholder=""#ลดราคา, #โปรโมชั่น"" value=""#สินค้าใหม่,#ดีที่สุด"">
                    </div>
                    <button onclick=""testViralAnalyze()"" id=""btnViral"">📈 Analyze</button>
                    <div class=""loading"" id=""loadingViral"">⏳ กำลังวิเคราะห์...</div>
                    <div class=""result"" id=""resultViral""></div>
                </div>

                <div class=""card"">
                    <h3><span class=""icon"">🔥</span> Trending Keywords</h3>
                    <div class=""form-group"">
                        <label>Platform (optional)</label>
                        <select id=""trendingPlatform"">
                            <option value="""">All Platforms</option>
                            <option value=""facebook"">Facebook</option>
                            <option value=""instagram"">Instagram</option>
                            <option value=""twitter"">Twitter</option>
                            <option value=""tiktok"">TikTok</option>
                        </select>
                    </div>
                    <button onclick=""getTrending()"" id=""btnTrending"">📊 Get Trending</button>
                    <div class=""loading"" id=""loadingTrending"">⏳ กำลังดึงข้อมูล...</div>
                    <div class=""result"" id=""resultTrending""></div>
                </div>

                <div class=""card"">
                    <h3><span class=""icon"">⏰</span> Optimal Posting Times</h3>
                    <div class=""form-group"">
                        <label>Platform</label>
                        <select id=""optimalPlatform"">
                            <option value=""facebook"">Facebook</option>
                            <option value=""instagram"">Instagram</option>
                            <option value=""twitter"">Twitter</option>
                            <option value=""tiktok"">TikTok</option>
                            <option value=""line"">LINE</option>
                        </select>
                    </div>
                    <button onclick=""getOptimalTimes()"" id=""btnOptimal"">🕐 Get Times</button>
                    <div class=""loading"" id=""loadingOptimal"">⏳ กำลังดึงข้อมูล...</div>
                    <div class=""result"" id=""resultOptimal""></div>
                </div>
            </div>
        </div>

        <!-- Comments Tab -->
        <div class=""tab-content"" id=""comments-tab"">
            <div class=""grid"">
                <div class=""card"">
                    <h3><span class=""icon"">🎨</span> Tone Presets</h3>
                    <button onclick=""getTonePresets()"" id=""btnTones"">📋 Get Presets</button>
                    <div class=""loading"" id=""loadingTones"">⏳ กำลังดึงข้อมูล...</div>
                    <div class=""result"" id=""resultTones""></div>
                </div>

                <div class=""card"">
                    <h3><span class=""icon"">💬</span> Sentiment Analysis</h3>
                    <div class=""form-group"">
                        <label>Comment Content</label>
                        <textarea id=""sentimentContent"" placeholder=""ข้อความที่ต้องการวิเคราะห์"">สินค้าดีมากครับ ชอบมาก ส่งเร็วด้วย</textarea>
                    </div>
                    <div class=""form-group"">
                        <label>Context (optional)</label>
                        <input id=""sentimentContext"" placeholder=""บริบทของโพสต์"" value=""โพสต์ขายสินค้า"">
                    </div>
                    <button onclick=""testSentiment()"" id=""btnSentiment"">🔍 Analyze</button>
                    <div class=""loading"" id=""loadingSentiment"">⏳ กำลังวิเคราะห์...</div>
                    <div class=""result"" id=""resultSentiment""></div>
                </div>

                <div class=""card"">
                    <h3><span class=""icon"">✍️</span> Generate Reply</h3>
                    <div class=""form-group"">
                        <label>Comment</label>
                        <textarea id=""replyComment"" placeholder=""ข้อความจากลูกค้า"">สินค้านี้ราคาเท่าไหร่ครับ</textarea>
                    </div>
                    <div class=""form-group"">
                        <label>Author Name</label>
                        <input id=""replyAuthor"" placeholder=""ชื่อลูกค้า"" value=""คุณสมชาย"">
                    </div>
                    <div class=""form-group"">
                        <label>Tone</label>
                        <select id=""replyTone"">
                            <option value=""friendly"">Friendly (เป็นมิตร)</option>
                            <option value=""professional"">Professional (มืออาชีพ)</option>
                            <option value=""casual"">Casual (สบายๆ)</option>
                            <option value=""humorous"">Humorous (ตลก)</option>
                        </select>
                    </div>
                    <div class=""form-group"">
                        <label>Platform</label>
                        <select id=""replyPlatform"">
                            <option value=""facebook"">Facebook</option>
                            <option value=""instagram"">Instagram</option>
                            <option value=""twitter"">Twitter</option>
                        </select>
                    </div>
                    <button onclick=""testReplyGenerate()"" id=""btnReply"">💬 Generate Reply</button>
                    <div class=""loading"" id=""loadingReply"">⏳ กำลังสร้างคำตอบ...</div>
                    <div class=""result"" id=""resultReply""></div>
                </div>
            </div>
        </div>

        <!-- Groups Tab -->
        <div class=""tab-content"" id=""groups-tab"">
            <div class=""grid"">
                <div class=""card"">
                    <h3><span class=""icon"">🔍</span> Search Groups</h3>
                    <p style=""color: #a0a0a0; margin-bottom: 15px; font-size: 0.9rem;"">
                        ⚠️ ต้องมี access token และ credentials จริงในการทดสอบ<br>
                        หน้านี้แสดง API structure เท่านั้น
                    </p>
                    <div class=""form-group"">
                        <label>Keywords (comma separated)</label>
                        <input id=""groupKeywords"" placeholder=""ขายของ, ซื้อขาย"" value=""ขายของออนไลน์,ธุรกิจ"">
                    </div>
                    <div class=""form-group"">
                        <label>Platform</label>
                        <select id=""groupPlatform"">
                            <option value=""facebook"">Facebook</option>
                            <option value=""line"">LINE</option>
                        </select>
                    </div>
                    <div class=""result"" style=""margin-top: 0;"">
API Endpoint: POST /api/GroupPosting/groups/search
Request Body:
{
  ""platform"": ""facebook"",
  ""keywords"": [""ขายของออนไลน์"", ""ธุรกิจ""],
  ""limit"": 20,
  ""credentials"": {
    ""accessToken"": ""YOUR_ACCESS_TOKEN""
  }
}</div>
                </div>

                <div class=""card"">
                    <h3><span class=""icon"">📮</span> Post to Group</h3>
                    <div class=""result"" style=""margin-top: 0;"">
API Endpoint: POST /api/GroupPosting/post
Request Body:
{
  ""platform"": ""facebook"",
  ""groupId"": ""GROUP_ID"",
  ""content"": {
    ""text"": ""ข้อความที่ต้องการโพสต์"",
    ""images"": [],
    ""link"": null
  },
  ""credentials"": {
    ""accessToken"": ""YOUR_ACCESS_TOKEN"",
    ""pageId"": ""PAGE_ID""
  }
}</div>
                </div>

                <div class=""card"">
                    <h3><span class=""icon"">🔄</span> Start Posting Loop</h3>
                    <div class=""result"" style=""margin-top: 0;"">
API Endpoint: POST /api/GroupPosting/loop/start
Request Body:
{
  ""platform"": ""facebook"",
  ""groupIds"": [""group1"", ""group2""],
  ""contentTemplates"": [""template1""],
  ""intervalMinutes"": 60,
  ""maxPostsPerCycle"": 5,
  ""credentials"": {
    ""accessToken"": ""YOUR_ACCESS_TOKEN""
  }
}</div>
                </div>

                <div class=""card"">
                    <h3><span class=""icon"">⏹️</span> Active Loops</h3>
                    <button onclick=""getActiveLoops()"" id=""btnLoops"">📋 Get Active Loops</button>
                    <div class=""loading"" id=""loadingLoops"">⏳ กำลังดึงข้อมูล...</div>
                    <div class=""result"" id=""resultLoops""></div>
                </div>
            </div>
        </div>

        <!-- Endpoints Tab -->
        <div class=""tab-content"" id=""endpoints-tab"">
            <div class=""card"">
                <h3><span class=""icon"">📋</span> Available API Endpoints</h3>
                <button onclick=""loadEndpoints()"" id=""btnEndpoints"" style=""margin-bottom: 15px;"">🔄 Refresh List</button>
                <div class=""loading"" id=""loadingEndpoints"">⏳ กำลังโหลด...</div>
                <div class=""endpoints-list"" id=""endpointsList""></div>
            </div>
        </div>
    </div>

    <script>
        const API_BASE = window.location.origin;

        async function apiCall(endpoint, method = 'GET', body = null) {
            const options = {
                method,
                headers: { 'Content-Type': 'application/json' }
            };
            if (body) options.body = JSON.stringify(body);

            const response = await fetch(`${API_BASE}${endpoint}`, options);
            return response.json();
        }

        function showResult(elementId, data, isSuccess) {
            const el = document.getElementById(elementId);
            el.textContent = JSON.stringify(data, null, 2);
            el.className = 'result ' + (isSuccess ? 'success' : 'error');
        }

        function setLoading(id, isLoading) {
            document.getElementById(id).classList.toggle('active', isLoading);
        }

        function showTab(tabName) {
            document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
            document.querySelectorAll('.tab-content').forEach(t => t.classList.remove('active'));
            event.target.classList.add('active');
            document.getElementById(tabName + '-tab').classList.add('active');
        }

        async function checkHealth() {
            try {
                const data = await apiCall('/api/ApiTest/health');
                document.getElementById('apiStatus').className = 'status-dot online';
                document.getElementById('apiStatusText').textContent = 'Online';
                document.getElementById('lastCheck').textContent = new Date().toLocaleTimeString('th-TH');
            } catch (e) {
                document.getElementById('apiStatus').className = 'status-dot offline';
                document.getElementById('apiStatusText').textContent = 'Offline';
            }
        }

        async function testContentGenerate() {
            setLoading('loadingContent', true);
            try {
                const data = await apiCall('/api/ApiTest/test/content-generate', 'POST', {
                    prompt: document.getElementById('contentPrompt').value,
                    platform: document.getElementById('contentPlatform').value
                });
                showResult('resultContent', data, data.success);
            } catch (e) {
                showResult('resultContent', { error: e.message }, false);
            }
            setLoading('loadingContent', false);
        }

        async function testEmotions() {
            setLoading('loadingEmotions', true);
            try {
                const data = await apiCall('/api/ApiTest/test/emotions', 'POST', {
                    content: document.getElementById('emotionContent').value
                });
                showResult('resultEmotions', data, data.success);
            } catch (e) {
                showResult('resultEmotions', { error: e.message }, false);
            }
            setLoading('loadingEmotions', false);
        }

        async function testViralAnalyze() {
            setLoading('loadingViral', true);
            try {
                const hashtags = document.getElementById('viralHashtags').value
                    .split(',').map(h => h.trim()).filter(h => h);
                const data = await apiCall('/api/ApiTest/test/viral-analyze', 'POST', {
                    content: document.getElementById('viralContent').value,
                    platform: document.getElementById('viralPlatform').value,
                    hashtags
                });
                showResult('resultViral', data, data.success);
            } catch (e) {
                showResult('resultViral', { error: e.message }, false);
            }
            setLoading('loadingViral', false);
        }

        async function getTrending() {
            setLoading('loadingTrending', true);
            try {
                const platform = document.getElementById('trendingPlatform').value;
                const url = platform ? `/api/ApiTest/test/trending?platform=${platform}` : '/api/ApiTest/test/trending';
                const data = await apiCall(url);
                showResult('resultTrending', data, data.success);
            } catch (e) {
                showResult('resultTrending', { error: e.message }, false);
            }
            setLoading('loadingTrending', false);
        }

        async function getOptimalTimes() {
            setLoading('loadingOptimal', true);
            try {
                const platform = document.getElementById('optimalPlatform').value;
                const data = await apiCall(`/api/ApiTest/test/optimal-times?platform=${platform}`);
                showResult('resultOptimal', data, data.success);
            } catch (e) {
                showResult('resultOptimal', { error: e.message }, false);
            }
            setLoading('loadingOptimal', false);
        }

        async function getTonePresets() {
            setLoading('loadingTones', true);
            try {
                const data = await apiCall('/api/ApiTest/test/tone-presets');
                showResult('resultTones', data, data.success);
            } catch (e) {
                showResult('resultTones', { error: e.message }, false);
            }
            setLoading('loadingTones', false);
        }

        async function testSentiment() {
            setLoading('loadingSentiment', true);
            try {
                const data = await apiCall('/api/ApiTest/test/sentiment', 'POST', {
                    content: document.getElementById('sentimentContent').value,
                    context: document.getElementById('sentimentContext').value
                });
                showResult('resultSentiment', data, data.success);
            } catch (e) {
                showResult('resultSentiment', { error: e.message }, false);
            }
            setLoading('loadingSentiment', false);
        }

        async function testReplyGenerate() {
            setLoading('loadingReply', true);
            try {
                const data = await apiCall('/api/ApiTest/test/reply-generate', 'POST', {
                    commentContent: document.getElementById('replyComment').value,
                    authorName: document.getElementById('replyAuthor').value,
                    tone: document.getElementById('replyTone').value,
                    platform: document.getElementById('replyPlatform').value
                });
                showResult('resultReply', data, data.success);
            } catch (e) {
                showResult('resultReply', { error: e.message }, false);
            }
            setLoading('loadingReply', false);
        }

        async function getActiveLoops() {
            setLoading('loadingLoops', true);
            try {
                const data = await apiCall('/api/GroupPosting/loop/active');
                showResult('resultLoops', data, data.success !== false);
            } catch (e) {
                showResult('resultLoops', { error: e.message }, false);
            }
            setLoading('loadingLoops', false);
        }

        async function loadEndpoints() {
            setLoading('loadingEndpoints', true);
            try {
                const data = await apiCall('/api/ApiTest/endpoints');
                const list = document.getElementById('endpointsList');
                list.innerHTML = data.endpoints.map(e => `
                    <div class=""endpoint-item"">
                        <div>
                            <span class=""method ${e.method}"">${e.method}</span>
                            <code style=""margin-left: 10px;"">${e.path}</code>
                        </div>
                        <span style=""color: #a0a0a0; font-size: 0.8rem;"">${e.description}</span>
                    </div>
                `).join('');
            } catch (e) {
                document.getElementById('endpointsList').innerHTML = '<div style=""color: #ff4444;"">Error loading endpoints</div>';
            }
            setLoading('loadingEndpoints', false);
        }

        // Initialize
        checkHealth();
        loadEndpoints();
    </script>
</body>
</html>";
    }
}

// Request models for test endpoints
public record TestContentRequest
{
    public string? Prompt { get; init; }
    public string? Platform { get; init; }
}

public record TestViralRequest
{
    public string? Content { get; init; }
    public string? Platform { get; init; }
    public List<string>? Hashtags { get; init; }
}

public record TestSentimentRequest
{
    public string? Content { get; init; }
    public string? Context { get; init; }
}

public record TestReplyRequest
{
    public string? CommentContent { get; init; }
    public string? AuthorName { get; init; }
    public string? Tone { get; init; }
    public string? Platform { get; init; }
    public string? PostContent { get; init; }
}

public record TestEmotionsRequest
{
    public string? Content { get; init; }
}
