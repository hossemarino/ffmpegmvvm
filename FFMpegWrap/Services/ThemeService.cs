using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace FFMpegWrap.Services;

/// <summary>
/// Centralizes theme selection so the application uses one consistent set of brushes.
/// When <see cref="AppTheme.System"/> is selected, the active theme follows the Windows app theme.
/// </summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private Application? _application;
    private ResourceDictionary? _activeThemeDictionary;
    private AppTheme _selectedTheme = AppTheme.System;

    public IReadOnlyList<AppTheme> AvailableThemes { get; } =
    [
        AppTheme.System,
        AppTheme.Light,
        AppTheme.Dark
    ];

    public AppTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value)
            {
                return;
            }

            _selectedTheme = value;
            SaveThemePreference();
            ApplyTheme();
        }
    }

    public void Initialize(Application application)
    {
        _application = application;
        _selectedTheme = LoadThemePreference();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        ApplyTheme();
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (_selectedTheme != AppTheme.System)
        {
            return;
        }

        if (e.Category is not UserPreferenceCategory.Color and not UserPreferenceCategory.General and not UserPreferenceCategory.VisualStyle)
        {
            return;
        }

        _ = _application?.Dispatcher.InvokeAsync(ApplyTheme);
    }

    private void ApplyTheme()
    {
        if (_application is null)
        {
            return;
        }

        var resolvedTheme = ResolveTheme();
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"Themes/{resolvedTheme}.xaml", UriKind.Relative)
        };

        if (_activeThemeDictionary is not null)
        {
            _application.Resources.MergedDictionaries.Remove(_activeThemeDictionary);
        }

        _application.Resources.MergedDictionaries.Add(dictionary);
        _activeThemeDictionary = dictionary;
    }

    private string ResolveTheme()
    {
        var theme = _selectedTheme == AppTheme.System
            ? GetSystemTheme()
            : _selectedTheme;

        return theme == AppTheme.Light ? nameof(AppTheme.Light) : nameof(AppTheme.Dark);
    }

    private static AppTheme GetSystemTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        var value = key?.GetValue("AppsUseLightTheme");

        return value is int intValue && intValue == 0
            ? AppTheme.Dark
            : AppTheme.Light;
    }

    private static string GetSettingsDirectoryPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FFMpegWrap");
    }

    private static string GetSettingsFilePath()
    {
        return Path.Combine(GetSettingsDirectoryPath(), "theme.settings.json");
    }

    private AppTheme LoadThemePreference()
    {
        try
        {
            var settingsFilePath = GetSettingsFilePath();
            if (!File.Exists(settingsFilePath))
            {
                return AppTheme.System;
            }

            var json = File.ReadAllText(settingsFilePath);
            var settings = JsonSerializer.Deserialize<ThemeSettings>(json, SerializerOptions);

            return settings?.SelectedTheme ?? AppTheme.System;
        }
        catch
        {
            return AppTheme.System;
        }
    }

    private void SaveThemePreference()
    {
        try
        {
            var settingsDirectoryPath = GetSettingsDirectoryPath();
            Directory.CreateDirectory(settingsDirectoryPath);

            var settings = new ThemeSettings
            {
                SelectedTheme = _selectedTheme
            };

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(GetSettingsFilePath(), json);
        }
        catch
        {
        }
    }
}
