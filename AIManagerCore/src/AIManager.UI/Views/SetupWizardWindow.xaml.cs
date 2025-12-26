using System.Windows;
using System.Windows.Controls;
using AIManager.Core.Services;
using AIManager.UI.Views.SetupPages;

namespace AIManager.UI.Views;

public partial class SetupWizardWindow : Window
{
    private readonly FirstRunDetectionService _firstRunService;
    private int _currentStep = 1;
    private readonly Page[] _pages;

    public SetupWizardWindow(FirstRunDetectionService firstRunService)
    {
        InitializeComponent();
        _firstRunService = firstRunService;

        // Initialize setup pages
        _pages = new Page[]
        {
            new WelcomePage(),
            new DatabaseSetupPage(),
            new AIProvidersSetupPage(),
            new CompletionPage()
        };

        // Show first page
        NavigateToStep(1);
    }

    private void NavigateToStep(int step)
    {
        _currentStep = step;

        // Update step indicators
        UpdateStepIndicators();

        // Navigate to page
        if (step >= 1 && step <= _pages.Length)
        {
            ContentFrame.Navigate(_pages[step - 1]);
        }

        // Update button visibility
        BtnBack.Visibility = step > 1 ? Visibility.Visible : Visibility.Collapsed;
        BtnNext.Visibility = step < _pages.Length ? Visibility.Visible : Visibility.Collapsed;
        BtnFinish.Visibility = step == _pages.Length ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStepIndicators()
    {
        // Reset all indicators
        Step1Indicator.Style = Resources["StepIndicator"] as Style;
        Step2Indicator.Style = Resources["StepIndicator"] as Style;
        Step3Indicator.Style = Resources["StepIndicator"] as Style;
        Step4Indicator.Style = Resources["StepIndicator"] as Style;

        // Set active indicator
        switch (_currentStep)
        {
            case 1:
                Step1Indicator.Style = Resources["StepIndicatorActive"] as Style;
                break;
            case 2:
                Step2Indicator.Style = Resources["StepIndicatorActive"] as Style;
                break;
            case 3:
                Step3Indicator.Style = Resources["StepIndicatorActive"] as Style;
                break;
            case 4:
                Step4Indicator.Style = Resources["StepIndicatorActive"] as Style;
                break;
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            NavigateToStep(_currentStep - 1);
        }
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep < _pages.Length)
        {
            NavigateToStep(_currentStep + 1);
        }
    }

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        // Mark setup as completed
        _firstRunService.MarkSetupCompleted();

        // Close wizard and show success message
        DialogResult = true;
        Close();
    }
}
