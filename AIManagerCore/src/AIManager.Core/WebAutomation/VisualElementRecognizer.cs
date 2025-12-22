using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Text.RegularExpressions;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// Visual Element Recognizer - ใช้ Computer Vision และ Pattern Recognition
/// เพื่อค้นหาและจดจำ Elements บนหน้าเว็บ แม้เมื่อ DOM เปลี่ยนไป
/// </summary>
public class VisualElementRecognizer
{
    private readonly ILogger<VisualElementRecognizer> _logger;
    private readonly Dictionary<string, VisualPattern> _learnedPatterns = new();
    private readonly Dictionary<string, ElementFeatures> _elementCache = new();

    // Feature weights สำหรับ element matching
    private static readonly Dictionary<string, double> FeatureWeights = new()
    {
        ["position"] = 0.15,
        ["size"] = 0.10,
        ["color"] = 0.10,
        ["shape"] = 0.15,
        ["text"] = 0.20,
        ["context"] = 0.15,
        ["semantic"] = 0.15
    };

    public VisualElementRecognizer(ILogger<VisualElementRecognizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// สกัด Visual Features จาก Element
    /// </summary>
    public ElementFeatures ExtractFeatures(RecordedElement element, string? screenshot = null)
    {
        var features = new ElementFeatures
        {
            ElementId = element.Id ?? Guid.NewGuid().ToString(),
            TagName = element.TagName,
            ExtractedAt = DateTime.UtcNow
        };

        // Position features
        if (element.Position != null)
        {
            features.RelativePosition = CalculateRelativePosition(element.Position);
            features.PositionQuadrant = DetermineQuadrant(element.Position);
        }

        // Size features
        if (element.Size != null)
        {
            features.SizeCategory = CategorizeSizeInternal(element.Size);
            features.AspectRatio = element.Size.Width > 0
                ? element.Size.Height / element.Size.Width
                : 0;
        }

        // Text features
        if (!string.IsNullOrEmpty(element.TextContent))
        {
            features.TextFeatures = ExtractTextFeatures(element.TextContent);
            features.TextLength = element.TextContent.Length;
            features.HasEmoji = ContainsEmoji(element.TextContent);
        }

        // Semantic features
        features.SemanticType = DetermineSemanticType(element);
        features.Purpose = InferPurpose(element);

        // Visual context
        features.VisualContext = new VisualContext
        {
            HasIcon = HasIcon(element),
            HasImage = HasImage(element),
            IsClickable = IsClickable(element),
            IsInput = IsInputElement(element),
            BorderRadius = GetBorderRadius(element),
            EstimatedColor = EstimateColor(element)
        };

        // Neighbor features
        features.NeighborSignature = CalculateNeighborSignature(element);

        return features;
    }

    /// <summary>
    /// ค้นหา Element ที่คล้ายกันที่สุดจาก Feature Matching
    /// </summary>
    public async Task<MatchResult?> FindSimilarElementAsync(
        ElementFeatures targetFeatures,
        List<ElementFeatures> candidates,
        double threshold = 0.7,
        CancellationToken ct = default)
    {
        MatchResult? bestMatch = null;
        var bestScore = 0.0;

        await Task.Run(() =>
        {
            foreach (var candidate in candidates)
            {
                ct.ThrowIfCancellationRequested();

                var score = CalculateSimilarityScore(targetFeatures, candidate);

                if (score > bestScore && score >= threshold)
                {
                    bestScore = score;
                    bestMatch = new MatchResult
                    {
                        MatchedElement = candidate,
                        SimilarityScore = score,
                        MatchedFeatures = GetMatchedFeatures(targetFeatures, candidate),
                        Confidence = CalculateConfidence(score, targetFeatures, candidate)
                    };
                }
            }
        }, ct);

        if (bestMatch != null)
        {
            _logger.LogDebug(
                "Found similar element with score {Score:F3}, confidence {Confidence:F3}",
                bestScore, bestMatch.Confidence);
        }

        return bestMatch;
    }

    /// <summary>
    /// เรียนรู้ Pattern ใหม่จาก Element
    /// </summary>
    public void LearnPattern(string platform, string purpose, ElementFeatures features)
    {
        var key = $"{platform}:{purpose}";

        if (!_learnedPatterns.ContainsKey(key))
        {
            _learnedPatterns[key] = new VisualPattern
            {
                Platform = platform,
                Purpose = purpose,
                Samples = new List<ElementFeatures>()
            };
        }

        var pattern = _learnedPatterns[key];
        pattern.Samples.Add(features);

        // Update aggregated features
        UpdatePatternAggregates(pattern);

        _logger.LogInformation(
            "Learned pattern for {Platform}:{Purpose}, total samples: {Count}",
            platform, purpose, pattern.Samples.Count);
    }

    /// <summary>
    /// ค้นหา Element จาก Learned Pattern
    /// </summary>
    public async Task<ElementSelector?> FindByPatternAsync(
        string platform,
        string purpose,
        string pageHtml,
        string? screenshot = null,
        CancellationToken ct = default)
    {
        var key = $"{platform}:{purpose}";

        if (!_learnedPatterns.TryGetValue(key, out var pattern) || pattern.Samples.Count == 0)
        {
            _logger.LogWarning("No learned pattern found for {Key}", key);
            return null;
        }

        // Extract candidates from HTML
        var candidates = await ExtractCandidatesFromHtmlAsync(pageHtml, purpose, ct);

        if (candidates.Count == 0)
        {
            return null;
        }

        // Find best match using aggregated pattern
        var targetFeatures = pattern.GetAggregatedFeatures();
        var match = await FindSimilarElementAsync(targetFeatures, candidates, 0.6, ct);

        if (match != null)
        {
            return CreateSelectorFromFeatures(match.MatchedElement);
        }

        return null;
    }

    /// <summary>
    /// วิเคราะห์หน้าเว็บและสร้าง Element Map
    /// </summary>
    public async Task<PageElementMap> AnalyzePageAsync(
        string pageHtml,
        string? screenshot = null,
        CancellationToken ct = default)
    {
        var map = new PageElementMap
        {
            AnalyzedAt = DateTime.UtcNow,
            Elements = new Dictionary<string, List<ElementFeatures>>()
        };

        // Extract all interactive elements
        var interactiveElements = await ExtractInteractiveElementsAsync(pageHtml, ct);

        // Group by semantic type
        foreach (var element in interactiveElements)
        {
            var semanticType = element.SemanticType;

            if (!map.Elements.ContainsKey(semanticType))
            {
                map.Elements[semanticType] = new List<ElementFeatures>();
            }

            map.Elements[semanticType].Add(element);
        }

        // Identify key elements
        map.KeyElements = IdentifyKeyElements(interactiveElements);

        // Detect page type
        map.DetectedPageType = DetectPageType(interactiveElements, pageHtml);

        _logger.LogInformation(
            "Analyzed page: {ElementCount} elements, {PageType} detected",
            interactiveElements.Count, map.DetectedPageType);

        return map;
    }

    /// <summary>
    /// สร้าง Smart Selector จาก Visual Features
    /// </summary>
    public ElementSelector CreateSmartSelector(ElementFeatures features)
    {
        var strategies = new List<SelectorStrategy>();

        // Strategy 1: Text-based
        if (!string.IsNullOrEmpty(features.TextFeatures))
        {
            strategies.Add(new SelectorStrategy
            {
                Type = SelectorType.Text,
                Value = features.TextFeatures,
                Priority = GetTextSelectorPriority(features)
            });
        }

        // Strategy 2: Position-based (relative)
        if (features.RelativePosition != null)
        {
            var xpath = BuildPositionBasedXPath(features);
            strategies.Add(new SelectorStrategy
            {
                Type = SelectorType.XPath,
                Value = xpath,
                Priority = 0.6
            });
        }

        // Strategy 3: Semantic-based
        var semanticXPath = BuildSemanticXPath(features);
        if (!string.IsNullOrEmpty(semanticXPath))
        {
            strategies.Add(new SelectorStrategy
            {
                Type = SelectorType.XPath,
                Value = semanticXPath,
                Priority = 0.75
            });
        }

        // Strategy 4: Visual context-based
        var contextSelector = BuildContextSelector(features);
        if (!string.IsNullOrEmpty(contextSelector))
        {
            strategies.Add(new SelectorStrategy
            {
                Type = SelectorType.CSS,
                Value = contextSelector,
                Priority = 0.7
            });
        }

        // Select best strategy
        var best = strategies.OrderByDescending(s => s.Priority).FirstOrDefault();

        if (best == null)
        {
            return new ElementSelector { Type = SelectorType.Smart };
        }

        return new ElementSelector
        {
            Type = best.Type,
            Value = best.Value,
            AIDescription = features.Purpose,
            Confidence = best.Priority
        };
    }

    #region Private Methods - Feature Extraction

    private RelativePosition CalculateRelativePosition(ElementPosition pos)
    {
        // Normalize to 0-1 range (assuming 1920x1080 viewport)
        return new RelativePosition
        {
            X = pos.X / 1920.0,
            Y = pos.Y / 1080.0,
            FromTop = pos.Y < 200 ? VerticalPosition.Top :
                      pos.Y > 800 ? VerticalPosition.Bottom : VerticalPosition.Middle,
            FromLeft = pos.X < 400 ? HorizontalPosition.Left :
                       pos.X > 1500 ? HorizontalPosition.Right : HorizontalPosition.Center
        };
    }

    private string DetermineQuadrant(ElementPosition pos)
    {
        var isTop = pos.Y < 540;
        var isLeft = pos.X < 960;

        return (isTop, isLeft) switch
        {
            (true, true) => "top-left",
            (true, false) => "top-right",
            (false, true) => "bottom-left",
            (false, false) => "bottom-right"
        };
    }

    private string CategorizeSizeInternal(ElementSize size)
    {
        var area = size.Width * size.Height;

        return area switch
        {
            < 1000 => "tiny",
            < 5000 => "small",
            < 20000 => "medium",
            < 50000 => "large",
            _ => "extra-large"
        };
    }

    private string ExtractTextFeatures(string text)
    {
        // Clean and normalize text
        var cleaned = text.Trim();

        // Limit length
        if (cleaned.Length > 100)
        {
            cleaned = cleaned.Substring(0, 100);
        }

        return cleaned;
    }

    private bool ContainsEmoji(string text)
    {
        return Regex.IsMatch(text, @"[\u{1F000}-\u{1F9FF}]", RegexOptions.Compiled);
    }

    private string DetermineSemanticType(RecordedElement element)
    {
        var tag = element.TagName.ToLowerInvariant();
        var type = element.Type?.ToLowerInvariant() ?? "";
        var role = element.Attributes.GetValueOrDefault("role", "");

        // Button variants
        if (tag == "button" || role == "button" || type == "submit" || type == "button")
            return "button";

        // Input variants
        if (tag == "input" || tag == "textarea")
        {
            return type switch
            {
                "text" or "" => "text-input",
                "email" => "email-input",
                "password" => "password-input",
                "file" => "file-input",
                "checkbox" => "checkbox",
                "radio" => "radio",
                _ => "input"
            };
        }

        // Links
        if (tag == "a") return "link";

        // Media
        if (tag == "img") return "image";
        if (tag == "video") return "video";

        // Select
        if (tag == "select") return "dropdown";

        // Labels
        if (tag == "label") return "label";

        return "other";
    }

    private string InferPurpose(RecordedElement element)
    {
        var indicators = new List<string>();

        // Collect all text indicators
        if (!string.IsNullOrEmpty(element.TextContent))
            indicators.Add(element.TextContent.ToLowerInvariant());

        if (!string.IsNullOrEmpty(element.Placeholder))
            indicators.Add(element.Placeholder.ToLowerInvariant());

        if (element.Attributes.TryGetValue("aria-label", out var ariaLabel))
            indicators.Add(ariaLabel.ToLowerInvariant());

        if (!string.IsNullOrEmpty(element.Name))
            indicators.Add(element.Name.ToLowerInvariant());

        var combined = string.Join(" ", indicators);

        // Detect purpose from indicators
        if (ContainsAny(combined, "post", "publish", "share", "submit", "send"))
            return "submit_action";

        if (ContainsAny(combined, "login", "sign in", "log in"))
            return "login_action";

        if (ContainsAny(combined, "message", "text", "content", "write", "compose"))
            return "content_input";

        if (ContainsAny(combined, "search", "find"))
            return "search";

        if (ContainsAny(combined, "upload", "attach", "photo", "image", "video"))
            return "media_upload";

        if (ContainsAny(combined, "next", "continue", "proceed"))
            return "navigation";

        if (ContainsAny(combined, "cancel", "close", "back"))
            return "cancel_action";

        return "unknown";
    }

    private bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(k => text.Contains(k));
    }

