using System.Collections.ObjectModel;
using System.Windows;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services.Data;

namespace MyPostXAgent.UI.ViewModels;

public class SchedulerViewModel : BaseViewModel
{
    private readonly DatabaseService _database;

    public ObservableCollection<Post> ScheduledPosts { get; } = new();
    public ObservableCollection<Post> DraftPosts { get; } = new();

    private DateTime _selectedDate = DateTime.Today;
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                _ = LoadScheduledPostsAsync();
            }
        }
    }

    private Post? _selectedPost;
    public Post? SelectedPost
    {
        get => _selectedPost;
        set => SetProperty(ref _selectedPost, value);
    }

    private bool _showScheduleDialog;
    public bool ShowScheduleDialog
    {
        get => _showScheduleDialog;
        set => SetProperty(ref _showScheduleDialog, value);
    }

    // Schedule Form
    private Post? _postToSchedule;
    public Post? PostToSchedule
    {
        get => _postToSchedule;
        set => SetProperty(ref _postToSchedule, value);
    }

    private DateTime _scheduleDate = DateTime.Today;
    public DateTime ScheduleDate
    {
        get => _scheduleDate;
        set => SetProperty(ref _scheduleDate, value);
    }

    private int _scheduleHour = 9;
    public int ScheduleHour
    {
        get => _scheduleHour;
        set => SetProperty(ref _scheduleHour, value);
    }

    private int _scheduleMinute = 0;
    public int ScheduleMinute
    {
        get => _scheduleMinute;
        set => SetProperty(ref _scheduleMinute, value);
    }

    // Commands
    public RelayCommand RefreshCommand { get; }
    public RelayCommand<Post> SchedulePostCommand { get; }
    public RelayCommand<Post> UnschedulePostCommand { get; }
    public RelayCommand<Post> PostNowCommand { get; }
    public RelayCommand ConfirmScheduleCommand { get; }
    public RelayCommand CancelScheduleCommand { get; }

    public SchedulerViewModel(DatabaseService database)
    {
        _database = database;

        RefreshCommand = new RelayCommand(async () => await RefreshAllAsync());
        SchedulePostCommand = new RelayCommand<Post>(OpenScheduleDialog);
        UnschedulePostCommand = new RelayCommand<Post>(async post => await UnschedulePostAsync(post));
        PostNowCommand = new RelayCommand<Post>(async post => await PostNowAsync(post));
        ConfirmScheduleCommand = new RelayCommand(async () => await ConfirmScheduleAsync());
        CancelScheduleCommand = new RelayCommand(CancelSchedule);

        // Load initial data
        _ = RefreshAllAsync();
    }

    private async Task RefreshAllAsync()
    {
        await Task.WhenAll(LoadScheduledPostsAsync(), LoadDraftPostsAsync());
    }

    public async Task LoadScheduledPostsAsync()
    {
        try
        {
            IsBusy = true;
            var posts = await _database.GetPostsAsync(PostStatus.Scheduled);

            // Filter by selected date
            var filteredPosts = posts.Where(p =>
                p.ScheduledAt?.Date == SelectedDate.Date).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                ScheduledPosts.Clear();
                foreach (var post in filteredPosts.OrderBy(p => p.ScheduledAt))
                {
                    ScheduledPosts.Add(post);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading scheduled posts: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadDraftPostsAsync()
    {
        try
        {
            var posts = await _database.GetPostsAsync(PostStatus.Draft);

            Application.Current.Dispatcher.Invoke(() =>
            {
                DraftPosts.Clear();
                foreach (var post in posts.OrderByDescending(p => p.CreatedAt))
                {
                    DraftPosts.Add(post);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading draft posts: {ex.Message}");
        }
    }

    private void OpenScheduleDialog(Post? post)
    {
        if (post == null) return;

        PostToSchedule = post;
        ScheduleDate = DateTime.Today.AddDays(1);
        ScheduleHour = 9;
        ScheduleMinute = 0;
        ShowScheduleDialog = true;
    }

    private async Task ConfirmScheduleAsync()
    {
        if (PostToSchedule == null) return;

        try
        {
            var scheduledTime = new DateTime(
                ScheduleDate.Year, ScheduleDate.Month, ScheduleDate.Day,
                ScheduleHour, ScheduleMinute, 0);

            if (scheduledTime <= DateTime.Now)
            {
                MessageBox.Show("เวลาที่เลือกต้องเป็นอนาคต", "เวลาไม่ถูกต้อง", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PostToSchedule.ScheduledAt = scheduledTime;
            PostToSchedule.Status = PostStatus.Scheduled;

            await _database.UpdatePostAsync(PostToSchedule);

            // Move from drafts to scheduled
            DraftPosts.Remove(PostToSchedule);
            if (PostToSchedule.ScheduledAt?.Date == SelectedDate.Date)
            {
                ScheduledPosts.Add(PostToSchedule);
            }

            ShowScheduleDialog = false;
            MessageBox.Show($"ตั้งเวลาโพสต์สำเร็จ!\nกำหนดโพสต์: {scheduledTime:dd/MM/yyyy HH:mm}", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelSchedule()
    {
        ShowScheduleDialog = false;
        PostToSchedule = null;
    }

    private async Task UnschedulePostAsync(Post? post)
    {
        if (post == null) return;

        var result = MessageBox.Show(
            "ต้องการยกเลิกการตั้งเวลาโพสต์นี้หรือไม่?",
            "ยืนยัน",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                post.ScheduledAt = null;
                post.Status = PostStatus.Draft;

                await _database.UpdatePostAsync(post);

                ScheduledPosts.Remove(post);
                DraftPosts.Insert(0, post);

                MessageBox.Show("ยกเลิกการตั้งเวลาสำเร็จ", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task PostNowAsync(Post? post)
    {
        if (post == null) return;

        var result = MessageBox.Show(
            "ต้องการโพสต์ทันทีหรือไม่?",
            "ยืนยัน",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                post.Status = PostStatus.Posting;
                await _database.UpdatePostAsync(post);

                ScheduledPosts.Remove(post);
                DraftPosts.Remove(post);

                // TODO: Actually post via AI Manager
                // For now, simulate success
                post.Status = PostStatus.Posted;
                post.PostedAt = DateTime.UtcNow;
                await _database.UpdatePostAsync(post);

                MessageBox.Show("โพสต์สำเร็จ!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
