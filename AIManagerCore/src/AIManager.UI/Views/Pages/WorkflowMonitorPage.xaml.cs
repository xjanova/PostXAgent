using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// Workflow Monitor Page
/// ดูและจัดการ Workflows และ Jobs แบบ Real-time
/// </summary>
public partial class WorkflowMonitorPage : Page
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private readonly DispatcherTimer _refreshTimer;

    public ObservableCollection<WorkflowViewModel> Workflows { get; } = new();
    public ObservableCollection<JobViewModel> Jobs { get; } = new();

    public WorkflowMonitorPage()
    {
        InitializeComponent();

        _apiBaseUrl = "http://localhost:5000/api/workflowruntime";
        _httpClient = new HttpClient();

        WorkflowsListBox.ItemsSource = Workflows;
        JobsListView.ItemsSource = Jobs;

        // Auto-refresh every 3 seconds
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _refreshTimer.Tick += async (s, e) => await RefreshDataAsync();

        Loaded += async (s, e) =>
        {
            await RefreshDataAsync();
            _refreshTimer.Start();
        };

        Unloaded += (s, e) => _refreshTimer.Stop();
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            await Task.WhenAll(
                LoadStatisticsAsync(),
                LoadWorkflowsAsync(),
                LoadJobsAsync()
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Refresh error: {ex.Message}");
        }
    }

    private async Task LoadStatisticsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<JsonElement>($"{_apiBaseUrl}/stats");

            if (response.TryGetProperty("data", out var data))
            {
                var workflows = data.GetProperty("workflows");
                var jobs = data.GetProperty("jobs");

                Dispatcher.Invoke(() =>
                {
                    TotalWorkflowsText.Text = workflows.GetProperty("total").GetInt32().ToString();
                    RunningJobsText.Text = jobs.GetProperty("running").GetInt32().ToString();
                    PendingJobsText.Text = jobs.GetProperty("pending").GetInt32().ToString();
                    FailedJobsText.Text = jobs.GetProperty("failed").GetInt32().ToString();
                    CompletedJobsText.Text = jobs.GetProperty("completed").GetInt32().ToString();
                });
            }
        }
        catch { }
    }

    private async Task LoadWorkflowsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<JsonElement>($"{_apiBaseUrl}/workflows");

            if (response.TryGetProperty("data", out var data))
            {
                var workflows = new List<WorkflowViewModel>();

                foreach (var item in data.EnumerateArray())
                {
                    workflows.Add(new WorkflowViewModel
                    {
                        Id = item.GetProperty("id").GetString() ?? "",
                        Name = item.GetProperty("name").GetString() ?? "",
                        Platform = item.GetProperty("platform").GetString() ?? "",
                        TaskType = item.GetProperty("taskType").GetString() ?? "",
                        CurrentVersion = item.GetProperty("currentVersion").GetInt32(),
                        IsActive = item.GetProperty("isActive").GetBoolean(),
                        TotalVersions = item.TryGetProperty("totalVersions", out var tv) ? tv.GetInt32() : 1
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    Workflows.Clear();
                    foreach (var wf in workflows)
                    {
                        Workflows.Add(wf);
                    }
                    WorkflowCountText.Text = $"({workflows.Count})";
                });
            }
        }
        catch { }
    }

    private async Task LoadJobsAsync()
    {
        try
        {
            var filter = "";
            var selectedFilter = Dispatcher.Invoke(() =>
                (JobFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString());

            if (!string.IsNullOrEmpty(selectedFilter) && selectedFilter != "All")
            {
                filter = $"?status={selectedFilter}";
            }

            var response = await _httpClient.GetFromJsonAsync<JsonElement>($"{_apiBaseUrl}/jobs{filter}");

            if (response.TryGetProperty("data", out var data))
            {
                var jobs = new List<JobViewModel>();

                foreach (var item in data.EnumerateArray())
                {
                    var statusStr = item.GetProperty("status").GetString() ?? "Pending";
                    var progress = item.GetProperty("progress");

                    jobs.Add(new JobViewModel
                    {
                        Id = item.GetProperty("id").GetString() ?? "",
                        WorkflowId = item.GetProperty("workflowId").GetString() ?? "",
                        WorkflowName = item.GetProperty("workflowName").GetString() ?? "",
                        WorkflowVersion = item.GetProperty("workflowVersion").GetInt32(),
                        Status = statusStr,
                        ProgressPercentage = progress.GetProperty("percentage").GetInt32(),
                        CurrentStep = progress.GetProperty("currentStep").GetString() ?? "",
                        Duration = item.TryGetProperty("duration", out var dur) && dur.ValueKind != JsonValueKind.Null
                            ? TimeSpan.FromSeconds(dur.GetDouble())
                            : null
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    Jobs.Clear();
                    foreach (var job in jobs)
                    {
                        Jobs.Add(job);
                    }
                    ActiveJobsCountText.Text = $"({jobs.Count(j => j.Status == "Running" || j.Status == "Pending")})";
                });
            }
        }
        catch { }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();
    }

    private async void CleanupButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_apiBaseUrl}/jobs/cleanup?hoursOld=24", null);
            if (response.IsSuccessStatusCode)
            {
                await RefreshDataAsync();
                MessageBox.Show("Cleaned up completed jobs older than 24 hours", "Cleanup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cleanup failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void WorkflowsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkflowsListBox.SelectedItem is WorkflowViewModel workflow)
        {
            await ShowWorkflowDetails(workflow.Id);
        }
    }

    private void JobsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (JobsListView.SelectedItem is JobViewModel job)
        {
            ShowJobDetails(job);
        }
    }

    private void JobFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            _ = LoadJobsAsync();
        }
    }

    private async Task ShowWorkflowDetails(string workflowId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<JsonElement>($"{_apiBaseUrl}/workflows/{workflowId}");

            if (response.TryGetProperty("data", out var data))
            {
                DetailsPanel.Children.Clear();

                // Header
                DetailsPanel.Children.Add(new TextBlock
                {
                    Text = data.GetProperty("name").GetString(),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                // Info
                AddDetailRow("Platform", data.GetProperty("platform").GetString() ?? "");
                AddDetailRow("Task Type", data.GetProperty("taskType").GetString() ?? "");
                AddDetailRow("Current Version", data.GetProperty("currentVersion").GetInt32().ToString());
                AddDetailRow("Status", data.GetProperty("isActive").GetBoolean() ? "Active" : "Inactive");

                // Steps
                if (data.TryGetProperty("workflow", out var workflow) &&
                    workflow.TryGetProperty("steps", out var steps))
                {
                    DetailsPanel.Children.Add(new TextBlock
                    {
                        Text = "Steps",
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 15, 0, 5)
                    });

                    var stepIndex = 0;
                    foreach (var step in steps.EnumerateArray())
                    {
                        stepIndex++;
                        var action = step.GetProperty("action").GetString();
                        var desc = step.TryGetProperty("description", out var d) ? d.GetString() : "";

                        var stepPanel = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(10),
                            Margin = new Thickness(0, 0, 0, 5)
                        };

                        var sp = new StackPanel();
                        sp.Children.Add(new TextBlock
                        {
                            Text = $"{stepIndex}. {action}",
                            FontWeight = FontWeights.SemiBold
                        });

                        if (!string.IsNullOrEmpty(desc))
                        {
                            sp.Children.Add(new TextBlock
                            {
                                Text = desc,
                                Foreground = new SolidColorBrush(Colors.Gray),
                                FontSize = 12
                            });
                        }

                        stepPanel.Child = sp;
                        DetailsPanel.Children.Add(stepPanel);
                    }
                }

                // Actions
                var actionsPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 15, 0, 0)
                };

                var editButton = new Button
                {
                    Content = "Edit Workflow",
                    Margin = new Thickness(0, 0, 10, 0),
                    Tag = workflowId
                };
                editButton.Click += EditWorkflow_Click;
                actionsPanel.Children.Add(editButton);

                var startJobButton = new Button
                {
                    Content = "Start Job",
                    Tag = workflowId
                };
                startJobButton.Click += StartJob_Click;
                actionsPanel.Children.Add(startJobButton);

                DetailsPanel.Children.Add(actionsPanel);
            }
        }
        catch (Exception ex)
        {
            DetailsPanel.Children.Clear();
            DetailsPanel.Children.Add(new TextBlock
            {
                Text = $"Error loading details: {ex.Message}",
                Foreground = new SolidColorBrush(Colors.Red)
            });
        }
    }

    private void ShowJobDetails(JobViewModel job)
    {
        DetailsPanel.Children.Clear();

        // Header
        DetailsPanel.Children.Add(new TextBlock
        {
            Text = $"Job: {job.Id.Substring(0, 8)}...",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Info
        AddDetailRow("Workflow", job.WorkflowName);
        AddDetailRow("Version", job.WorkflowVersion.ToString());
        AddDetailRow("Status", job.Status);
        AddDetailRow("Progress", job.ProgressText);
        AddDetailRow("Current Step", job.CurrentStep);
        AddDetailRow("Duration", job.DurationText);

        // Actions based on status
        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 15, 0, 0)
        };

        if (job.CanPauseResume)
        {
            var pauseResumeButton = new Button
            {
                Content = job.Status == "Paused" ? "Resume" : "Pause",
                Margin = new Thickness(0, 0, 10, 0),
                Tag = job.Id
            };
            pauseResumeButton.Click += PauseResumeJob_Click;
            actionsPanel.Children.Add(pauseResumeButton);
        }

        if (job.CanCancel)
        {
            var cancelButton = new Button
            {
                Content = "Cancel",
                Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                Foreground = new SolidColorBrush(Colors.White),
                Tag = job.Id
            };
            cancelButton.Click += CancelJob_Click;
            actionsPanel.Children.Add(cancelButton);
        }

        DetailsPanel.Children.Add(actionsPanel);
    }

    private void AddDetailRow(string label, string value)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 3, 0, 3)
        };

        panel.Children.Add(new TextBlock
        {
            Text = $"{label}:",
            FontWeight = FontWeights.SemiBold,
            Width = 100
        });

        panel.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
        });

        DetailsPanel.Children.Add(panel);
    }

    private void EditWorkflow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string workflowId)
        {
            // Navigate to workflow editor (to be implemented)
            MessageBox.Show($"Edit workflow: {workflowId}\n\nWorkflow Editor will be implemented in next phase.",
                "Edit Workflow", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void StartJob_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string workflowId)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/jobs/start",
                    new { workflowId });

                if (response.IsSuccessStatusCode)
                {
                    await RefreshDataAsync();
                    MessageBox.Show("Job started successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to start job", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void PauseResumeJob_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string jobId)
        {
            var job = Jobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null) return;

            var action = job.Status == "Paused" ? "resume" : "pause";

            try
            {
                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/jobs/{jobId}/{action}", null);

                if (response.IsSuccessStatusCode)
                {
                    await RefreshDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void CancelJob_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string jobId)
        {
            var result = MessageBox.Show("Are you sure you want to cancel this job?", "Confirm Cancel",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var response = await _httpClient.PostAsync($"{_apiBaseUrl}/jobs/{jobId}/cancel", null);

                    if (response.IsSuccessStatusCode)
                    {
                        await RefreshDataAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}

#region ViewModels

public class WorkflowViewModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
    public int CurrentVersion { get; set; }
    public bool IsActive { get; set; }
    public int TotalVersions { get; set; }

    public string VersionText => $"v{CurrentVersion} ({TotalVersions} versions)";
}

public class JobViewModel
{
    public string Id { get; set; } = "";
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public int WorkflowVersion { get; set; }
    public string Status { get; set; } = "Pending";
    public int ProgressPercentage { get; set; }
    public string CurrentStep { get; set; } = "";
    public TimeSpan? Duration { get; set; }

    public string StatusText => Status;

    public Brush StatusColor => Status switch
    {
        "Running" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
        "Pending" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
        "Paused" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
        "Completed" => new SolidColorBrush(Color.FromRgb(139, 195, 74)),
        "Failed" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
        "Cancelled" => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
        _ => new SolidColorBrush(Colors.Gray)
    };

    public string ProgressText => $"{ProgressPercentage}%";

    public string DurationText => Duration.HasValue
        ? Duration.Value.TotalMinutes >= 1
            ? $"{Duration.Value.Minutes}m {Duration.Value.Seconds}s"
            : $"{Duration.Value.Seconds}s"
        : "-";

    public bool CanPauseResume => Status == "Running" || Status == "Paused";
    public bool CanCancel => Status == "Running" || Status == "Pending" || Status == "Paused";

    public string PauseResumeIcon => Status == "Paused" ? "Play" : "Pause";
    public string PauseResumeTooltip => Status == "Paused" ? "Resume Job" : "Pause Job";
}

#endregion
