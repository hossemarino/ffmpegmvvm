using System.Windows;

namespace FFMpegWrap.Services;

public interface IThemeService
{
    IReadOnlyList<AppTheme> AvailableThemes { get; }

    AppTheme SelectedTheme { get; set; }

    void Initialize(Application application);
}
