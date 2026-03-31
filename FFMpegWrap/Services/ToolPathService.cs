using System.IO;
using System.Text.Json;

namespace FFMpegWrap.Services;

/// <summary>
/// Resolves external tool executable paths from persisted settings, application-relative folders, and PATH.
/// </summary>
public sealed class ToolPathService : IToolPathService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private ToolPathSettings _settings;

    public ToolPathService()
    {
        _settings = LoadSettings();
    }

    public string ResolveToolPath(string toolName)
    {
        var executableName = toolName.Equals("yt-dlp", StringComparison.OrdinalIgnoreCase)
            ? "yt-dlp.exe"
            : $"{toolName}.exe";

        var configuredPath = toolName.ToLowerInvariant() switch
        {
            "ffmpeg" => _settings.FfmpegPath,
            "ffplay" => _settings.FfplayPath,
            "ffprobe" => _settings.FfprobePath,
            "yt-dlp" => _settings.YtDlpPath,
            _ => null
        };

        if (FileExists(configuredPath))
        {
            return configuredPath!;
        }

        foreach (var candidate in GetCandidatePaths(executableName))
        {
            if (FileExists(candidate))
            {
                return candidate;
            }
        }

        var pathMatch = TryResolveFromEnvironmentPath(executableName);
        return !string.IsNullOrWhiteSpace(pathMatch)
            ? pathMatch
            : executableName;
    }

    public ToolPathSettings GetSettings()
    {
        return new ToolPathSettings
        {
            FfmpegPath = _settings.FfmpegPath,
            FfplayPath = _settings.FfplayPath,
            FfprobePath = _settings.FfprobePath,
            YtDlpPath = _settings.YtDlpPath
        };
    }

    public void SaveSettings(ToolPathSettings settings)
    {
        _settings = new ToolPathSettings
        {
            FfmpegPath = Normalize(settings.FfmpegPath),
            FfplayPath = Normalize(settings.FfplayPath),
            FfprobePath = Normalize(settings.FfprobePath),
            YtDlpPath = Normalize(settings.YtDlpPath)
        };

        var settingsDirectory = Path.GetDirectoryName(GetSettingsFilePath());
        if (!string.IsNullOrWhiteSpace(settingsDirectory))
        {
            Directory.CreateDirectory(settingsDirectory);
        }

        var json = JsonSerializer.Serialize(_settings, SerializerOptions);
        File.WriteAllText(GetSettingsFilePath(), json);
    }

    private static IEnumerable<string> GetCandidatePaths(string executableName)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var currentDirectory = Environment.CurrentDirectory;
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in GetSearchDirectories(baseDirectory, currentDirectory))
        {
            foreach (var candidate in new[]
            {
                Path.Combine(directory, executableName),
                Path.Combine(directory, "tools", executableName),
                Path.Combine(directory, "bin", executableName),
                Path.Combine(directory, "ffmpeg", executableName),
                Path.Combine(directory, "yt-dlp", executableName)
            })
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<string> GetSearchDirectories(params string[] seedDirectories)
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in seedDirectories.Where(directory => !string.IsNullOrWhiteSpace(directory)))
        {
            var current = new DirectoryInfo(seed);
            while (current is not null)
            {
                if (yielded.Add(current.FullName))
                {
                    yield return current.FullName;
                }

                current = current.Parent;
            }
        }
    }

    private static string? TryResolveFromEnvironmentPath(string executableName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(directory, executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static bool FileExists(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ToolPathSettings LoadSettings()
    {
        try
        {
            var settingsPath = GetSettingsFilePath();
            if (!File.Exists(settingsPath))
            {
                return new ToolPathSettings();
            }

            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<ToolPathSettings>(json, SerializerOptions) ?? new ToolPathSettings();
        }
        catch
        {
            return new ToolPathSettings();
        }
    }

    private static string GetSettingsFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FFMpegWrap",
            "tool-paths.json");
    }
}
