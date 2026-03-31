using FFMpegWrap.Services;

namespace FFMpegWrap.ViewModels;

public sealed class ToolPathsViewModel : ObservableObject
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IToolPathService _toolPathService;
    private string? _ffmpegPath;
    private string? _ffplayPath;
    private string? _ffprobePath;
    private string? _ytDlpPath;
    private string _statusMessage = "Configure tool paths only if auto-discovery does not find them.";

    public ToolPathsViewModel(IFileDialogService fileDialogService, IToolPathService toolPathService)
    {
        _fileDialogService = fileDialogService;
        _toolPathService = toolPathService;

        BrowseFfmpegCommand = new RelayCommand(() => FfmpegPath = BrowseForExecutable("ffmpeg", FfmpegPath));
        BrowseFfplayCommand = new RelayCommand(() => FfplayPath = BrowseForExecutable("ffplay", FfplayPath));
        BrowseFfprobeCommand = new RelayCommand(() => FfprobePath = BrowseForExecutable("ffprobe", FfprobePath));
        BrowseYtDlpCommand = new RelayCommand(() => YtDlpPath = BrowseForExecutable("yt-dlp", YtDlpPath));
        SaveCommand = new RelayCommand(Save);
        ReloadCommand = new RelayCommand(Load);

        Load();
    }

    public RelayCommand BrowseFfmpegCommand { get; }

    public RelayCommand BrowseFfplayCommand { get; }

    public RelayCommand BrowseFfprobeCommand { get; }

    public RelayCommand BrowseYtDlpCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand ReloadCommand { get; }

    public string? FfmpegPath
    {
        get => _ffmpegPath;
        set => SetProperty(ref _ffmpegPath, value);
    }

    public string? FfplayPath
    {
        get => _ffplayPath;
        set => SetProperty(ref _ffplayPath, value);
    }

    public string? FfprobePath
    {
        get => _ffprobePath;
        set => SetProperty(ref _ffprobePath, value);
    }

    public string? YtDlpPath
    {
        get => _ytDlpPath;
        set => SetProperty(ref _ytDlpPath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private string? BrowseForExecutable(string toolName, string? currentPath)
    {
        return _fileDialogService.PickExecutableFile(toolName, currentPath) ?? currentPath;
    }

    private void Load()
    {
        var settings = _toolPathService.GetSettings();
        FfmpegPath = settings.FfmpegPath;
        FfplayPath = settings.FfplayPath;
        FfprobePath = settings.FfprobePath;
        YtDlpPath = settings.YtDlpPath;
        StatusMessage = "Loaded saved tool paths.";
    }

    private void Save()
    {
        _toolPathService.SaveSettings(new ToolPathSettings
        {
            FfmpegPath = Normalize(FfmpegPath),
            FfplayPath = Normalize(FfplayPath),
            FfprobePath = Normalize(FfprobePath),
            YtDlpPath = Normalize(YtDlpPath)
        });

        StatusMessage = "Tool paths saved.";
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
