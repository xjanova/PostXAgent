using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace AIManager.UI.Views.Pages;

public partial class ApiKeysPage : Page
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private ObservableCollection<ApiKeyViewModel> _apiKeys = new();

    public ApiKeysPage()
    {
        InitializeComponent();

        _httpClient = new HttpClient();
        _apiBaseUrl = "http://localhost:5000/api";

        DgApiKeys.ItemsSource = _apiKeys;

        Loaded += ApiKeysPage_Loaded;
    }

    private async void ApiKeysPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadApiKeysAsync();
    }

    private async Task LoadApiKeysAsync()
    {
        try
        {
            TxtStatus.Text = "Loading API keys...";
            _apiKeys.Clear();

            // Note: In production, this would use the local ApiKeyService directly
            // For now, we'll load from the local file
            var keysFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AIManager", "api_keys.json"
            );

            if (File.Exists(keysFile))
            {
                var json = await File.ReadAllTextAsync(keysFile);
                var keys = JsonSerializer.Deserialize<Dictionary<string, ApiKeyData>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (keys != null)
                {
                    foreach (var kvp in keys)
                    {
                        _apiKeys.Add(new ApiKeyViewModel(kvp.Value));
                    }
                }
            }

            TxtStatus.Text = "Ready";
            TxtKeyCount.Text = $"Total: {_apiKeys.Count} keys";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error: {ex.Message}";
        }
    }

    private void RefreshKeys_Click(object sender, RoutedEventArgs e)
    {
        _ = LoadApiKeysAsync();
    }

    private void CreateKey_Click(object sender, RoutedEventArgs e)
    {
        // Reset form
        TxtNewKeyName.Text = "";
        TxtNewKeyDescription.Text = "";
        TxtNewKeyAllowedIps.Text = "";
        ChkScopeAll.IsChecked = false;
        ChkScopeAdmin.IsChecked = false;
        ChkScopeTasks.IsChecked = true;
        ChkScopeWorkers.IsChecked = false;
        ChkScopeContent.IsChecked = true;
        ChkScopeImages.IsChecked = true;
        ChkScopeAutomation.IsChecked = false;
        ChkScopeAnalytics.IsChecked = true;
        ChkScopeReadOnly.IsChecked = false;
        ChkHasExpiry.IsChecked = false;
        DpExpiry.SelectedDate = DateTime.Now.AddMonths(1);

        CreateKeyDialog.Visibility = Visibility.Visible;
    }

    private void CancelCreateKey_Click(object sender, RoutedEventArgs e)
    {
        CreateKeyDialog.Visibility = Visibility.Collapsed;
    }

    private async void ConfirmCreateKey_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNewKeyName.Text))
        {
            MessageBox.Show("Please enter a key name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Build scopes list
            var scopes = new List<string>();
            if (ChkScopeAll.IsChecked == true) scopes.Add("all");
            if (ChkScopeAdmin.IsChecked == true) scopes.Add("admin");
            if (ChkScopeTasks.IsChecked == true) scopes.Add("tasks");
            if (ChkScopeWorkers.IsChecked == true) scopes.Add("workers");
            if (ChkScopeContent.IsChecked == true) scopes.Add("content");
            if (ChkScopeImages.IsChecked == true) scopes.Add("images");
            if (ChkScopeAutomation.IsChecked == true) scopes.Add("automation");
            if (ChkScopeAnalytics.IsChecked == true) scopes.Add("analytics");
            if (ChkScopeReadOnly.IsChecked == true) scopes.Add("read");

            if (scopes.Count == 0) scopes.Add("read");

            // Build allowed IPs
            var allowedIps = string.IsNullOrWhiteSpace(TxtNewKeyAllowedIps.Text)
                ? Array.Empty<string>()
                : TxtNewKeyAllowedIps.Text.Split(',').Select(ip => ip.Trim()).ToArray();

            // Create key using local service
            var service = new Core.Services.ApiKeyService();
            var key = service.GenerateKey(
                TxtNewKeyName.Text.Trim(),
                string.IsNullOrWhiteSpace(TxtNewKeyDescription.Text) ? null : TxtNewKeyDescription.Text.Trim(),
                allowedIps.Length > 0 ? allowedIps : null,
                scopes.ToArray()
            );

            if (ChkHasExpiry.IsChecked == true && DpExpiry.SelectedDate.HasValue)
            {
                service.UpdateKey(key.Id, expiresAt: DpExpiry.SelectedDate.Value);
            }

            CreateKeyDialog.Visibility = Visibility.Collapsed;

            // Show the generated key
            TxtGeneratedKey.Text = key.PlainKey;
            RunKeyExample.Text = key.PlainKey?[..Math.Min(20, key.PlainKey?.Length ?? 0)] + "...";
            ShowKeyDialog.Visibility = Visibility.Visible;

            await LoadApiKeysAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create API key: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyKey_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(TxtGeneratedKey.Text);
        TxtStatus.Text = "API key copied to clipboard!";
    }

    private void CloseShowKeyDialog_Click(object sender, RoutedEventArgs e)
    {
        ShowKeyDialog.Visibility = Visibility.Collapsed;
    }

    private void EditKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ApiKeyViewModel key)
        {
            // TODO: Implement edit dialog
            MessageBox.Show($"Edit key: {key.Name}\n\nEdit functionality coming soon.",
                "Edit API Key", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void RegenerateKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ApiKeyViewModel key)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to regenerate the key '{key.Name}'?\n\n" +
                "The old key will stop working immediately.",
                "Confirm Regenerate",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var service = new Core.Services.ApiKeyService();
                    var newKey = service.RegenerateKey(key.Id);

                    if (newKey != null)
                    {
                        TxtGeneratedKey.Text = newKey.PlainKey;
                        RunKeyExample.Text = newKey.PlainKey?[..Math.Min(20, newKey.PlainKey?.Length ?? 0)] + "...";
                        ShowKeyDialog.Visibility = Visibility.Visible;

                        await LoadApiKeysAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to regenerate key: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async void ToggleKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ApiKeyViewModel key)
        {
            try
            {
                var service = new Core.Services.ApiKeyService();
                var currentKey = service.GetKeyById(key.Id);
                if (currentKey != null)
                {
                    service.UpdateKey(key.Id, isActive: !currentKey.IsActive);
                    await LoadApiKeysAsync();
                    TxtStatus.Text = $"Key '{key.Name}' {(currentKey.IsActive ? "disabled" : "enabled")}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to toggle key: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void DeleteKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ApiKeyViewModel key)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete the key '{key.Name}'?\n\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var service = new Core.Services.ApiKeyService();
                    service.RevokeKey(key.Id);
                    await LoadApiKeysAsync();
                    TxtStatus.Text = $"Key '{key.Name}' deleted";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete key: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}

// View Model for API Key
public class ApiKeyViewModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string KeyPrefix { get; set; } = "";
    public string[] Scopes { get; set; } = Array.Empty<string>();
    public string[] AllowedIps { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; }
    public long UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public ApiKeyViewModel() { }

    public ApiKeyViewModel(ApiKeyData data)
    {
        Id = data.Id ?? "";
        Name = data.Name ?? "";
        Description = data.Description;
        KeyPrefix = data.KeyPrefix ?? "";
        Scopes = data.Scopes ?? Array.Empty<string>();
        AllowedIps = data.AllowedIps ?? Array.Empty<string>();
        IsActive = data.IsActive;
        UsageCount = data.UsageCount;
        CreatedAt = data.CreatedAt;
        LastUsedAt = data.LastUsedAt;
        ExpiresAt = data.ExpiresAt;
    }

    public string ScopesDisplay => Scopes.Length > 0
        ? string.Join(", ", Scopes.Take(3)) + (Scopes.Length > 3 ? "..." : "")
        : "None";

    public string ScopesTooltip => Scopes.Length > 0
        ? string.Join("\n", Scopes)
        : "No scopes assigned";

    public string IpDisplay => AllowedIps.Length == 0
        ? "Any"
        : AllowedIps.Length == 1 ? AllowedIps[0] : $"{AllowedIps.Length} IPs";

    public string IpTooltip => AllowedIps.Length == 0
        ? "No IP restrictions"
        : string.Join("\n", AllowedIps);

    public string CreatedAtDisplay => CreatedAt.ToString("yyyy-MM-dd HH:mm");

    public string LastUsedDisplay => LastUsedAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never";

    public string ExpiresAtDisplay => ExpiresAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never";

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}

// Data model for JSON deserialization
public class ApiKeyData
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? KeyPrefix { get; set; }
    public string[]? Scopes { get; set; }
    public string[]? AllowedIps { get; set; }
    public bool IsActive { get; set; }
    public long UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
