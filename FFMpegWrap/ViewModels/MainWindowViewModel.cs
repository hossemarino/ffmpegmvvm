using FFMpegWrap.Services;

namespace FFMpegWrap.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private AppTheme _selectedTheme;

    public MainWindowViewModel(
        IThemeService themeService,
        LocalMediaViewModel localMedia,
        OnlineMediaViewModel onlineMedia,
        CommandExplorerViewModel commandExplorer,
        ToolPathsViewModel toolPaths)
    {
        _themeService = themeService;
        LocalMedia = localMedia;
        OnlineMedia = onlineMedia;
        CommandExplorer = commandExplorer;
        ToolPaths = toolPaths;
        _selectedTheme = _themeService.SelectedTheme;
    }

    public string Title => "FFMpegWrap";

    public IReadOnlyList<AppTheme> Themes => _themeService.AvailableThemes;

    public AppTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                _themeService.SelectedTheme = value;
            }
        }
    }

    public LocalMediaViewModel LocalMedia { get; }

    public OnlineMediaViewModel OnlineMedia { get; }

    public CommandExplorerViewModel CommandExplorer { get; }

    public ToolPathsViewModel ToolPaths { get; }
}
