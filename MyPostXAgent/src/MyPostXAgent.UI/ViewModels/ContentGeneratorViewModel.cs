using System.Windows;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services.Data;

namespace MyPostXAgent.UI.ViewModels;

public class ContentGeneratorViewModel : BaseViewModel
{
    private readonly DatabaseService _database;

    // AI Provider Selection
    private bool _useOllama = true;
    public bool UseOllama
    {
        get => _useOllama;
        set
        {
            if (SetProperty(ref _useOllama, value) && value)
            {
                ClearOtherProviders(nameof(UseOllama));
            }
        }
    }

    private bool _useOpenAI;
    public bool UseOpenAI
    {
        get => _useOpenAI;
        set
        {
            if (SetProperty(ref _useOpenAI, value) && value)
            {
                ClearOtherProviders(nameof(UseOpenAI));
            }
        }
    }

    private bool _useClaude;
    public bool UseClaude
    {
        get => _useClaude;
        set
        {
            if (SetProperty(ref _useClaude, value) && value)
            {
                ClearOtherProviders(nameof(UseClaude));
            }
        }
    }

    private bool _useGemini;
    public bool UseGemini
    {
        get => _useGemini;
        set
        {
            if (SetProperty(ref _useGemini, value) && value)
            {
                ClearOtherProviders(nameof(UseGemini));
            }
        }
    }