    private bool HasIcon(RecordedElement element)
    {
        var classes = element.ClassName?.ToLowerInvariant() ?? "";
        var hasIconClass = classes.Contains("icon") || classes.Contains("fa-") ||
                           classes.Contains("material") || classes.Contains("svg");

        return hasIconClass;
    }

    private bool HasImage(RecordedElement element)
    {
        return element.TagName.ToLowerInvariant() == "img" ||
               !string.IsNullOrEmpty(element.Attributes.GetValueOrDefault("style", "")
                   .Contains("background-image") ? "y" : null);
    }

    private bool IsClickable(RecordedElement element)
    {
        var tag = element.TagName.ToLowerInvariant();
        var role = element.Attributes.GetValueOrDefault("role", "");
        var tabindex = element.Attributes.GetValueOrDefault("tabindex", "");

        return tag == "button" || tag == "a" || role == "button" ||
               element.Type == "submit" || !string.IsNullOrEmpty(tabindex);
    }

    private bool IsInputElement(RecordedElement element)
    {
        var tag = element.TagName.ToLowerInvariant();
        return tag == "input" || tag == "textarea" || tag == "select";
    }

    private string GetBorderRadius(RecordedElement element)
    {
        var classes = element.ClassName?.ToLowerInvariant() ?? "";

        if (classes.Contains("rounded-full") || classes.Contains("circle"))
            return "full";
        if (classes.Contains("rounded"))
            return "rounded";

        return "none";
    }

