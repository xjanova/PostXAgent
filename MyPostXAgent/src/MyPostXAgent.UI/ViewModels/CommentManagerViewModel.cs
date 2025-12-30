using System.Collections.ObjectModel;
using System.Windows;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services;

namespace MyPostXAgent.UI.ViewModels;

/// <summary>
/// ViewModel for Comment Manager Page - Monitor and reply to comments
/// </summary>
public class CommentManagerViewModel : BaseViewModel
{
    private readonly LocalizationService _localizationService;

    // Collections
    public ObservableCollection<CommentItem> Comments { get; } = new();
    public ObservableCollection<CommentItem> PendingComments { get; } = new();
    public ObservableCollection<AutoReplyTemplate> AutoReplyTemplates { get; } = new();
    public ObservableCollection<SocialPlatform> Platforms { get; } = new();

    // Filters
    private SocialPlatform? _selectedPlatform;
    public SocialPlatform? SelectedPlatform
    {
        get => _selectedPlatform;
        set
        {
            if (SetProperty(ref _selectedPlatform, value))
            {
                _ = LoadCommentsAsync();
            }
        }
    }

    private CommentFilter _selectedFilter = CommentFilter.All;
    public CommentFilter SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                _ = LoadCommentsAsync();
            }
        }
    }

    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    // Selected Comment
    private CommentItem? _selectedComment;
    public CommentItem? SelectedComment
    {
        get => _selectedComment;
        set => SetProperty(ref _selectedComment, value);
    }

    // Reply
    private string _replyText = "";
    public string ReplyText
    {
        get => _replyText;
        set => SetProperty(ref _replyText, value);
    }

    private bool _showReplyDialog;
    public bool ShowReplyDialog
    {
        get => _showReplyDialog;
        set => SetProperty(ref _showReplyDialog, value);
    }

    // Auto Reply Settings
    private bool _autoReplyEnabled;
    public bool AutoReplyEnabled
    {
        get => _autoReplyEnabled;
        set => SetProperty(ref _autoReplyEnabled, value);
    }

    private bool _showAutoReplyDialog;
    public bool ShowAutoReplyDialog
    {
        get => _showAutoReplyDialog;
        set => SetProperty(ref _showAutoReplyDialog, value);
    }

    private string _newTemplateName = "";
    public string NewTemplateName
    {
        get => _newTemplateName;
        set => SetProperty(ref _newTemplateName, value);
    }

    private string _newTemplateKeywords = "";
    public string NewTemplateKeywords
    {
        get => _newTemplateKeywords;
        set => SetProperty(ref _newTemplateKeywords, value);
    }

    private string _newTemplateReply = "";
    public string NewTemplateReply
    {
        get => _newTemplateReply;
        set => SetProperty(ref _newTemplateReply, value);
    }

    // Statistics
    private int _totalComments;
    public int TotalComments
    {
        get => _totalComments;
        set => SetProperty(ref _totalComments, value);
    }

    private int _pendingCount;
    public int PendingCount
    {
        get => _pendingCount;
        set => SetProperty(ref _pendingCount, value);
    }

    private int _repliedCount;
    public int RepliedCount
    {
        get => _repliedCount;
        set => SetProperty(ref _repliedCount, value);
    }

    private int _positiveCount;
    public int PositiveCount
    {
        get => _positiveCount;
        set => SetProperty(ref _positiveCount, value);
    }

    private int _negativeCount;
    public int NegativeCount
    {
        get => _negativeCount;
        set => SetProperty(ref _negativeCount, value);
    }

    // Tab Selection
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    // Commands
    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand<CommentItem> ReplyCommand { get; }
    public RelayCommand<CommentItem> QuickReplyCommand { get; }
    public RelayCommand<CommentItem> LikeCommand { get; }
    public RelayCommand<CommentItem> HideCommand { get; }
    public RelayCommand<CommentItem> DeleteCommand { get; }
    public RelayCommand<CommentItem> MarkAsSpamCommand { get; }
    public RelayCommand SendReplyCommand { get; }
    public RelayCommand CancelReplyCommand { get; }
    public RelayCommand AddAutoReplyTemplateCommand { get; }
    public RelayCommand<AutoReplyTemplate> DeleteTemplateCommand { get; }
    public RelayCommand<AutoReplyTemplate> UseTemplateCommand { get; }
    public RelayCommand SaveAutoReplyTemplateCommand { get; }
    public RelayCommand CancelAutoReplyTemplateCommand { get; }
    public RelayCommand MarkAllAsReadCommand { get; }
    public RelayCommand ExportCommentsCommand { get; }

    public CommentManagerViewModel(LocalizationService localizationService)
    {
        _localizationService = localizationService;
        Title = LocalizationStrings.Nav.Comments(_localizationService.IsThaiLanguage);

        // Initialize platforms
        InitializePlatforms();
        InitializeAutoReplyTemplates();

        // Commands
        RefreshCommand = new RelayCommand(async () => await LoadCommentsAsync());
        SearchCommand = new RelayCommand(async () => await SearchCommentsAsync());
        ReplyCommand = new RelayCommand<CommentItem>(OpenReplyDialog);
        QuickReplyCommand = new RelayCommand<CommentItem>(async c => await QuickReplyAsync(c));
        LikeCommand = new RelayCommand<CommentItem>(async c => await LikeCommentAsync(c));
        HideCommand = new RelayCommand<CommentItem>(async c => await HideCommentAsync(c));
        DeleteCommand = new RelayCommand<CommentItem>(async c => await DeleteCommentAsync(c));
        MarkAsSpamCommand = new RelayCommand<CommentItem>(async c => await MarkAsSpamAsync(c));
        SendReplyCommand = new RelayCommand(async () => await SendReplyAsync());
        CancelReplyCommand = new RelayCommand(() => { ShowReplyDialog = false; ReplyText = ""; });
        AddAutoReplyTemplateCommand = new RelayCommand(() => ShowAutoReplyDialog = true);
        DeleteTemplateCommand = new RelayCommand<AutoReplyTemplate>(DeleteTemplate);
        UseTemplateCommand = new RelayCommand<AutoReplyTemplate>(UseTemplate);
        SaveAutoReplyTemplateCommand = new RelayCommand(SaveAutoReplyTemplate);
        CancelAutoReplyTemplateCommand = new RelayCommand(() => { ShowAutoReplyDialog = false; ClearTemplateForm(); });
        MarkAllAsReadCommand = new RelayCommand(MarkAllAsRead);
        ExportCommentsCommand = new RelayCommand(ExportComments);

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Load initial data
        _ = LoadCommentsAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Title = LocalizationStrings.Nav.Comments(_localizationService.IsThaiLanguage);
    }

    private void InitializePlatforms()
    {
        Platforms.Add(SocialPlatform.Facebook);
        Platforms.Add(SocialPlatform.Instagram);
        Platforms.Add(SocialPlatform.TikTok);
        Platforms.Add(SocialPlatform.YouTube);
    }

    private void InitializeAutoReplyTemplates()
    {
        AutoReplyTemplates.Add(new AutoReplyTemplate
        {
            Id = 1,
            Name = "ขอบคุณ",
            Keywords = "ดี, ชอบ, สวย, เยี่ยม",
            ReplyText = "ขอบคุณมากค่ะ/ครับ ที่สนับสนุน 🙏❤️",
            IsActive = true
        });
        AutoReplyTemplates.Add(new AutoReplyTemplate
        {
            Id = 2,
            Name = "สอบถามราคา",
            Keywords = "ราคา, เท่าไหร่, ราคาเท่าไร",
            ReplyText = "สอบถามราคาได้ทาง inbox นะคะ/ครับ 📩",
            IsActive = true
        });
        AutoReplyTemplates.Add(new AutoReplyTemplate
        {
            Id = 3,
            Name = "สั่งซื้อ",
            Keywords = "สั่ง, ซื้อ, order, สนใจ",
            ReplyText = "สนใจสั่งซื้อ inbox มาได้เลยนะคะ/ครับ ✨",
            IsActive = true
        });
    }

    public async Task LoadCommentsAsync()
    {
        try
        {
            IsBusy = true;

            // TODO: Load from social media APIs
            await Task.Delay(500);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Comments.Clear();
                PendingComments.Clear();

                // Sample comments
                var sampleComments = new List<CommentItem>
                {
                    new CommentItem
                    {
                        Id = "1",
                        Platform = SocialPlatform.Facebook,
                        AuthorName = "สมชาย ใจดี",
                        AuthorAvatar = "",
                        Text = "สินค้าดีมากครับ จัดส่งเร็ว บริการดี 👍👍",
                        PostTitle = "โปรโมชั่นสุดคุ้มประจำเดือน",
                        CreatedAt = DateTime.Now.AddHours(-2),
                        Sentiment = CommentSentiment.Positive,
                        IsRead = true,
                        LikeCount = 5
                    },
                    new CommentItem
                    {
                        Id = "2",
                        Platform = SocialPlatform.Facebook,
                        AuthorName = "มาลี วงศ์สกุล",
                        AuthorAvatar = "",
                        Text = "ราคาเท่าไหร่คะ สนใจอยากได้ค่ะ",
                        PostTitle = "โปรโมชั่นสุดคุ้มประจำเดือน",
                        CreatedAt = DateTime.Now.AddHours(-1),
                        Sentiment = CommentSentiment.Neutral,
                        IsRead = false,
                        LikeCount = 0
                    },
                    new CommentItem
                    {
                        Id = "3",
                        Platform = SocialPlatform.Instagram,
                        AuthorName = "beauty_lover",
                        AuthorAvatar = "",
                        Text = "สวยมากค่ะ 😍😍😍 อยากได้จัง",
                        PostTitle = "New Collection 2024",
                        CreatedAt = DateTime.Now.AddMinutes(-30),
                        Sentiment = CommentSentiment.Positive,
                        IsRead = false,
                        LikeCount = 12
                    },
                    new CommentItem
                    {
                        Id = "4",
                        Platform = SocialPlatform.TikTok,
                        AuthorName = "shopping_thai",
                        AuthorAvatar = "",
                        Text = "ส่งไปต่างจังหวัดได้ไหมครับ",
                        PostTitle = "Review สินค้าใหม่",
                        CreatedAt = DateTime.Now.AddMinutes(-15),
                        Sentiment = CommentSentiment.Neutral,
                        IsRead = false,
                        LikeCount = 2
                    },
                    new CommentItem
                    {
                        Id = "5",
                        Platform = SocialPlatform.Facebook,
                        AuthorName = "Anonymous User",
                        AuthorAvatar = "",
                        Text = "สินค้าแย่มาก ไม่ตรงปก",
                        PostTitle = "โปรโมชั่นสุดคุ้มประจำเดือน",
                        CreatedAt = DateTime.Now.AddDays(-1),
                        Sentiment = CommentSentiment.Negative,
                        IsRead = true,
                        LikeCount = 0,
                        IsHidden = false
                    }
                };

                foreach (var comment in sampleComments)
                {
                    if (SelectedPlatform == null || comment.Platform == SelectedPlatform)
                    {
                        switch (SelectedFilter)
                        {
                            case CommentFilter.Unread:
                                if (!comment.IsRead) Comments.Add(comment);
                                break;
                            case CommentFilter.Pending:
                                if (!comment.IsReplied) Comments.Add(comment);
                                break;
                            case CommentFilter.Positive:
                                if (comment.Sentiment == CommentSentiment.Positive) Comments.Add(comment);
                                break;
                            case CommentFilter.Negative:
                                if (comment.Sentiment == CommentSentiment.Negative) Comments.Add(comment);
                                break;
                            default:
                                Comments.Add(comment);
                                break;
                        }
                    }

                    if (!comment.IsReplied && !comment.IsHidden)
                    {
                        PendingComments.Add(comment);
                    }
                }

                // Update statistics
                TotalComments = sampleComments.Count;
                PendingCount = sampleComments.Count(c => !c.IsReplied);
                RepliedCount = sampleComments.Count(c => c.IsReplied);
                PositiveCount = sampleComments.Count(c => c.Sentiment == CommentSentiment.Positive);
                NegativeCount = sampleComments.Count(c => c.Sentiment == CommentSentiment.Negative);
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SearchCommentsAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await LoadCommentsAsync();
            return;
        }

        // Filter comments by search query
        var filtered = Comments.Where(c =>
            c.Text.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
            c.AuthorName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

        Comments.Clear();
        foreach (var comment in filtered)
        {
            Comments.Add(comment);
        }
    }

    private void OpenReplyDialog(CommentItem? comment)
    {
        if (comment == null) return;
        SelectedComment = comment;
        ReplyText = "";
        ShowReplyDialog = true;
    }

    private async Task SendReplyAsync()
    {
        if (SelectedComment == null || string.IsNullOrWhiteSpace(ReplyText)) return;

        try
        {
            IsBusy = true;

            // TODO: Send reply via social media API
            await Task.Delay(500);

            SelectedComment.IsReplied = true;
            SelectedComment.ReplyText = ReplyText;
            SelectedComment.RepliedAt = DateTime.Now;

            PendingComments.Remove(SelectedComment);
            RepliedCount++;
            PendingCount--;

            ShowReplyDialog = false;
            ReplyText = "";

            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "ตอบกลับความคิดเห็นสำเร็จ!" : "Reply sent successfully!",
                isThai ? "สำเร็จ" : "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task QuickReplyAsync(CommentItem? comment)
    {
        if (comment == null) return;

        // Auto-select template based on keywords
        foreach (var template in AutoReplyTemplates.Where(t => t.IsActive))
        {
            var keywords = template.Keywords.Split(',').Select(k => k.Trim().ToLower());
            if (keywords.Any(k => comment.Text.ToLower().Contains(k)))
            {
                SelectedComment = comment;
                ReplyText = template.ReplyText;
                await SendReplyAsync();
                return;
            }
        }

        // No matching template, open reply dialog
        OpenReplyDialog(comment);
    }

    private async Task LikeCommentAsync(CommentItem? comment)
    {
        if (comment == null) return;

        comment.IsLikedByPage = !comment.IsLikedByPage;
        if (comment.IsLikedByPage)
        {
            comment.LikeCount++;
        }
        else
        {
            comment.LikeCount--;
        }
    }

    private async Task HideCommentAsync(CommentItem? comment)
    {
        if (comment == null) return;

        var isThai = _localizationService.IsThaiLanguage;
        var result = MessageBox.Show(
            isThai ? "ต้องการซ่อนความคิดเห็นนี้?" : "Hide this comment?",
            isThai ? "ยืนยัน" : "Confirm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            comment.IsHidden = true;
            Comments.Remove(comment);
            PendingComments.Remove(comment);
        }
    }

    private async Task DeleteCommentAsync(CommentItem? comment)
    {
        if (comment == null) return;

        var isThai = _localizationService.IsThaiLanguage;
        var result = MessageBox.Show(
            isThai ? "ต้องการลบความคิดเห็นนี้? (ไม่สามารถย้อนกลับได้)" : "Delete this comment? (Cannot be undone)",
            isThai ? "ยืนยันการลบ" : "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // TODO: Delete via API
            Comments.Remove(comment);
            PendingComments.Remove(comment);
            TotalComments--;
        }
    }

    private async Task MarkAsSpamAsync(CommentItem? comment)
    {
        if (comment == null) return;

        comment.IsSpam = true;
        comment.IsHidden = true;
        Comments.Remove(comment);
        PendingComments.Remove(comment);

        var isThai = _localizationService.IsThaiLanguage;
        MessageBox.Show(
            isThai ? "ทำเครื่องหมายเป็นสแปมแล้ว" : "Marked as spam",
            isThai ? "สำเร็จ" : "Success",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void UseTemplate(AutoReplyTemplate? template)
    {
        if (template == null || SelectedComment == null) return;
        ReplyText = template.ReplyText;
    }

    private void DeleteTemplate(AutoReplyTemplate? template)
    {
        if (template == null) return;
        AutoReplyTemplates.Remove(template);
    }

    private void SaveAutoReplyTemplate()
    {
        if (string.IsNullOrWhiteSpace(NewTemplateName) || string.IsNullOrWhiteSpace(NewTemplateReply))
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "กรุณากรอกข้อมูลให้ครบ" : "Please fill in all fields",
                isThai ? "ข้อมูลไม่ครบ" : "Missing Information",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        AutoReplyTemplates.Add(new AutoReplyTemplate
        {
            Id = AutoReplyTemplates.Count + 1,
            Name = NewTemplateName,
            Keywords = NewTemplateKeywords,
            ReplyText = NewTemplateReply,
            IsActive = true
        });

        ShowAutoReplyDialog = false;
        ClearTemplateForm();
    }

    private void ClearTemplateForm()
    {
        NewTemplateName = "";
        NewTemplateKeywords = "";
        NewTemplateReply = "";
    }

    private void MarkAllAsRead()
    {
        foreach (var comment in Comments)
        {
            comment.IsRead = true;
        }

        var isThai = _localizationService.IsThaiLanguage;
        MessageBox.Show(
            isThai ? "ทำเครื่องหมายอ่านแล้วทั้งหมด" : "All marked as read",
            isThai ? "สำเร็จ" : "Success",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ExportComments()
    {
        var isThai = _localizationService.IsThaiLanguage;
        MessageBox.Show(
            isThai ? $"Export {Comments.Count} ความคิดเห็นเป็น CSV" : $"Export {Comments.Count} comments to CSV",
            isThai ? "Export สำเร็จ" : "Export Complete",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

// Supporting Models
public class CommentItem
{
    public string Id { get; set; } = "";
    public SocialPlatform Platform { get; set; }
    public string AuthorName { get; set; } = "";
    public string AuthorAvatar { get; set; } = "";
    public string Text { get; set; } = "";
    public string PostTitle { get; set; } = "";
    public string PostId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public CommentSentiment Sentiment { get; set; }
    public bool IsRead { get; set; }
    public bool IsReplied { get; set; }
    public string ReplyText { get; set; } = "";
    public DateTime? RepliedAt { get; set; }
    public int LikeCount { get; set; }
    public bool IsLikedByPage { get; set; }
    public bool IsHidden { get; set; }
    public bool IsSpam { get; set; }

    public string TimeAgo
    {
        get
        {
            var diff = DateTime.Now - CreatedAt;
            if (diff.TotalMinutes < 1) return "เมื่อสักครู่";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} นาทีที่แล้ว";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} ชั่วโมงที่แล้ว";
            return $"{(int)diff.TotalDays} วันที่แล้ว";
        }
    }
}

public class AutoReplyTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Keywords { get; set; } = "";
    public string ReplyText { get; set; } = "";
    public bool IsActive { get; set; }
}

public enum CommentSentiment
{
    Positive,
    Neutral,
    Negative
}

public enum CommentFilter
{
    All,
    Unread,
    Pending,
    Positive,
    Negative
}
