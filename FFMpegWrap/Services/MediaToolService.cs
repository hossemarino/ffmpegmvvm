using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FFMpegWrap.Models;

namespace FFMpegWrap.Services;

/// <summary>
/// Bridges the local media workspace to ffmpeg-family executables.
/// It derives starter options from live help output when available and falls back gracefully when tools are unavailable.
/// </summary>
public sealed class MediaToolService : IMediaToolService
{
    private readonly IHelpPageService _helpPageService;
    private readonly IProcessRunner _processRunner;
    private readonly IToolPathService _toolPathService;

    public MediaToolService(IHelpPageService helpPageService, IProcessRunner processRunner, IToolPathService toolPathService)
    {
        _helpPageService = helpPageService;
        _processRunner = processRunner;
        _toolPathService = toolPathService;
    }

    public async Task<Uri?> CreatePreviewSourceAsync(string? filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var previewPath = await BuildPreviewPathAsync(filePath, cancellationToken);
            return new Uri(previewPath, UriKind.Absolute);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new Uri(filePath, UriKind.Absolute);
        }
    }

    public async Task<IReadOnlyList<ToolCommandOption>> GetOptionsAsync(string toolName, CancellationToken cancellationToken = default)
    {
        try
        {
            var categories = await _helpPageService.GetHelpCategoriesAsync(toolName, cancellationToken);
            var options = categories
                .SelectMany(category => category.Entries.Select(entry => CreateCommandOption(toolName, category.Name, entry)))
                .Where(option => !string.IsNullOrWhiteSpace(option.Flag))
                .DistinctBy(option => option.Flag)
                .Take(120)
                .ToList();

            return options.Count > 0
                ? options
                : GetFallbackOptions(toolName);
        }
        catch
        {
            return GetFallbackOptions(toolName);
        }
    }

    public string BuildCommandLine(string toolName, string? inputPath, string? outputPath, IEnumerable<ToolCommandOption> options)
    {
        var executablePath = _toolPathService.ResolveToolPath(toolName);
        var arguments = BuildArguments(toolName, inputPath, outputPath, options);
        return string.IsNullOrWhiteSpace(arguments)
            ? executablePath
            : $"{executablePath} {arguments}";
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        string? inputPath,
        string? outputPath,
        IEnumerable<ToolCommandOption> options,
        IProgress<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var executablePath = _toolPathService.ResolveToolPath(toolName);
        var arguments = BuildArguments(toolName, inputPath, outputPath, options);
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        void AppendOutput(StringBuilder target, string line)
        {
            target.AppendLine(line);
            log?.Report(line);
        }

        var exitCode = await _processRunner.RunAsync(
            executablePath,
            arguments,
            line => AppendOutput(standardOutput, line),
            line => AppendOutput(standardError, line),
            cancellationToken);

        return new ToolExecutionResult
        {
            ExitCode = exitCode,
            StandardOutput = standardOutput.ToString(),
            StandardError = standardError.ToString()
        };
    }

    public async Task<IReadOnlyList<MediaMetadataItem>> GetMetadataAsync(string? filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return [];
        }

        var fileInfoMetadata = GetFileInfoMetadata(filePath);

        try
        {
            var toolPath = _toolPathService.ResolveToolPath("ffprobe");
            var output = new StringBuilder();
            var errors = new StringBuilder();
            var arguments = $"-v error -print_format json -show_format -show_streams {Quote(filePath)}";

            var exitCode = await _processRunner.RunAsync(
                toolPath,
                arguments,
                line => output.AppendLine(line),
                line => errors.AppendLine(line),
                cancellationToken);

            if (exitCode != 0 || output.Length == 0)
            {
                return fileInfoMetadata;
            }

            return ParseFfprobeMetadata(output.ToString(), fileInfoMetadata);
        }
        catch
        {
            return fileInfoMetadata;
        }
    }

    public IReadOnlyList<string> GetSupportedTools()
    {
        return ["ffmpeg", "ffplay", "ffprobe"];
    }

    private async Task<string> BuildPreviewPathAsync(string filePath, CancellationToken cancellationToken)
    {
        var previewKind = await GetPreviewKindAsync(filePath, cancellationToken);
        if (previewKind == PreviewKind.None)
        {
            return filePath;
        }

        var previewPath = GetPreviewCachePath(filePath, previewKind);
        if (File.Exists(previewPath))
        {
            return previewPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);

        var executablePath = _toolPathService.ResolveToolPath("ffmpeg");
        var arguments = previewKind == PreviewKind.Video
            ? BuildVideoPreviewArguments(filePath, previewPath)
            : BuildAudioPreviewArguments(filePath, previewPath);

        var errors = new StringBuilder();
        var exitCode = await _processRunner.RunAsync(
            executablePath,
            arguments,
            onStandardError: line => errors.AppendLine(line),
            cancellationToken: cancellationToken);

        if (exitCode != 0 || !File.Exists(previewPath))
        {
            throw new InvalidOperationException($"Unable to build preview media for '{filePath}'.{Environment.NewLine}{errors}");
        }

        return previewPath;
    }

    private async Task<PreviewKind> GetPreviewKindAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var toolPath = _toolPathService.ResolveToolPath("ffprobe");
            var output = new StringBuilder();
            var arguments = $"-v error -print_format json -show_streams {Quote(filePath)}";

            var exitCode = await _processRunner.RunAsync(
                toolPath,
                arguments,
                line => output.AppendLine(line),
                cancellationToken: cancellationToken);

            if (exitCode != 0 || output.Length == 0)
            {
                return PreviewKind.None;
            }

            using var document = JsonDocument.Parse(output.ToString());
            if (!document.RootElement.TryGetProperty("streams", out var streams)
                || streams.ValueKind != JsonValueKind.Array)
            {
                return PreviewKind.None;
            }

            var hasVideo = false;
            var hasAudio = false;

            foreach (var stream in streams.EnumerateArray())
            {
                var streamType = GetString(stream, "codec_type");
                hasVideo |= string.Equals(streamType, "video", StringComparison.OrdinalIgnoreCase);
                hasAudio |= string.Equals(streamType, "audio", StringComparison.OrdinalIgnoreCase);
            }

            return hasVideo
                ? PreviewKind.Video
                : hasAudio
                    ? PreviewKind.Audio
                    : PreviewKind.None;
        }
        catch
        {
            return PreviewKind.None;
        }
    }

    private static string GetPreviewCachePath(string filePath, PreviewKind previewKind)
    {
        var fileInfo = new FileInfo(filePath);
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "FFMpegWrap", "PreviewCache");
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{fileInfo.FullName}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}|{previewKind}")));
        return Path.Combine(cacheDirectory, $"{cacheKey}.mp4");
    }

    private static string BuildVideoPreviewArguments(string inputPath, string previewPath)
    {
        return string.Join(' ',
        [
            "-y",
            "-v", "error",
            "-ss", "00:00:00",
            "-t", "00:00:20",
            "-i", Quote(inputPath),
            "-vf", Quote("scale=960:-2:force_original_aspect_ratio=decrease"),
            "-c:v", "libx264",
            "-preset", "ultrafast",
            "-pix_fmt", "yuv420p",
            "-c:a", "aac",
            "-ac", "2",
            "-movflags", "+faststart",
            Quote(previewPath)
        ]);
    }

    private static string BuildAudioPreviewArguments(string inputPath, string previewPath)
    {
        return string.Join(' ',
        [
            "-y",
            "-v", "error",
            "-t", "00:00:20",
            "-i", Quote(inputPath),
            "-filter_complex", Quote("[0:a]showspectrum=s=960x540:mode=combined:slide=scroll:color=intensity:legend=0,format=yuv420p[v]"),
            "-map", Quote("[v]"),
            "-map", "0:a:0",
            "-c:v", "libx264",
            "-preset", "ultrafast",
            "-pix_fmt", "yuv420p",
            "-c:a", "aac",
            "-ac", "2",
            "-shortest",
            "-movflags", "+faststart",
            Quote(previewPath)
        ]);
    }

    private static ToolCommandOption CreateCommandOption(string toolName, string category, HelpEntry entry)
    {
        var primaryFlag = ExtractPrimaryFlag(entry.Flags);
        var requiresValue = RequiresValue(entry.Flags);

        return new ToolCommandOption
        {
            ToolName = toolName,
            Category = category,
            Flag = primaryFlag,
            Description = entry.Description,
            ExampleUsage = entry.ExampleUsage,
            RequiresValue = requiresValue,
            Value = requiresValue ? GetSuggestedValue(entry.Flags) : null
        };
    }

    private static IReadOnlyList<MediaMetadataItem> GetFileInfoMetadata(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        return
        [
            new MediaMetadataItem { Name = "File Name", Value = fileInfo.Name },
            new MediaMetadataItem { Name = "Directory", Value = fileInfo.DirectoryName ?? string.Empty },
            new MediaMetadataItem { Name = "Extension", Value = fileInfo.Extension },
            new MediaMetadataItem { Name = "Size", Value = $"{fileInfo.Length / 1024d / 1024d:F2} MB" },
            new MediaMetadataItem { Name = "Created", Value = fileInfo.CreationTime.ToString("g") },
            new MediaMetadataItem { Name = "Modified", Value = fileInfo.LastWriteTime.ToString("g") }
        ];
    }

    private static IReadOnlyList<MediaMetadataItem> ParseFfprobeMetadata(string json, IReadOnlyList<MediaMetadataItem> fallbackMetadata)
    {
        var metadata = fallbackMetadata.ToList();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("format", out var formatElement))
        {
            AddMetadataIfPresent(metadata, "Container", formatElement, "format_long_name");
            AddMetadataIfPresent(metadata, "Format", formatElement, "format_name");
            AddMetadataIfPresent(metadata, "Duration", formatElement, "duration");
            AddMetadataIfPresent(metadata, "Bit Rate", formatElement, "bit_rate");
            AddMetadataIfPresent(metadata, "Probe Score", formatElement, "probe_score");
        }

        if (root.TryGetProperty("streams", out var streamsElement) && streamsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streamsElement.EnumerateArray())
            {
                var streamType = GetString(stream, "codec_type");
                var suffix = string.IsNullOrWhiteSpace(streamType)
                    ? "Stream"
                    : char.ToUpperInvariant(streamType[0]) + streamType[1..];

                AddMetadataIfPresent(metadata, $"{suffix} Codec", stream, "codec_name");
                AddMetadataIfPresent(metadata, $"{suffix} Codec Long", stream, "codec_long_name");
                AddMetadataIfPresent(metadata, $"{suffix} Bit Rate", stream, "bit_rate");

                if (streamType == "video")
                {
                    var width = GetString(stream, "width");
                    var height = GetString(stream, "height");
                    if (!string.IsNullOrWhiteSpace(width) && !string.IsNullOrWhiteSpace(height))
                    {
                        metadata.Add(new MediaMetadataItem { Name = "Resolution", Value = $"{width}x{height}" });
                    }
                }

                if (streamType == "audio")
                {
                    AddMetadataIfPresent(metadata, "Sample Rate", stream, "sample_rate");
                    AddMetadataIfPresent(metadata, "Channels", stream, "channels");
                }
            }
        }

        return metadata
            .GroupBy(item => item.Name)
            .Select(group => group.First())
            .ToList();
    }

    private static void AddMetadataIfPresent(List<MediaMetadataItem> metadata, string name, JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata.Add(new MediaMetadataItem { Name = name, Value = value });
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.ToString()
            : null;
    }

    private static IReadOnlyList<ToolCommandOption> GetFallbackOptions(string toolName)
    {
        return toolName.ToLowerInvariant() switch
        {
            "ffplay" =>
            [
                new ToolCommandOption { ToolName = "ffplay", Category = "Playback", Flag = "-autoexit", Description = "Exit when playback finishes.", ExampleUsage = "ffplay -autoexit input.mp4" },
                new ToolCommandOption { ToolName = "ffplay", Category = "Playback", Flag = "-ss", Description = "Seek to a start position.", ExampleUsage = "ffplay -ss 00:00:30 input.mp4", RequiresValue = true, Value = "00:00:00" },
                new ToolCommandOption { ToolName = "ffplay", Category = "Playback", Flag = "-volume", Description = "Set playback volume.", ExampleUsage = "ffplay -volume 80 input.mp4", RequiresValue = true, Value = "100" }
            ],
            "ffprobe" =>
            [
                new ToolCommandOption { ToolName = "ffprobe", Category = "Inspection", Flag = "-show_streams", Description = "Print stream information.", ExampleUsage = "ffprobe -show_streams input.mp4" },
                new ToolCommandOption { ToolName = "ffprobe", Category = "Inspection", Flag = "-show_format", Description = "Print container format information.", ExampleUsage = "ffprobe -show_format input.mp4" },
                new ToolCommandOption { ToolName = "ffprobe", Category = "Output", Flag = "-of", Description = "Set output format.", ExampleUsage = "ffprobe -of json input.mp4", RequiresValue = true, Value = "json" }
            ],
            _ =>
            [
                new ToolCommandOption { ToolName = "ffmpeg", Category = "Input", Flag = "-ss", Description = "Seek input before processing.", ExampleUsage = "ffmpeg -ss 00:00:10 -i input.mp4 output.mp4", RequiresValue = true, Value = "00:00:00" },
                new ToolCommandOption { ToolName = "ffmpeg", Category = "Video", Flag = "-c:v", Description = "Select the video codec.", ExampleUsage = "ffmpeg -i input.mp4 -c:v libx264 output.mp4", RequiresValue = true, Value = "libx264" },
                new ToolCommandOption { ToolName = "ffmpeg", Category = "Audio", Flag = "-c:a", Description = "Select the audio codec.", ExampleUsage = "ffmpeg -i input.mp4 -c:a aac output.mp4", RequiresValue = true, Value = "aac" },
                new ToolCommandOption { ToolName = "ffmpeg", Category = "Output", Flag = "-y", Description = "Overwrite output files without asking.", ExampleUsage = "ffmpeg -y -i input.mp4 output.mp4" }
            ]
        };
    }

    private static string BuildArguments(string toolName, string? inputPath, string? outputPath, IEnumerable<ToolCommandOption> options)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            throw new InvalidOperationException("Select an existing input file first.");
        }

        var builder = new StringBuilder();

        foreach (var option in options.Where(option => option.IsEnabled))
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(option.Flag);

            if (option.RequiresValue && !string.IsNullOrWhiteSpace(option.Value))
            {
                builder.Append(' ');
                builder.Append(Quote(option.Value));
            }
        }

        if (toolName.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append("-i ");
            builder.Append(Quote(inputPath));

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new InvalidOperationException("Provide an output file path when running ffmpeg.");
            }

            builder.Append(' ');
            builder.Append(Quote(outputPath));
            return builder.ToString();
        }

        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(Quote(inputPath));
        return builder.ToString();
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "\"\"";
        }

        return value.Contains(' ') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
    }

    private static string ExtractPrimaryFlag(string flags)
    {
        if (string.IsNullOrWhiteSpace(flags))
        {
            return string.Empty;
        }

        return flags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(flag => flag.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .FirstOrDefault(flag => !string.IsNullOrWhiteSpace(flag))
            ?? string.Empty;
    }

    private static bool RequiresValue(string flags)
    {
        return flags.Contains('<')
            || flags.Contains('=')
            || flags.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Skip(1).Any();
    }

    private static string? GetSuggestedValue(string flags)
    {
        if (flags.Contains("<time>", StringComparison.OrdinalIgnoreCase))
        {
            return "00:00:00";
        }

        if (flags.Contains("<file>", StringComparison.OrdinalIgnoreCase) || flags.Contains("PATH", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (flags.Contains("<codec>", StringComparison.OrdinalIgnoreCase))
        {
            return "copy";
        }

        if (flags.Contains("<format>", StringComparison.OrdinalIgnoreCase) || flags.Contains("FORMAT", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }

        return string.Empty;
    }

    private enum PreviewKind
    {
        None,
        Audio,
        Video
    }
}
