using System.Collections.ObjectModel;
using System.Windows;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services;
using MyPostXAgent.Core.Services.Data;

namespace MyPostXAgent.UI.ViewModels;

public class PostsViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly LocalizationService _localizationService;

    public ObservableCollection<Post> Posts { get; } = new();
    public ObservableCollection<SocialPlatform> Platforms { get; } = new();
    public ObservableCollection<PostStatus> StatusFilters { get; } = new();

    private Post? _selectedPost;
    public Post? SelectedPost
    {
        get => _selectedPost;
        set => SetProperty(ref _selectedPost, value);
    }

    private PostStatus? _selectedStatusFilter;
    public PostStatus? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                _ = LoadPostsAsync();
            }
        }
    }

    private bool _showCreateDialog;
    public bool ShowCreateDialog
    {
        get => _showCreateDialog;
        set => SetProperty(ref _showCreateDialog, value);
    }

    // New Post Form
    private SocialPlatform _newPostPlatform = SocialPlatform.Facebook;
    public SocialPlatform NewPostPlatform
    {
        get => _newPostPlatform;
        set => SetProperty(ref _newPostPlatform, value);
    }

    private string _newPostContent = "";
    public string NewPostContent
    {
        get => _newPostContent;
        set => SetProperty(ref _newPostContent, value);
    }

    private string _newPostHashtags = "";
    public string NewPostHashtags
    {
        get => _newPostHashtags;
        set => SetProperty(ref _newPostHashtags, value);
    }

    private ContentType _newPostContentType = ContentType.Text;
    public ContentType NewPostContentType
    {
        get => _newPostContentType;
        set => SetProperty(ref _newPostContentType, value);
    }

    // Commands
    public RelayCommand RefreshCommand { get; }
    public RelayCommand CreatePostCommand { get; }
    public RelayCommand<Post> EditPostCommand { get; }
    public RelayCommand<Post> DeletePostCommand { get; }
    public RelayCommand<Post> DuplicatePostCommand { get; }
    public RelayCommand SaveNewPostCommand { get; }
    public RelayCommand CancelCreateCommand { get; }

    public PostsViewModel(DatabaseService database, LocalizationService localizationService)
    {
        _database = database;
        _localizationService = localizationService;

        Title = LocalizationStrings.Nav.Posts(_localizationService.IsThaiLanguage);

        // Populate filters
        foreach (SocialPlatform platform in Enum.GetValues(typeof(SocialPlatform)))
        {
            Platforms.Add(platform);
        }

        foreach (PostStatus status in Enum.GetValues(typeof(PostStatus)))
        {
            StatusFilters.Add(status);
        }

        // Commands
        RefreshCommand = new RelayCommand(async () => await LoadPostsAsync());
        CreatePostCommand = new RelayCommand(() => ShowCreateDialog = true);
        EditPostCommand = new RelayCommand<Post>(EditPost);
        DeletePostCommand = new RelayCommand<Post>(async post => await DeletePostAsync(post));
        DuplicatePostCommand = new RelayCommand<Post>(async post => await DuplicatePostAsync(post));
        SaveNewPostCommand = new RelayCommand(async () => await SaveNewPostAsync());
        CancelCreateCommand = new RelayCommand(CancelCreate);

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Load initial data
        _ = LoadPostsAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Title = LocalizationStrings.Nav.Posts(_localizationService.IsThaiLanguage);
    }

    public async Task LoadPostsAsync()
    {
        try
        {
            IsBusy = true;
            var posts = await _database.GetPostsAsync(SelectedStatusFilter);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Posts.Clear();
                foreach (var post in posts)
                {
                    Posts.Add(post);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading posts: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void EditPost(Post? post)
    {
        if (post == null) return;
        SelectedPost = post;
        // TODO: Show edit dialog
    }

    private async Task DeletePostAsync(Post? post)
    {
        if (post == null) return;

        var result = MessageBox.Show(
            $"ต้องการลบโพสต์นี้หรือไม่?\n\n{post.Content[..Math.Min(50, post.Content.Length)]}...",
            "ยืนยันการลบ",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _database.DeletePostAsync(post.Id);
                Posts.Remove(post);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task DuplicatePostAsync(Post? post)
    {
        if (post == null) return;

        try
        {
            var newPost = new Post
            {
                Platform = post.Platform,
                Content = post.Content,
                ContentType = post.ContentType,
                MediaPaths = new List<string>(post.MediaPaths),
                Hashtags = new List<string>(post.Hashtags),
                Status = PostStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            var id = await _database.AddPostAsync(newPost);
            newPost.Id = id;
            Posts.Insert(0, newPost);

            MessageBox.Show("คัดลอกโพสต์สำเร็จ!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveNewPostAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPostContent))
        {
            MessageBox.Show("กรุณากรอกเนื้อหาโพสต์", "ข้อมูลไม่ครบ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var hashtags = string.IsNullOrWhiteSpace(NewPostHashtags)
                ? new List<string>()
                : NewPostHashtags.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(h => h.StartsWith("#") ? h : $"#{h}")
                    .ToList();

            var post = new Post
            {
                Platform = NewPostPlatform,
                Content = NewPostContent,
                ContentType = NewPostContentType,
                Hashtags = hashtags,
                Status = PostStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            var id = await _database.AddPostAsync(post);
            post.Id = id;

            Posts.Insert(0, post);
            CancelCreate();

            MessageBox.Show("สร้างโพสต์สำเร็จ!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelCreate()
    {
        ShowCreateDialog = false;
        NewPostContent = "";
        NewPostHashtags = "";
        NewPostPlatform = SocialPlatform.Facebook;
        NewPostContentType = ContentType.Text;
    }
}
