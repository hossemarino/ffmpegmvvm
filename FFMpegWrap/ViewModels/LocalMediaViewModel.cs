using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using FFMpegWrap.Models;
using FFMpegWrap.Services;

namespace FFMpegWrap.ViewModels;

/// <summary>
/// Coordinates multi-file local media workflows, including preview, metadata, conversion settings, and batch execution.
/// </summary>
public sealed class LocalMediaViewModel : ObservableObject
{
    private const string SameAsSourceFormat = "Same as source";
    private readonly IFileDialogService _fileDialogService;
    private readonly IMediaToolService _mediaToolService;
    private readonly AsyncRelayCommand _refreshOptionsCommand;
    private readonly AsyncRelayCommand _executeToolCommand;
    private readonly RelayCommand _browseOutputFolderCommand;
    private CancellationTokenSource? _previewCancellationTokenSource;
    private string? _selectedFilePath;
    private string? _outputDirectory;
    private string _selectedOutputFormat = SameAsSourceFormat;
    private string _audioBitrate = string.Empty;
    private string _videoBitrate = string.Empty;
    private string _selectedTool = "ffmpeg";
    private Uri? _previewSource;
    private string _generatedCommandLine = "ffmpeg";
    private string _executionLog = string.Empty;
    private string _statusMessage = "Select one or more local files to inspect or convert.";

    public LocalMediaViewModel(IFileDialogService fileDialogService, IMediaToolService mediaToolService)
    {
        _fileDialogService = fileDialogService;
        _mediaToolService = mediaToolService;
        _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        foreach (var tool in _mediaToolService.GetSupportedTools())
        {
            Tools.Add(tool);
        }

        foreach (var format in new[] { SameAsSourceFormat, "mp4", "mkv", "mov", "mp3", "wav", "flac", "m4a", "aac", "ogg" })
        {
            OutputFormats.Add(format);
        }

        BrowseFilesCommand = new AsyncRelayCommand(BrowseFilesAsync);
        _refreshOptionsCommand = new AsyncRelayCommand(LoadOptionsAsync);
        _executeToolCommand = new AsyncRelayCommand(ExecuteToolAsync, CanExecuteTool);
        _browseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);

