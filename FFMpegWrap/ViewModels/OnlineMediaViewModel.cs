using System.Collections.ObjectModel;
using FFMpegWrap.Models;
using FFMpegWrap.Services;

namespace FFMpegWrap.ViewModels;

public sealed class OnlineMediaViewModel : ObservableObject
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IYtDlpService _ytDlpService;
    private readonly AsyncRelayCommand _loadFormatsCommand;
    private readonly AsyncRelayCommand _downloadCommand;
    private readonly RelayCommand _browseOutputFolderCommand;
    private string _url = string.Empty;
    private string _outputName = string.Empty;
    private string _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    private OnlineFormatOption? _selectedFormat;
    private double _downloadProgress;
    private string _statusMessage = "Enter a URL to inspect available formats.";
    private string _downloadLog = string.Empty;

    public OnlineMediaViewModel(IFileDialogService fileDialogService, IYtDlpService ytDlpService)
    {
        _fileDialogService = fileDialogService;
        _ytDlpService = ytDlpService;
        _loadFormatsCommand = new AsyncRelayCommand(LoadFormatsAsync, () => !string.IsNullOrWhiteSpace(Url));
        _downloadCommand = new AsyncRelayCommand(DownloadAsync, () => !string.IsNullOrWhiteSpace(Url));
        _browseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
    }

    public ObservableCollection<OnlineFormatOption> Formats { get; } = [];

    public AsyncRelayCommand LoadFormatsCommand => _loadFormatsCommand;

    public AsyncRelayCommand DownloadCommand => _downloadCommand;

    public RelayCommand BrowseOutputFolderCommand => _browseOutputFolderCommand;

    public string Url
    {
        get => _url;
        set
        {
            if (SetProperty(ref _url, value))
            {
                _loadFormatsCommand.RaiseCanExecuteChanged();
                _downloadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string OutputName
    {
        get => _outputName;
        set => SetProperty(ref _outputName, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetProperty(ref _outputDirectory, value);
    }

    public OnlineFormatOption? SelectedFormat
    {
        get => _selectedFormat;
        set => SetProperty(ref _selectedFormat, value);
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string DownloadLog
    {
        get => _downloadLog;
        set => SetProperty(ref _downloadLog, value);
    }

    private async Task LoadFormatsAsync()
    {
        try
        {
            Formats.Clear();
            StatusMessage = "Loading formats...";

            var formats = await _ytDlpService.GetAvailableFormatsAsync(Url);
            foreach (var format in formats)
            {
                Formats.Add(format);
            }

            SelectedFormat = Formats.FirstOrDefault();
            StatusMessage = Formats.Count == 0
                ? "No formats found."
                : $"Loaded {Formats.Count} format option(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load formats: {ex.Message}";
        }
    }

    private async Task DownloadAsync()
    {
        try
        {
            DownloadProgress = 0;
            DownloadLog = string.Empty;
            StatusMessage = "Preparing download...";

            var progress = new Progress<double>(value => DownloadProgress = value);
            var log = new Progress<string>(message =>
            {
                DownloadLog = string.IsNullOrWhiteSpace(DownloadLog)
                    ? message
                    : $"{DownloadLog}{Environment.NewLine}{message}";
            });

            await _ytDlpService.DownloadAsync(Url, SelectedFormat?.FormatId, OutputName, OutputDirectory, progress, log);
            StatusMessage = "Download completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download failed: {ex.Message}";
        }
    }

    private void BrowseOutputFolder()
    {
        var folder = _fileDialogService.PickOutputFolder();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            OutputDirectory = folder;
        }
    }
}