    private string EstimateColor(RecordedElement element)
    {
        var classes = element.ClassName?.ToLowerInvariant() ?? "";

        if (classes.Contains("primary") || classes.Contains("blue"))
            return "primary";
        if (classes.Contains("secondary") || classes.Contains("gray"))
            return "secondary";
        if (classes.Contains("success") || classes.Contains("green"))
            return "success";
        if (classes.Contains("danger") || classes.Contains("red"))
            return "danger";
        if (classes.Contains("warning") || classes.Contains("yellow"))
            return "warning";

        return "default";
    }

    private string CalculateNeighborSignature(RecordedElement element)
    {
        // Create a signature based on parent context
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(element.ParentId))
            parts.Add($"parent:{element.ParentId}");

        if (element.Position != null)
            parts.Add($"quad:{DetermineQuadrant(element.Position)}");

        return string.Join("|", parts);
    }

    #endregion

    #region Private Methods - Similarity Calculation

    private double CalculateSimilarityScore(ElementFeatures target, ElementFeatures candidate)
    {
        var scores = new Dictionary<string, double>();

        // Position similarity
        if (target.RelativePosition != null && candidate.RelativePosition != null)
        {
            scores["position"] = CalculatePositionSimilarity(
                target.RelativePosition, candidate.RelativePosition);
        }

        // Size similarity
        scores["size"] = target.SizeCategory == candidate.SizeCategory ? 1.0 :
                         AreSimilarSizes(target.SizeCategory, candidate.SizeCategory) ? 0.7 : 0.3;

        // Text similarity
        if (!string.IsNullOrEmpty(target.TextFeatures) && !string.IsNullOrEmpty(candidate.TextFeatures))
        {
            scores["text"] = CalculateTextSimilarity(target.TextFeatures, candidate.TextFeatures);
        }

        // Semantic similarity
        scores["semantic"] = target.SemanticType == candidate.SemanticType ? 1.0 :
                             AreSimilarSemanticTypes(target.SemanticType, candidate.SemanticType) ? 0.6 : 0.2;

        // Context similarity
        if (target.VisualContext != null && candidate.VisualContext != null)
        {
            scores["context"] = CalculateContextSimilarity(target.VisualContext, candidate.VisualContext);
        }

        // Calculate weighted average
        var totalWeight = 0.0;
        var weightedSum = 0.0;

        foreach (var (feature, score) in scores)
        {
            if (FeatureWeights.TryGetValue(feature, out var weight))
            {
                weightedSum += score * weight;
                totalWeight += weight;
            }
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0;
    }

    private double CalculatePositionSimilarity(RelativePosition a, RelativePosition b)
    {
        var distance = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        return Math.Max(0, 1 - distance * 2); // Normalize to 0-1
    }

    private bool AreSimilarSizes(string a, string b)
    {
        var sizes = new[] { "tiny", "small", "medium", "large", "extra-large" };
        var indexA = Array.IndexOf(sizes, a);
        var indexB = Array.IndexOf(sizes, b);

        return Math.Abs(indexA - indexB) <= 1;
    }

    private double CalculateTextSimilarity(string a, string b)
    {
        // Simple Jaccard similarity on words
        var wordsA = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var wordsB = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        var intersection = wordsA.Intersect(wordsB).Count();
        var union = wordsA.Union(wordsB).Count();

        return union > 0 ? (double)intersection / union : 0;
    }

    private bool AreSimilarSemanticTypes(string a, string b)
    {
        var groups = new[]
        {
            new[] { "button", "link" },
            new[] { "text-input", "email-input", "password-input", "input" },
            new[] { "checkbox", "radio" }
        };

        return groups.Any(g => g.Contains(a) && g.Contains(b));
    }

    private double CalculateContextSimilarity(VisualContext a, VisualContext b)
    {
        var matches = 0;
        var total = 5;

        if (a.HasIcon == b.HasIcon) matches++;
        if (a.HasImage == b.HasImage) matches++;
        if (a.IsClickable == b.IsClickable) matches++;
        if (a.IsInput == b.IsInput) matches++;
        if (a.BorderRadius == b.BorderRadius) matches++;

        return (double)matches / total;
    }

    private List<string> GetMatchedFeatures(ElementFeatures target, ElementFeatures candidate)
    {
        var matched = new List<string>();

        if (target.SemanticType == candidate.SemanticType)
            matched.Add("semantic_type");

        if (target.Purpose == candidate.Purpose)
            matched.Add("purpose");

        if (target.SizeCategory == candidate.SizeCategory)
            matched.Add("size");

        if (target.PositionQuadrant == candidate.PositionQuadrant)
            matched.Add("position");

        return matched;
    }

    private double CalculateConfidence(double score, ElementFeatures target, ElementFeatures candidate)
    {
        // Boost confidence if multiple strong features match
        var boost = 0.0;

        if (target.SemanticType == candidate.SemanticType)
            boost += 0.05;

        if (target.Purpose == candidate.Purpose)
            boost += 0.1;

        return Math.Min(1.0, score + boost);
    }

    #endregion

    #region Private Methods - Pattern Learning

    private void UpdatePatternAggregates(VisualPattern pattern)
    {
        if (pattern.Samples.Count == 0) return;

        // Calculate most common values
        pattern.MostCommonSemanticType = pattern.Samples
            .GroupBy(s => s.SemanticType)
            .OrderByDescending(g => g.Count())
            .First().Key;

        pattern.MostCommonSizeCategory = pattern.Samples
            .GroupBy(s => s.SizeCategory)
            .OrderByDescending(g => g.Count())
            .First().Key;

        pattern.AverageConfidence = pattern.Samples.Average(s => s.Confidence);
    }

    private async Task<List<ElementFeatures>> ExtractCandidatesFromHtmlAsync(
        string pageHtml,
        string purpose,
        CancellationToken ct)
    {
        var candidates = new List<ElementFeatures>();

        // Simple HTML parsing for common elements
        var patterns = purpose switch
        {
            "submit_action" => new[] { @"<button[^>]*>", @"<input[^>]*type=['""]submit['""]" },
            "content_input" => new[] { @"<textarea[^>]*>", @"<input[^>]*type=['""]text['""]" },
            "media_upload" => new[] { @"<input[^>]*type=['""]file['""]" },
            _ => new[] { @"<button[^>]*>", @"<input[^>]*>", @"<a[^>]*>" }
        };

        await Task.Run(() =>
        {
            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(pageHtml, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var element = ParseElementFromHtml(match.Value);
                    if (element != null)
                    {
                        candidates.Add(element);
                    }
                }
            }
        }, ct);

        return candidates;
    }

    private ElementFeatures? ParseElementFromHtml(string html)
    {
        // Simple attribute extraction
        var features = new ElementFeatures
        {
            ElementId = ExtractAttribute(html, "id") ?? Guid.NewGuid().ToString()
        };

        // Extract tag name
        var tagMatch = Regex.Match(html, @"<(\w+)");
        features.TagName = tagMatch.Success ? tagMatch.Groups[1].Value : "unknown";

        // Extract text-like attributes
        features.TextFeatures = ExtractAttribute(html, "aria-label") ??
                                ExtractAttribute(html, "placeholder") ??
                                ExtractAttribute(html, "value") ?? "";

        features.SemanticType = DetermineSemanticTypeFromHtml(html);
        features.Purpose = InferPurposeFromHtml(html);

        return features;
    }

    private string? ExtractAttribute(string html, string name)
    {
        var match = Regex.Match(html, $@"{name}=['""]([^'""]+)['""]");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string DetermineSemanticTypeFromHtml(string html)
    {
        if (html.Contains("type=\"submit\"") || html.StartsWith("<button"))
            return "button";
        if (html.Contains("type=\"text\"") || html.Contains("type=\"email\""))
            return "text-input";
        if (html.StartsWith("<textarea"))
            return "text-input";
        if (html.Contains("type=\"file\""))
            return "file-input";

        return "other";
    }

    private string InferPurposeFromHtml(string html)
    {
        var lower = html.ToLowerInvariant();

        if (ContainsAny(lower, "submit", "post", "publish", "send"))
            return "submit_action";
        if (ContainsAny(lower, "message", "content", "compose"))
            return "content_input";
        if (ContainsAny(lower, "file", "upload", "attach"))
            return "media_upload";

        return "unknown";
    }

    private ElementSelector CreateSelectorFromFeatures(ElementFeatures features)
    {
        return CreateSmartSelector(features);
    }

    #endregion

    #region Private Methods - Page Analysis

    private async Task<List<ElementFeatures>> ExtractInteractiveElementsAsync(
        string pageHtml,
        CancellationToken ct)
    {
        var elements = new List<ElementFeatures>();

        await Task.Run(() =>
        {
            var interactivePatterns = new[]
            {
                @"<button[^>]*>.*?</button>",
                @"<input[^>]*>",
                @"<textarea[^>]*>.*?</textarea>",
                @"<a[^>]*>.*?</a>",
                @"<select[^>]*>.*?</select>"
            };

            foreach (var pattern in interactivePatterns)
            {
                var matches = Regex.Matches(pageHtml, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                foreach (Match match in matches)
                {
                    var features = ParseElementFromHtml(match.Value);
                    if (features != null)
                    {
                        elements.Add(features);
                    }
                }
            }
        }, ct);

        return elements;
    }

    private Dictionary<string, ElementFeatures> IdentifyKeyElements(List<ElementFeatures> elements)
    {
        var keyElements = new Dictionary<string, ElementFeatures>();

        // Find primary action button
        var submitButton = elements.FirstOrDefault(e =>
            e.Purpose == "submit_action" || e.SemanticType == "button");
        if (submitButton != null)
            keyElements["primary_action"] = submitButton;

        // Find main content input
        var contentInput = elements.FirstOrDefault(e =>
            e.Purpose == "content_input");
        if (contentInput != null)
            keyElements["content_input"] = contentInput;

        // Find file upload
        var fileInput = elements.FirstOrDefault(e =>
            e.SemanticType == "file-input");
        if (fileInput != null)
            keyElements["file_upload"] = fileInput;

        return keyElements;
    }

    private string DetectPageType(List<ElementFeatures> elements, string pageHtml)
    {
        var purposes = elements.Select(e => e.Purpose).ToList();

        // Login page
        if (elements.Any(e => e.SemanticType == "password-input"))
            return "login";

        // Compose/Post page
        if (purposes.Contains("content_input") && purposes.Contains("submit_action"))
            return "compose";

        // Upload page
        if (purposes.Contains("media_upload"))
            return "upload";

        // Feed page
        if (pageHtml.Contains("feed") || pageHtml.Contains("timeline"))
            return "feed";

        return "unknown";
    }

    private double GetTextSelectorPriority(ElementFeatures features)
    {
        // Higher priority for unique, short text
        if (features.TextLength <= 20 && !features.HasEmoji)
            return 0.85;
        if (features.TextLength <= 50)
            return 0.75;
        return 0.6;
    }

    private string BuildPositionBasedXPath(ElementFeatures features)
    {
        var pos = features.RelativePosition!;
        var tag = features.TagName ?? "*";

        // Build XPath based on position
        var positionPredicate = (pos.FromTop, pos.FromLeft) switch
        {
            (VerticalPosition.Top, HorizontalPosition.Left) => "position() < 3",
            (VerticalPosition.Top, HorizontalPosition.Right) => "position() > 2",
            (VerticalPosition.Bottom, _) => "position() > 5",
            _ => "position() > 2 and position() < 6"
        };

        return $"//{tag}[{positionPredicate}]";
    }

    private string BuildSemanticXPath(ElementFeatures features)
    {
        return features.SemanticType switch
        {
            "button" => "//button | //input[@type='submit'] | //*[@role='button']",
            "text-input" => "//textarea | //input[@type='text'] | //*[@contenteditable='true']",
            "file-input" => "//input[@type='file']",
            _ => ""
        };
    }

    private string BuildContextSelector(ElementFeatures features)
    {
        if (features.VisualContext == null) return "";

        var parts = new List<string>();

        if (features.VisualContext.IsClickable)
            parts.Add("[onclick], [tabindex], button, a");

        if (features.VisualContext.IsInput)
            parts.Add("input, textarea, select");

        return string.Join(", ", parts);
    }

    #endregion
}

#region Models

public class ElementFeatures
{
    public string ElementId { get; set; } = "";
    public string TagName { get; set; } = "";
    public string SemanticType { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string? TextFeatures { get; set; }
    public int TextLength { get; set; }
    public bool HasEmoji { get; set; }
    public string SizeCategory { get; set; } = "";
    public double AspectRatio { get; set; }
    public RelativePosition? RelativePosition { get; set; }
    public string? PositionQuadrant { get; set; }
    public VisualContext? VisualContext { get; set; }
    public string? NeighborSignature { get; set; }
    public double Confidence { get; set; } = 0.5;
    public DateTime ExtractedAt { get; set; }
}

public class RelativePosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public VerticalPosition FromTop { get; set; }
    public HorizontalPosition FromLeft { get; set; }
}

public enum VerticalPosition { Top, Middle, Bottom }
public enum HorizontalPosition { Left, Center, Right }

public class VisualContext
{
    public bool HasIcon { get; set; }
    public bool HasImage { get; set; }
    public bool IsClickable { get; set; }
    public bool IsInput { get; set; }
    public string BorderRadius { get; set; } = "";
    public string EstimatedColor { get; set; } = "";
}

public class MatchResult
{
    public ElementFeatures MatchedElement { get; set; } = new();
    public double SimilarityScore { get; set; }
    public double Confidence { get; set; }
    public List<string> MatchedFeatures { get; set; } = new();
}

public class VisualPattern
{
    public string Platform { get; set; } = "";
    public string Purpose { get; set; } = "";
    public List<ElementFeatures> Samples { get; set; } = new();
    public string MostCommonSemanticType { get; set; } = "";
    public string MostCommonSizeCategory { get; set; } = "";
    public double AverageConfidence { get; set; }

    public ElementFeatures GetAggregatedFeatures()
    {
        if (Samples.Count == 0)
            return new ElementFeatures();

        return new ElementFeatures
        {
            SemanticType = MostCommonSemanticType,
            SizeCategory = MostCommonSizeCategory,
            Purpose = Purpose,
            Confidence = AverageConfidence
        };
    }
}

public class PageElementMap
{
    public DateTime AnalyzedAt { get; set; }
    public Dictionary<string, List<ElementFeatures>> Elements { get; set; } = new();
    public Dictionary<string, ElementFeatures> KeyElements { get; set; } = new();
    public string DetectedPageType { get; set; } = "";
}

public class SelectorStrategy
{
    public SelectorType Type { get; set; }
    public string Value { get; set; } = "";
    public double Priority { get; set; }
}

#endregion
