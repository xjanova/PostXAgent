using System.Collections.ObjectModel;
using System.Windows;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services;
using MyPostXAgent.Core.Services.Data;
using MyPostXAgent.Core.Services.AI;

namespace MyPostXAgent.UI.ViewModels;

public class ContentGeneratorViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly AIContentService _aiService;
    private readonly LocalizationService _localizationService;

    // Template Selection
    public ObservableCollection<string> TemplateCategories { get; }
    public ObservableCollection<PromptTemplate> AvailableTemplates { get; }

    private int _selectedCategoryIndex = -1;
    public int SelectedCategoryIndex
    {
        get => _selectedCategoryIndex;
        set
        {
            if (SetProperty(ref _selectedCategoryIndex, value))
            {
                LoadTemplatesForCategory();
            }
        }
    }

    private PromptTemplate? _selectedTemplate;
    public PromptTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value) && value != null)
            {
                ApplyTemplate(value);
            }
        }
    }

    // AI Provider Selection (Fixed: No circular property updates)
    private AIProvider _selectedProvider = AIProvider.Ollama;
    public AIProvider SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value))
            {
                OnPropertyChanged(nameof(UseOllama));
                OnPropertyChanged(nameof(UseOpenAI));
                OnPropertyChanged(nameof(UseClaude));
                OnPropertyChanged(nameof(UseGemini));
            }
        }
    }

    public bool UseOllama
    {
        get => SelectedProvider == AIProvider.Ollama;
        set { if (value) SelectedProvider = AIProvider.Ollama; }
    }

    public bool UseOpenAI
    {
        get => SelectedProvider == AIProvider.OpenAI;
        set { if (value) SelectedProvider = AIProvider.OpenAI; }
    }

    public bool UseClaude
    {
        get => SelectedProvider == AIProvider.Claude;
        set { if (value) SelectedProvider = AIProvider.Claude; }
    }

    public bool UseGemini
    {
        get => SelectedProvider == AIProvider.Gemini;
        set { if (value) SelectedProvider = AIProvider.Gemini; }
    }

    // Content Details
    private int _selectedContentTypeIndex;
    public int SelectedContentTypeIndex
    {
        get => _selectedContentTypeIndex;
        set => SetProperty(ref _selectedContentTypeIndex, value);
    }

    private int _selectedToneIndex;
    public int SelectedToneIndex
    {
        get => _selectedToneIndex;
        set => SetProperty(ref _selectedToneIndex, value);
    }

    private string _topic = string.Empty;
    public string Topic
    {
        get => _topic;
        set => SetProperty(ref _topic, value);
    }

    private string _keywords = string.Empty;
    public string Keywords
    {
        get => _keywords;
        set => SetProperty(ref _keywords, value);
    }

    private string _hashtags = string.Empty;
    public string Hashtags
    {
        get => _hashtags;
        set => SetProperty(ref _hashtags, value);
    }

    // Target Platforms
    private bool _targetFacebook = true;
    public bool TargetFacebook
    {
        get => _targetFacebook;
        set => SetProperty(ref _targetFacebook, value);
    }

    private bool _targetInstagram;
    public bool TargetInstagram
    {
        get => _targetInstagram;
        set => SetProperty(ref _targetInstagram, value);
    }

    private bool _targetTikTok;
    public bool TargetTikTok
    {
        get => _targetTikTok;
        set => SetProperty(ref _targetTikTok, value);
    }

    private bool _targetTwitter;
    public bool TargetTwitter
    {
        get => _targetTwitter;
        set => SetProperty(ref _targetTwitter, value);
    }

    private bool _targetLine;
    public bool TargetLine
    {
        get => _targetLine;
        set => SetProperty(ref _targetLine, value);
    }

    // Advanced Options
    private int _selectedLengthIndex = 1; // Default: Medium
    public int SelectedLengthIndex
    {
        get => _selectedLengthIndex;
        set => SetProperty(ref _selectedLengthIndex, value);
    }

    private int _selectedLanguageIndex; // Default: Thai
    public int SelectedLanguageIndex
    {
        get => _selectedLanguageIndex;
        set => SetProperty(ref _selectedLanguageIndex, value);
    }

    private bool _includeEmojis = true;
    public bool IncludeEmojis
    {
        get => _includeEmojis;
        set => SetProperty(ref _includeEmojis, value);
    }

    private bool _includeCTA = true;
    public bool IncludeCTA
    {
        get => _includeCTA;
        set => SetProperty(ref _includeCTA, value);
    }

    // Generated Content
    private string _generatedContent = string.Empty;
    public string GeneratedContent
    {
        get => _generatedContent;
        set
        {
            if (SetProperty(ref _generatedContent, value))
            {
                OnPropertyChanged(nameof(CharacterCount));
                OnPropertyChanged(nameof(WordCount));
                OnPropertyChanged(nameof(HasContent));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    private string _generatedHashtags = string.Empty;
    public string GeneratedHashtags
    {
        get => _generatedHashtags;
        set
        {
            if (SetProperty(ref _generatedHashtags, value))
            {
                OnPropertyChanged(nameof(HasGeneratedHashtags));
            }
        }
    }

    private bool _isGenerating;
    public bool IsGenerating
    {
        get => _isGenerating;
        set
        {
            if (SetProperty(ref _isGenerating, value))
            {
                OnPropertyChanged(nameof(IsNotGenerating));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public bool IsNotGenerating => !IsGenerating;
    public bool HasContent => !string.IsNullOrWhiteSpace(GeneratedContent);
    public bool HasGeneratedHashtags => !string.IsNullOrWhiteSpace(GeneratedHashtags);
    public bool ShowEmptyState => !IsGenerating && !HasContent;
    public int CharacterCount => GeneratedContent?.Length ?? 0;
    public int WordCount => string.IsNullOrWhiteSpace(GeneratedContent)
        ? 0
        : GeneratedContent.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

    // Commands
    public RelayCommand GenerateCommand { get; }
    public RelayCommand RegenerateCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand CopyContentCommand { get; }
    public RelayCommand SaveAsDraftCommand { get; }
    public RelayCommand CreatePostCommand { get; }

    public ContentGeneratorViewModel(DatabaseService database, AIContentService aiService, LocalizationService localizationService)
    {
        _database = database;
        _aiService = aiService;
        _localizationService = localizationService;

        // Initialize template collections
        TemplateCategories = new ObservableCollection<string>(BuiltInTemplates.GetCategories());
        AvailableTemplates = new ObservableCollection<PromptTemplate>();

        GenerateCommand = new RelayCommand(async () => await GenerateContentAsync());
        RegenerateCommand = new RelayCommand(async () => await GenerateContentAsync());
        ClearCommand = new RelayCommand(ClearAll);
        CopyContentCommand = new RelayCommand(CopyContent);
        SaveAsDraftCommand = new RelayCommand(async () => await SaveAsDraftAsync());
        CreatePostCommand = new RelayCommand(async () => await CreatePostAsync());

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Initialize AI providers on startup
        _ = InitializeAIProvidersAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Refresh UI text when language changes
        OnPropertyChanged(nameof(GenerateCommand));
        OnPropertyChanged(nameof(RegenerateCommand));
        OnPropertyChanged(nameof(ClearCommand));
        OnPropertyChanged(nameof(CopyContentCommand));
        OnPropertyChanged(nameof(SaveAsDraftCommand));
        OnPropertyChanged(nameof(CreatePostCommand));
    }

    private async Task InitializeAIProvidersAsync()
    {
        try
        {
            await _aiService.InitializeProvidersAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing AI providers: {ex.Message}");
        }
    }

    private async Task GenerateContentAsync()
    {
        if (string.IsNullOrWhiteSpace(Topic))
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "กรุณากรอกหัวข้อหรือคำอธิบาย" : "Please enter a topic or description",
                isThai ? "ข้อมูลไม่ครบ" : "Incomplete Data",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsGenerating = true;
            GeneratedContent = string.Empty;
            GeneratedHashtags = string.Empty;

            // Determine which AI provider to use
            var provider = GetSelectedAIProvider();

            // Build the request
            var request = BuildContentRequest();

            // Generate content using AI
            System.Diagnostics.Debug.WriteLine($"Attempting to generate content with provider: {provider}");
            var result = await _aiService.GenerateContentAsync(request, provider, useFallback: true);

            var isThai = _localizationService.IsThaiLanguage;

            if (result.Success)
            {
                GeneratedContent = result.Content;
                GeneratedHashtags = result.Hashtags;

                // Show success notification with provider info
                System.Diagnostics.Debug.WriteLine($"✅ Content generated successfully using {result.Provider}");
                MessageBox.Show(
                    LocalizationStrings.ContentGen.GenerateSuccess(isThai, result.Provider.ToString()),
                    LocalizationStrings.Common.Success(isThai),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ Content generation failed: {result.ErrorMessage}");

                // Show detailed error message
                var errorDetails = !string.IsNullOrEmpty(result.ErrorMessage)
                    ? result.ErrorMessage
                    : (isThai ? "ไม่ทราบสาเหตุ" : "Unknown error");

                var suggestions = isThai
                    ? $"💡 แนะนำ:\n1. ตรวจสอบว่า Ollama กำลังรันอยู่ (ollama serve)\n2. ตรวจสอบว่าติดตั้ง model แล้ว (ollama pull llama3.2)\n3. ตรวจสอบ AI Status ที่ Status Bar ด้านล่าง"
                    : $"💡 Suggestions:\n1. Check if Ollama is running (ollama serve)\n2. Verify model is installed (ollama pull llama3.2)\n3. Check AI Status in the Status Bar below";

                MessageBox.Show(
                    $"{LocalizationStrings.ContentGen.GenerateFailed(isThai)}\n\n" +
                    $"🔴 Provider: {provider}\n" +
                    $"🔴 Error: {errorDetails}\n\n" +
                    suggestions,
                    LocalizationStrings.ContentGen.GenerateFailed(isThai),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                $"{LocalizationStrings.Common.Error(isThai)}: {ex.Message}",
                LocalizationStrings.Common.Error(isThai),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private AIProvider GetSelectedAIProvider()
    {
        return SelectedProvider;
    }

    private ContentGenerationRequest BuildContentRequest()
    {
        var contentTypes = new[] { "โพสต์โปรโมท", "เล่าเรื่อง/Storytelling", "รีวิวสินค้า", "ข่าวสาร/อัพเดท", "Tips & Tricks", "คำถาม/Poll", "แรงบันดาลใจ/Motivation" };
        var tones = new[] { "เป็นมิตร/Friendly", "มืออาชีพ/Professional", "ตลก/Humorous", "สร้างแรงบันดาลใจ", "แบบเด็ก Gen Z", "ทางการ/Formal" };
        var lengths = new[] { "สั้น (1-2 ประโยค)", "ปานกลาง (3-5 ประโยค)", "ยาว (1 ย่อหน้า)", "ยาวมาก (2+ ย่อหน้า)" };
        var languages = new[] { "ไทย", "English", "ไทย + English (ผสม)" };

        var platforms = new List<string>();
        if (TargetFacebook) platforms.Add("Facebook");
        if (TargetInstagram) platforms.Add("Instagram");
        if (TargetTikTok) platforms.Add("TikTok");
        if (TargetTwitter) platforms.Add("Twitter");
        if (TargetLine) platforms.Add("LINE");

        return new ContentGenerationRequest
        {
            Topic = Topic,
            ContentType = contentTypes[SelectedContentTypeIndex],
            Tone = tones[SelectedToneIndex],
            Length = lengths[SelectedLengthIndex],
            Language = languages[SelectedLanguageIndex],
            Keywords = Keywords,
            IncludeEmojis = IncludeEmojis,
            IncludeCTA = IncludeCTA,
            TargetPlatforms = platforms
        };
    }

    private void ClearAll()
    {
        Topic = string.Empty;
        Keywords = string.Empty;
        Hashtags = string.Empty;
        GeneratedContent = string.Empty;
        GeneratedHashtags = string.Empty;
        SelectedContentTypeIndex = 0;
        SelectedToneIndex = 0;
        SelectedLengthIndex = 1;
        SelectedLanguageIndex = 0;
    }

    private void CopyContent()
    {
        if (!string.IsNullOrWhiteSpace(GeneratedContent))
        {
            var fullContent = GeneratedContent;
            if (HasGeneratedHashtags)
            {
                fullContent += "\n\n" + GeneratedHashtags;
            }

            Clipboard.SetText(fullContent);
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "คัดลอกเนื้อหาแล้ว!" : "Content copied!",
                LocalizationStrings.Common.Success(isThai),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private async Task SaveAsDraftAsync()
    {
        if (string.IsNullOrWhiteSpace(GeneratedContent))
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "ไม่มีเนื้อหาที่จะบันทึก" : "No content to save",
                isThai ? "ข้อมูลไม่ครบ" : "Incomplete Data",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var platform = GetSelectedPlatform();

            var post = new Post
            {
                Content = GeneratedContent + (HasGeneratedHashtags ? "\n\n" + GeneratedHashtags : ""),
                Status = PostStatus.Draft,
                Platform = platform,
                CreatedAt = DateTime.UtcNow
            };

            await _database.AddPostAsync(post);

            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "บันทึก Draft สำเร็จ!\n\nสามารถไปที่หน้า 'โพสต์' เพื่อแก้ไขหรือตั้งเวลาได้"
                       : "Draft saved successfully!\n\nGo to 'Posts' page to edit or schedule",
                LocalizationStrings.Common.Success(isThai),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                $"{LocalizationStrings.Common.Error(isThai)}: {ex.Message}",
                LocalizationStrings.Common.Error(isThai),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private SocialPlatform GetSelectedPlatform()
    {
        if (TargetFacebook) return SocialPlatform.Facebook;
        if (TargetInstagram) return SocialPlatform.Instagram;
        if (TargetTikTok) return SocialPlatform.TikTok;
        if (TargetTwitter) return SocialPlatform.Twitter;
        if (TargetLine) return SocialPlatform.Line;
        return SocialPlatform.Facebook; // Default
    }

    private async Task CreatePostAsync()
    {
        if (string.IsNullOrWhiteSpace(GeneratedContent))
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "ไม่มีเนื้อหาที่จะโพสต์" : "No content to post",
                isThai ? "ข้อมูลไม่ครบ" : "Incomplete Data",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var isThai2 = _localizationService.IsThaiLanguage;
        var result = MessageBox.Show(
            isThai2 ? "ต้องการสร้างโพสต์และไปที่หน้าตั้งเวลาหรือไม่?\n\n(หรือกด 'ไม่' เพื่อบันทึกเป็น Draft)"
                    : "Create post and go to schedule page?\n\n(Or click 'No' to save as Draft)",
            isThai2 ? "สร้างโพสต์" : "Create Post",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
            return;

        try
        {
            var platform = GetSelectedPlatform();

            var post = new Post
            {
                Content = GeneratedContent + (HasGeneratedHashtags ? "\n\n" + GeneratedHashtags : ""),
                Status = PostStatus.Draft,
                Platform = platform,
                CreatedAt = DateTime.UtcNow
            };

            await _database.AddPostAsync(post);

            var isThai3 = _localizationService.IsThaiLanguage;
            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show(
                    isThai3 ? "สร้างโพสต์สำเร็จ!\n\nไปที่หน้า 'ตั้งเวลา' เพื่อกำหนดเวลาโพสต์"
                            : "Post created successfully!\n\nGo to 'Schedule' page to set posting time",
                    LocalizationStrings.Common.Success(isThai3),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    isThai3 ? "บันทึก Draft สำเร็จ!" : "Draft saved successfully!",
                    LocalizationStrings.Common.Success(isThai3),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            // Clear after successful save
            ClearAll();
        }
        catch (Exception ex)
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                $"{LocalizationStrings.Common.Error(isThai)}: {ex.Message}",
                LocalizationStrings.Common.Error(isThai),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LoadTemplatesForCategory()
    {
        AvailableTemplates.Clear();

        if (SelectedCategoryIndex < 0 || SelectedCategoryIndex >= TemplateCategories.Count)
            return;

        var category = TemplateCategories[SelectedCategoryIndex];
        var templates = BuiltInTemplates.GetByCategory(category);

        foreach (var template in templates)
        {
            AvailableTemplates.Add(template);
        }
    }

    private void ApplyTemplate(PromptTemplate template)
    {
        // Map template values to form
        Topic = template.Topic;
        Keywords = template.Keywords;

        // Set content type
        var contentTypes = new[] { "โพสต์โปรโมท", "เล่าเรื่อง/Storytelling", "รีวิวสินค้า", "ข่าวสาร/อัพเดท", "Tips & Tricks", "คำถาม/Poll", "แรงบันดาลใจ/Motivation" };
        SelectedContentTypeIndex = Array.IndexOf(contentTypes, template.ContentType);
        if (SelectedContentTypeIndex < 0) SelectedContentTypeIndex = 0;

        // Set tone
        var tones = new[] { "เป็นมิตร/Friendly", "มืออาชีพ/Professional", "ตลก/Humorous", "สร้างแรงบันดาลใจ", "แบบเด็ก Gen Z", "ทางการ/Formal" };
        SelectedToneIndex = Array.IndexOf(tones, template.Tone);
        if (SelectedToneIndex < 0) SelectedToneIndex = 0;

        // Set length
        var lengths = new[] { "สั้น (1-2 ประโยค)", "ปานกลาง (3-5 ประโยค)", "ยาว (1 ย่อหน้า)", "ยาวมาก (2+ ย่อหน้า)" };
        SelectedLengthIndex = Array.IndexOf(lengths, template.Length);
        if (SelectedLengthIndex < 0) SelectedLengthIndex = 1;

        // Set language
        var languages = new[] { "ไทย", "English", "ไทย + English (ผสม)" };
        SelectedLanguageIndex = Array.IndexOf(languages, template.Language);
        if (SelectedLanguageIndex < 0) SelectedLanguageIndex = 0;

        // Set options
        IncludeEmojis = template.IncludeEmojis;
        IncludeCTA = template.IncludeCTA;

        // Set platforms
        TargetFacebook = template.SuggestedPlatforms.Contains("Facebook");
        TargetInstagram = template.SuggestedPlatforms.Contains("Instagram");
        TargetTikTok = template.SuggestedPlatforms.Contains("TikTok");
        TargetTwitter = template.SuggestedPlatforms.Contains("Twitter");
        TargetLine = template.SuggestedPlatforms.Contains("LINE");
    }
}