    private void ClearOtherProviders(string except)
    {
        if (except != nameof(UseOllama) && _useOllama)
        {
            _useOllama = false;
            OnPropertyChanged(nameof(UseOllama));
        }
        if (except != nameof(UseOpenAI) && _useOpenAI)
        {
            _useOpenAI = false;
            OnPropertyChanged(nameof(UseOpenAI));
        }
        if (except != nameof(UseClaude) && _useClaude)
        {
            _useClaude = false;
            OnPropertyChanged(nameof(UseClaude));
        }
        if (except != nameof(UseGemini) && _useGemini)
        {
            _useGemini = false;
            OnPropertyChanged(nameof(UseGemini));
        }
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

    public ContentGeneratorViewModel(DatabaseService database)
    {
        _database = database;

        GenerateCommand = new RelayCommand(async () => await GenerateContentAsync());
        RegenerateCommand = new RelayCommand(async () => await GenerateContentAsync());
        ClearCommand = new RelayCommand(ClearAll);
        CopyContentCommand = new RelayCommand(CopyContent);
        SaveAsDraftCommand = new RelayCommand(async () => await SaveAsDraftAsync());
        CreatePostCommand = new RelayCommand(async () => await CreatePostAsync());
    }

    private async Task GenerateContentAsync()
    {
        if (string.IsNullOrWhiteSpace(Topic))
        {
            MessageBox.Show("กรุณากรอกหัวข้อหรือคำอธิบาย", "ข้อมูลไม่ครบ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsGenerating = true;
            GeneratedContent = string.Empty;
            GeneratedHashtags = string.Empty;

            // Build the prompt
            var prompt = BuildPrompt();

            // Simulate AI generation (replace with actual AI call)
            await Task.Delay(2000); // Simulate API call

            // For demo, generate sample content based on settings
            GeneratedContent = GenerateSampleContent();
            GeneratedHashtags = GenerateSampleHashtags();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private string BuildPrompt()
    {
        var contentTypes = new[] { "โพสต์โปรโมท", "เล่าเรื่อง/Storytelling", "รีวิวสินค้า", "ข่าวสาร/อัพเดท", "Tips & Tricks", "คำถาม/Poll", "แรงบันดาลใจ/Motivation" };
        var tones = new[] { "เป็นมิตร/Friendly", "มืออาชีพ/Professional", "ตลก/Humorous", "สร้างแรงบันดาลใจ", "แบบเด็ก Gen Z", "ทางการ/Formal" };
        var lengths = new[] { "สั้น (1-2 ประโยค)", "ปานกลาง (3-5 ประโยค)", "ยาว (1 ย่อหน้า)", "ยาวมาก (2+ ย่อหน้า)" };
        var languages = new[] { "ไทย", "English", "ไทย + English (ผสม)" };

        var prompt = $@"
สร้างเนื้อหาโพสต์ Social Media:
- หัวข้อ: {Topic}
- ประเภท: {contentTypes[SelectedContentTypeIndex]}
- โทนเสียง: {tones[SelectedToneIndex]}
- ความยาว: {lengths[SelectedLengthIndex]}
- ภาษา: {languages[SelectedLanguageIndex]}
- Keywords: {Keywords}
- ใส่ Emojis: {(IncludeEmojis ? "ใช่" : "ไม่")}
- ใส่ Call-to-Action: {(IncludeCTA ? "ใช่" : "ไม่")}
";

        return prompt;
    }

    private string GenerateSampleContent()
    {
        var tones = new[] { "เป็นมิตร", "มืออาชีพ", "ตลก", "แรงบันดาลใจ", "Gen Z", "ทางการ" };
        var tone = tones[SelectedToneIndex];

        var emoji = IncludeEmojis ? "✨🔥💯" : "";
        var cta = IncludeCTA ? "\n\n📍 สนใจติดต่อได้เลยนะคะ!" : "";

        var content = Topic switch
        {
            var t when t.Contains("กาแฟ") => $"{emoji} มาแล้วจ้า! ร้านกาแฟใหม่เปิดแล้ว ☕\n\nหอมกรุ่นกลิ่นกาแฟคั่วสดใหม่ทุกวัน บรรยากาศชิลล์ๆ นั่งทำงานได้ทั้งวัน\n\nโปรเปิดร้าน ลด 50% ทุกเมนู! 🎉{cta}",
            var t when t.Contains("สินค้า") => $"{emoji} รีวิวจริง ใช้จริง! 💖\n\nสินค้าตัวนี้ต้องบอกว่าปังมาก ใช้มาหลายเดือนแล้วประทับใจสุดๆ คุณภาพดี คุ้มค่าทุกบาท 👍{cta}",
            _ => $"{emoji} {Topic}\n\nเนื้อหาที่สร้างโดย AI ตามหัวข้อที่กำหนด จะมีความยาวและโทนเสียงตามที่เลือก ({tone}){cta}"
        };

        return content;
    }

    private string GenerateSampleHashtags()
    {
        var baseHashtags = "#โพสต์ #โซเชียลมีเดีย #การตลาด";

        if (!string.IsNullOrWhiteSpace(Hashtags))
        {
            return Hashtags;
        }

        // Generate based on keywords
        if (!string.IsNullOrWhiteSpace(Keywords))
        {
            var keywords = Keywords.Split(',').Select(k => k.Trim());
            var generated = string.Join(" ", keywords.Take(5).Select(k => $"#{k.Replace(" ", "")}"));
            return $"{generated} {baseHashtags}";
        }

        // Generate based on platforms
        var platformTags = new List<string>();
        if (TargetFacebook) platformTags.Add("#Facebook");
        if (TargetInstagram) platformTags.Add("#Instagram");
        if (TargetTikTok) platformTags.Add("#TikTok");
        if (TargetTwitter) platformTags.Add("#Twitter");
        if (TargetLine) platformTags.Add("#LINE");

        return $"{string.Join(" ", platformTags)} {baseHashtags}";
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
            MessageBox.Show("คัดลอกเนื้อหาแล้ว!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task SaveAsDraftAsync()
    {
        if (string.IsNullOrWhiteSpace(GeneratedContent))
        {
            MessageBox.Show("ไม่มีเนื้อหาที่จะบันทึก", "ข้อมูลไม่ครบ", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            MessageBox.Show("บันทึก Draft สำเร็จ!\n\nสามารถไปที่หน้า 'โพสต์' เพื่อแก้ไขหรือตั้งเวลาได้", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show("ไม่มีเนื้อหาที่จะโพสต์", "ข้อมูลไม่ครบ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            "ต้องการสร้างโพสต์และไปที่หน้าตั้งเวลาหรือไม่?\n\n(หรือกด 'ไม่' เพื่อบันทึกเป็น Draft)",
            "สร้างโพสต์",
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

            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show("สร้างโพสต์สำเร็จ!\n\nไปที่หน้า 'ตั้งเวลา' เพื่อกำหนดเวลาโพสต์", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("บันทึก Draft สำเร็จ!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // Clear after successful save
            ClearAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