        _ = LoadOptionsAsync();
    }

    public ObservableCollection<string> Tools { get; } = [];

    public ObservableCollection<string> SelectedFiles { get; } = [];

    public ObservableCollection<string> OutputFormats { get; } = [];

    public ObservableCollection<MediaMetadataItem> Metadata { get; } = [];

    public ObservableCollection<ToolCommandOption> ToolOptions { get; } = [];

    public AsyncRelayCommand BrowseFilesCommand { get; }

    public AsyncRelayCommand RefreshOptionsCommand => _refreshOptionsCommand;

    public AsyncRelayCommand ExecuteToolCommand => _executeToolCommand;

    public RelayCommand BrowseOutputFolderCommand => _browseOutputFolderCommand;

    public string? SelectedFilePath
    {
        get => _selectedFilePath;
        set
        {
            if (SetProperty(ref _selectedFilePath, value))
            {
                _ = LoadPreviewAsync(value);
                _ = LoadMetadataAsync();
                UpdateGeneratedCommandLine();
                _executeToolCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            if (SetProperty(ref _outputDirectory, value))
            {
                UpdateGeneratedCommandLine();
            }
        }
    }

    public string SelectedOutputFormat
    {
        get => _selectedOutputFormat;
        set
        {
            if (SetProperty(ref _selectedOutputFormat, value))
            {
                UpdateGeneratedCommandLine();
            }
        }
    }

    public string AudioBitrate
    {
        get => _audioBitrate;
        set
        {
            if (SetProperty(ref _audioBitrate, value))
            {
                UpdateGeneratedCommandLine();
            }
        }
    }

    public string VideoBitrate
    {
        get => _videoBitrate;
        set
        {
            if (SetProperty(ref _videoBitrate, value))
            {
                UpdateGeneratedCommandLine();
            }
        }
    }

    public string SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (SetProperty(ref _selectedTool, value))
            {
                _ = LoadOptionsAsync();
                _executeToolCommand.RaiseCanExecuteChanged();
                UpdateGeneratedCommandLine();
            }
        }
    }

    public Uri? PreviewSource
    {
        get => _previewSource;
        private set => SetProperty(ref _previewSource, value);
    }

    public string GeneratedCommandLine
    {
        get => _generatedCommandLine;
        private set => SetProperty(ref _generatedCommandLine, value);
    }

    public string ExecutionLog
    {
        get => _executionLog;
        private set => SetProperty(ref _executionLog, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private async Task BrowseFilesAsync()
    {
        var filePaths = _fileDialogService.OpenMediaFiles();
        if (filePaths.Count == 0)
        {
            return;
        }

        SelectedFiles.Clear();
        foreach (var filePath in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SelectedFiles.Add(filePath);
        }

        SelectedFilePath = SelectedFiles.FirstOrDefault();
        StatusMessage = SelectedFiles.Count == 1
            ? "Loaded 1 file. Preview starts automatically."
            : $"Loaded {SelectedFiles.Count} files. Preview starts automatically for the selected file.";

        await LoadMetadataAsync();
        _executeToolCommand.RaiseCanExecuteChanged();
    }

    private void BrowseOutputFolder()
    {
        var folder = _fileDialogService.PickOutputFolder();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            OutputDirectory = folder;
        }
    }

    private async Task LoadMetadataAsync()
    {
        Metadata.Clear();

        var metadata = await _mediaToolService.GetMetadataAsync(SelectedFilePath);
        foreach (var item in metadata)
        {
            Metadata.Add(item);
        }
    }

    private async Task LoadPreviewAsync(string? filePath)
    {
        _previewCancellationTokenSource?.Cancel();
        _previewCancellationTokenSource?.Dispose();
        _previewCancellationTokenSource = null;

        PreviewSource = null;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        _previewCancellationTokenSource = cancellationTokenSource;

        try
        {
            var previewSource = await _mediaToolService.CreatePreviewSourceAsync(filePath, cancellationTokenSource.Token);
            if (!cancellationTokenSource.IsCancellationRequested
                && string.Equals(filePath, SelectedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                PreviewSource = previewSource;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (string.Equals(filePath, SelectedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                PreviewSource = null;
            }
        }
        finally
        {
            if (ReferenceEquals(_previewCancellationTokenSource, cancellationTokenSource))
            {
                _previewCancellationTokenSource = null;
            }

            cancellationTokenSource.Dispose();
        }
    }

    private async Task LoadOptionsAsync()
    {
        try
        {
            StatusMessage = $"Loading {SelectedTool} options...";

            foreach (var option in ToolOptions)
            {
                option.PropertyChanged -= ToolOption_PropertyChanged;
            }

            ToolOptions.Clear();

            var options = await _mediaToolService.GetOptionsAsync(SelectedTool);
            foreach (var option in options)
            {
                option.PropertyChanged += ToolOption_PropertyChanged;
                ToolOptions.Add(option);
            }

            StatusMessage = ToolOptions.Count == 0
                ? $"No options were discovered for {SelectedTool}."
                : $"Loaded {ToolOptions.Count} option(s) for {SelectedTool}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load {SelectedTool} options: {ex.Message}";
        }
        finally
        {
            UpdateGeneratedCommandLine();
        }
    }

    private async Task ExecuteToolAsync()
    {
        try
        {
            ExecutionLog = string.Empty;

            if (SelectedTool.Equals("ffplay", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = string.IsNullOrWhiteSpace(SelectedFilePath)
                    ? "Choose a file to preview."
                    : $"Previewing {Path.GetFileName(SelectedFilePath)} in the embedded player.";
                await LoadPreviewAsync(SelectedFilePath);
                return;
            }

            var log = new Progress<string>(line =>
            {
                ExecutionLog = string.IsNullOrWhiteSpace(ExecutionLog)
                    ? line
                    : $"{ExecutionLog}{Environment.NewLine}{line}";
            });

            if (SelectedTool.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = $"Converting {SelectedFiles.Count} file(s)...";

                foreach (var inputPath in SelectedFiles)
                {
                    var outputPath = BuildOutputPath(inputPath);
                    var result = await _mediaToolService.ExecuteAsync(
                        SelectedTool,
                        inputPath,
                        outputPath,
                        GetEffectiveOptions(),
                        log);

                    if (!result.Succeeded)
                    {
                        StatusMessage = $"Conversion failed for {Path.GetFileName(inputPath)} with exit code {result.ExitCode}.";
                        return;
                    }
                }

                StatusMessage = $"Converted {SelectedFiles.Count} file(s) successfully.";
                return;
            }

            StatusMessage = $"Running {SelectedTool}...";
            var singleResult = await _mediaToolService.ExecuteAsync(SelectedTool, SelectedFilePath, null, GetEffectiveOptions(), log);
            StatusMessage = singleResult.Succeeded
                ? $"{SelectedTool} finished successfully."
                : $"{SelectedTool} exited with code {singleResult.ExitCode}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to run {SelectedTool}: {ex.Message}";
        }
    }

    private bool CanExecuteTool()
    {
        return SelectedFiles.Count > 0;
    }

    private void ToolOption_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToolCommandOption.IsEnabled) or nameof(ToolCommandOption.Value))
        {
            UpdateGeneratedCommandLine();
        }
    }

    private void UpdateGeneratedCommandLine()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedFilePath))
            {
                GeneratedCommandLine = SelectedTool;
                return;
            }

            var outputPath = SelectedTool.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase)
                ? BuildOutputPath(SelectedFilePath)
                : null;

            GeneratedCommandLine = _mediaToolService.BuildCommandLine(SelectedTool, SelectedFilePath, outputPath, GetEffectiveOptions());
        }
        catch
        {
            GeneratedCommandLine = SelectedTool;
        }
    }

    private IReadOnlyList<ToolCommandOption> GetEffectiveOptions()
    {
        var options = ToolOptions
            .Select(option => new ToolCommandOption
            {
                ToolName = option.ToolName,
                Category = option.Category,
                Flag = option.Flag,
                Description = option.Description,
                ExampleUsage = option.ExampleUsage,
                RequiresValue = option.RequiresValue,
                IsEnabled = option.IsEnabled,
                Value = option.Value
            })
            .ToList();

        if (SelectedTool.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            AddOrUpdateOption(options, "-b:a", AudioBitrate);
            AddOrUpdateOption(options, "-b:v", VideoBitrate);
        }

        return options;
    }

    private string BuildOutputPath(string inputPath)
    {
        var directory = string.IsNullOrWhiteSpace(OutputDirectory)
            ? Path.GetDirectoryName(inputPath)
            : OutputDirectory;

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Unable to determine the output folder.");
        }

        var extension = SelectedOutputFormat.Equals(SameAsSourceFormat, StringComparison.OrdinalIgnoreCase)
            ? Path.GetExtension(inputPath)
            : $".{SelectedOutputFormat.Trim('.')}";


        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory, $"{fileNameWithoutExtension}.converted{extension}");
    }

    private static void AddOrUpdateOption(List<ToolCommandOption> options, string flag, string value)
    {
        var existing = options.FirstOrDefault(option => option.Flag.Equals(flag, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(value))
        {
            if (existing is not null)
            {
                existing.IsEnabled = false;
                existing.Value = value;
            }

            return;
        }

        if (existing is not null)
        {
            existing.IsEnabled = true;
            existing.Value = value;
            return;
        }

        options.Add(new ToolCommandOption
        {
            ToolName = "ffmpeg",
            Category = "Conversion",
            Flag = flag,
            Description = flag == "-b:a" ? "Audio bitrate" : "Video bitrate",
            ExampleUsage = flag == "-b:a" ? "ffmpeg -i input.wav -b:a 192k output.mp3" : "ffmpeg -i input.mp4 -b:v 2500k output.mp4",
            RequiresValue = true,
            IsEnabled = true,
            Value = value
        });
    }
}
