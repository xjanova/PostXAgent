using System.Globalization;

namespace MyPostXAgent.Core.Services;

/// <summary>
/// Service for managing application localization (Thai/English)
/// </summary>
public class LocalizationService
{
    public event EventHandler? LanguageChanged;

    private string _currentLanguage = "th"; // Default: Thai

    public string CurrentLanguage
    {
        get => _currentLanguage;
        private set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                CultureInfo.CurrentUICulture = new CultureInfo(value);
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsThaiLanguage => CurrentLanguage == "th";
    public bool IsEnglishLanguage => CurrentLanguage == "en";

    public void SetLanguage(string languageCode)
    {
        if (languageCode == "th" || languageCode == "en")
        {
            CurrentLanguage = languageCode;
        }
    }

    public void ToggleLanguage()
    {
        CurrentLanguage = IsThaiLanguage ? "en" : "th";
    }

    public string GetText(string thaiText, string englishText)
    {
        return IsThaiLanguage ? thaiText : englishText;
    }
}
