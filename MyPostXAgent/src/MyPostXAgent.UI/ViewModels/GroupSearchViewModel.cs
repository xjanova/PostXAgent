using System.Collections.ObjectModel;
using System.Windows;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services;

namespace MyPostXAgent.UI.ViewModels;

/// <summary>
/// ViewModel for Group Search Page - Find and join social media groups
/// </summary>
public class GroupSearchViewModel : BaseViewModel
{
    private readonly LocalizationService _localizationService;

    // Search Results
    public ObservableCollection<SocialGroup> SearchResults { get; } = new();
    public ObservableCollection<SocialGroup> JoinedGroups { get; } = new();
    public ObservableCollection<SocialPlatform> Platforms { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    // Search Filters
    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    private SocialPlatform _selectedPlatform = SocialPlatform.Facebook;
    public SocialPlatform SelectedPlatform
    {
        get => _selectedPlatform;
        set => SetProperty(ref _selectedPlatform, value);
    }

    private string _selectedCategory = "ทั้งหมด";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    private int _minMembers = 0;
    public int MinMembers
    {
        get => _minMembers;
        set => SetProperty(ref _minMembers, value);
    }

    private bool _onlyOpenGroups = false;
    public bool OnlyOpenGroups
    {
        get => _onlyOpenGroups;
        set => SetProperty(ref _onlyOpenGroups, value);
    }

    // Selected Group
    private SocialGroup? _selectedGroup;
    public SocialGroup? SelectedGroup
    {
        get => _selectedGroup;
        set => SetProperty(ref _selectedGroup, value);
    }

    // Search Status
    private bool _isSearching;
    public bool IsSearching
    {
        get => _isSearching;
        set => SetProperty(ref _isSearching, value);
    }

    private int _searchProgress;
    public int SearchProgress
    {
        get => _searchProgress;
        set => SetProperty(ref _searchProgress, value);
    }

    private string _searchStatus = "";
    public string SearchStatus
    {
        get => _searchStatus;
        set => SetProperty(ref _searchStatus, value);
    }

    private int _totalResults;
    public int TotalResults
    {
        get => _totalResults;
        set => SetProperty(ref _totalResults, value);
    }

    // Tab Selection
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    // Commands
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand<SocialGroup> ViewGroupCommand { get; }
    public RelayCommand<SocialGroup> JoinGroupCommand { get; }
    public RelayCommand<SocialGroup> LeaveGroupCommand { get; }
    public RelayCommand<SocialGroup> PostToGroupCommand { get; }
    public RelayCommand RefreshJoinedCommand { get; }
    public RelayCommand ExportGroupsCommand { get; }

    public GroupSearchViewModel(LocalizationService localizationService)
    {
        _localizationService = localizationService;
        Title = LocalizationStrings.Nav.Groups(_localizationService.IsThaiLanguage);

        // Initialize collections
        InitializePlatforms();
        InitializeCategories();

        // Commands
        SearchCommand = new RelayCommand(async () => await SearchGroupsAsync());
        ClearSearchCommand = new RelayCommand(ClearSearch);
        ViewGroupCommand = new RelayCommand<SocialGroup>(ViewGroup);
        JoinGroupCommand = new RelayCommand<SocialGroup>(async g => await JoinGroupAsync(g));
        LeaveGroupCommand = new RelayCommand<SocialGroup>(async g => await LeaveGroupAsync(g));
        PostToGroupCommand = new RelayCommand<SocialGroup>(PostToGroup);
        RefreshJoinedCommand = new RelayCommand(async () => await LoadJoinedGroupsAsync());
        ExportGroupsCommand = new RelayCommand(ExportGroups);

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Load initial data
        _ = LoadJoinedGroupsAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Title = LocalizationStrings.Nav.Groups(_localizationService.IsThaiLanguage);
    }

    private void InitializePlatforms()
    {
        Platforms.Add(SocialPlatform.Facebook);
        Platforms.Add(SocialPlatform.Line);
        Platforms.Add(SocialPlatform.LinkedIn);
    }

    private void InitializeCategories()
    {
        var isThai = _localizationService.IsThaiLanguage;
        Categories.Add(isThai ? "ทั้งหมด" : "All");
        Categories.Add(isThai ? "ธุรกิจ" : "Business");
        Categories.Add(isThai ? "การตลาด" : "Marketing");
        Categories.Add(isThai ? "เทคโนโลยี" : "Technology");
        Categories.Add(isThai ? "สุขภาพ" : "Health");
        Categories.Add(isThai ? "อาหาร" : "Food");
        Categories.Add(isThai ? "ท่องเที่ยว" : "Travel");
        Categories.Add(isThai ? "แฟชั่น" : "Fashion");
        Categories.Add(isThai ? "ความงาม" : "Beauty");
        Categories.Add(isThai ? "รถยนต์" : "Automotive");
        Categories.Add(isThai ? "อสังหาริมทรัพย์" : "Real Estate");
        Categories.Add(isThai ? "การเงิน" : "Finance");
        Categories.Add(isThai ? "การศึกษา" : "Education");
    }

    private async Task SearchGroupsAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "กรุณาใส่คำค้นหา" : "Please enter a search query",
                isThai ? "ข้อมูลไม่ครบ" : "Missing Information",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsSearching = true;
            SearchProgress = 0;
            SearchResults.Clear();

            var isThai = _localizationService.IsThaiLanguage;

            // Simulate searching multiple pages
            for (int page = 1; page <= 5; page++)
            {
                SearchStatus = isThai
                    ? $"กำลังค้นหาหน้า {page}..."
                    : $"Searching page {page}...";
                SearchProgress = page * 20;

                await Task.Delay(500);

                // Simulate adding results
                var result = new SocialGroup
                {
                    Id = page,
                    Name = $"{SearchQuery} Group {page}",
                    Platform = SelectedPlatform,
                    MemberCount = new Random().Next(1000, 100000),
                    Category = SelectedCategory,
                    IsPublic = page % 2 == 0,
                    PostsPerDay = new Random().Next(5, 50),
                    Description = $"กลุ่มเกี่ยวกับ {SearchQuery} สำหรับคนไทย",
                    LastActivity = DateTime.Now.AddHours(-new Random().Next(1, 48)),
                    JoinedAt = null
                };

                if (MinMembers == 0 || result.MemberCount >= MinMembers)
                {
                    if (!OnlyOpenGroups || result.IsPublic)
                    {
                        Application.Current.Dispatcher.Invoke(() => SearchResults.Add(result));
                    }
                }
            }

            TotalResults = SearchResults.Count;
            SearchStatus = isThai
                ? $"พบ {TotalResults} กลุ่ม"
                : $"Found {TotalResults} groups";
        }
        finally
        {
            IsSearching = false;
            SearchProgress = 100;
        }
    }

    private void ClearSearch()
    {
        SearchQuery = "";
        SearchResults.Clear();
        TotalResults = 0;
        SearchStatus = "";
    }

    private async Task LoadJoinedGroupsAsync()
    {
        try
        {
            IsBusy = true;

            // TODO: Load from database
            await Task.Delay(300);

            Application.Current.Dispatcher.Invoke(() =>
            {
                JoinedGroups.Clear();
                // Sample joined groups
                JoinedGroups.Add(new SocialGroup
                {
                    Id = 101,
                    Name = "Thai Marketing Pro",
                    Platform = SocialPlatform.Facebook,
                    MemberCount = 45000,
                    Category = "การตลาด",
                    IsPublic = true,
                    PostsPerDay = 25,
                    JoinedAt = DateTime.Now.AddDays(-30)
                });
                JoinedGroups.Add(new SocialGroup
                {
                    Id = 102,
                    Name = "Startup Thailand",
                    Platform = SocialPlatform.Facebook,
                    MemberCount = 120000,
                    Category = "ธุรกิจ",
                    IsPublic = true,
                    PostsPerDay = 50,
                    JoinedAt = DateTime.Now.AddDays(-60)
                });
                JoinedGroups.Add(new SocialGroup
                {
                    Id = 103,
                    Name = "LINE OA Community",
                    Platform = SocialPlatform.Line,
                    MemberCount = 8500,
                    Category = "การตลาด",
                    IsPublic = false,
                    PostsPerDay = 15,
                    JoinedAt = DateTime.Now.AddDays(-15)
                });
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ViewGroup(SocialGroup? group)
    {
        if (group == null) return;

        SelectedGroup = group;

        var isThai = _localizationService.IsThaiLanguage;
        var message = isThai
            ? $"กลุ่ม: {group.Name}\n" +
              $"สมาชิก: {group.MemberCount:N0}\n" +
              $"หมวดหมู่: {group.Category}\n" +
              $"โพสต์/วัน: ~{group.PostsPerDay}\n" +
              $"ประเภท: {(group.IsPublic ? "สาธารณะ" : "ส่วนตัว")}"
            : $"Group: {group.Name}\n" +
              $"Members: {group.MemberCount:N0}\n" +
              $"Category: {group.Category}\n" +
              $"Posts/Day: ~{group.PostsPerDay}\n" +
              $"Type: {(group.IsPublic ? "Public" : "Private")}";

        MessageBox.Show(message, isThai ? "รายละเอียดกลุ่ม" : "Group Details",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task JoinGroupAsync(SocialGroup? group)
    {
        if (group == null) return;

        var isThai = _localizationService.IsThaiLanguage;
        var result = MessageBox.Show(
            isThai ? $"ต้องการเข้าร่วมกลุ่ม '{group.Name}'?" : $"Join group '{group.Name}'?",
            isThai ? "ยืนยัน" : "Confirm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                IsBusy = true;

                // Simulate joining
                await Task.Delay(1000);

                group.JoinedAt = DateTime.Now;

                // Move to joined groups
                SearchResults.Remove(group);
                JoinedGroups.Insert(0, group);

                MessageBox.Show(
                    isThai ? $"เข้าร่วมกลุ่ม '{group.Name}' สำเร็จ!" : $"Successfully joined '{group.Name}'!",
                    isThai ? "สำเร็จ" : "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private async Task LeaveGroupAsync(SocialGroup? group)
    {
        if (group == null) return;

        var isThai = _localizationService.IsThaiLanguage;
        var result = MessageBox.Show(
            isThai ? $"ต้องการออกจากกลุ่ม '{group.Name}'?" : $"Leave group '{group.Name}'?",
            isThai ? "ยืนยัน" : "Confirm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                IsBusy = true;

                // Simulate leaving
                await Task.Delay(500);

                JoinedGroups.Remove(group);

                MessageBox.Show(
                    isThai ? $"ออกจากกลุ่ม '{group.Name}' แล้ว" : $"Left group '{group.Name}'",
                    isThai ? "สำเร็จ" : "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private void PostToGroup(SocialGroup? group)
    {
        if (group == null) return;

        var isThai = _localizationService.IsThaiLanguage;
        MessageBox.Show(
            isThai ? $"เปิดหน้าโพสต์ไปยังกลุ่ม '{group.Name}'" : $"Opening post dialog for '{group.Name}'",
            isThai ? "โพสต์ไปยังกลุ่ม" : "Post to Group",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        // TODO: Navigate to create post page with group pre-selected
    }

    private void ExportGroups()
    {
        if (JoinedGroups.Count == 0)
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "ไม่มีกลุ่มให้ export" : "No groups to export",
                isThai ? "ไม่มีข้อมูล" : "No Data",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // TODO: Export to CSV
        var isThai2 = _localizationService.IsThaiLanguage;
        MessageBox.Show(
            isThai2 ? $"Export {JoinedGroups.Count} กลุ่มเป็น CSV" : $"Export {JoinedGroups.Count} groups to CSV",
            isThai2 ? "Export สำเร็จ" : "Export Complete",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

// Supporting Model
public class SocialGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public SocialPlatform Platform { get; set; }
    public string GroupUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public int MemberCount { get; set; }
    public int PostsPerDay { get; set; }
    public bool IsPublic { get; set; }
    public DateTime? JoinedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public string ThumbnailUrl { get; set; } = "";

    public bool IsJoined => JoinedAt.HasValue;
    public string MemberCountFormatted => MemberCount >= 1000
        ? $"{MemberCount / 1000.0:N1}K"
        : MemberCount.ToString();
}
