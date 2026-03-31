using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FFMpegWrap.Models;

namespace FFMpegWrap.Services;

/// <summary>
/// Runs yt-dlp as an external process and parses common machine-readable and console-friendly outputs.
/// </summary>
public sealed partial class YtDlpService : IYtDlpService
{
    private static readonly Regex DownloadProgressRegex = new(@"\[download\]\s+(\d{1,3}(?:\.\d+)?)%", RegexOptions.Compiled);
    private readonly IProcessRunner _processRunner;
    private readonly IToolPathService _toolPathService;

    public YtDlpService(IProcessRunner processRunner, IToolPathService toolPathService)
    {
        _processRunner = processRunner;
        _toolPathService = toolPathService;
    }

    public async Task<IReadOnlyList<OnlineFormatOption>> GetAvailableFormatsAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return [];
        }

        var output = new StringBuilder();
        var errors = new StringBuilder();
        var executablePath = _toolPathService.ResolveToolPath("yt-dlp");
        var arguments = $"--list-formats --no-warnings --skip-download {Quote(url)}";

        var exitCode = await _processRunner.RunAsync(
            executablePath,
            arguments,
            line => output.AppendLine(line),
            line => errors.AppendLine(line),
            cancellationToken);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"yt-dlp format listing failed.{Environment.NewLine}{errors}");
        }

        return ParseFormats(output.ToString());
    }

    public async Task DownloadAsync(
        string url,
        string? formatId,
        string? outputName,
        string? outputDirectory,
        IProgress<double>? progress = null,
        IProgress<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Provide a URL before starting a download.");
        }

        var executablePath = _toolPathService.ResolveToolPath("yt-dlp");
        var arguments = BuildDownloadArguments(url, formatId, outputName, outputDirectory);
        var output = new StringBuilder();
        var errors = new StringBuilder();

        void HandleLine(StringBuilder target, string line)
        {
            target.AppendLine(line);
            log?.Report(line);

            var match = DownloadProgressRegex.Match(line);
            if (match.Success && double.TryParse(match.Groups[1].Value, out var percentage))
            {
                progress?.Report(percentage);
            }
        }

        var exitCode = await _processRunner.RunAsync(
            executablePath,
            arguments,
            line => HandleLine(output, line),
            line => HandleLine(errors, line),
            cancellationToken);

        progress?.Report(100);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"yt-dlp download failed.{Environment.NewLine}{errors}{Environment.NewLine}{output}");
        }
    }

    private static IReadOnlyList<OnlineFormatOption> ParseFormats(string output)
    {
        var formats = new List<OnlineFormatOption>();
        var lines = output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        var formatSectionStarted = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.Contains("ID", StringComparison.OrdinalIgnoreCase)
                && line.Contains("EXT", StringComparison.OrdinalIgnoreCase))
            {
                formatSectionStarted = true;
                continue;
            }

            if (!formatSectionStarted)
            {
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                continue;
            }

            var columns = trimmed.Split('|', 2, StringSplitOptions.TrimEntries);
            var leftTokens = columns[0].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (leftTokens.Length < 2)
            {
                continue;
            }

            formats.Add(new OnlineFormatOption
            {
                FormatId = leftTokens[0],
                Extension = leftTokens[1],
                Resolution = leftTokens.Length > 2 ? string.Join(' ', leftTokens.Skip(2)) : string.Empty,
                Notes = columns.Length > 1 ? columns[1].Trim() : string.Empty
            });
        }

        return formats;
    }

    private static string BuildDownloadArguments(string url, string? formatId, string? outputName, string? outputDirectory)
    {
        var builder = new StringBuilder("--newline --progress --no-warnings ");

        var effectiveOutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : outputDirectory;

        if (!string.IsNullOrWhiteSpace(formatId))
        {
            builder.Append("-f ");
            builder.Append(Quote(formatId));
            builder.Append(' ');
        }

        var outputTemplate = string.IsNullOrWhiteSpace(outputName)
            ? Path.Combine(effectiveOutputDirectory, "%(title)s.%(ext)s")
            : Path.Combine(effectiveOutputDirectory, NormalizeOutputTemplate(outputName));

        builder.Append("-o ");
        builder.Append(Quote(outputTemplate));
        builder.Append(' ');

        builder.Append(Quote(url));
        return builder.ToString();
    }

    private static string NormalizeOutputTemplate(string outputName)
    {
        var template = outputName.Trim();
        if (template.Contains("%(ext)s", StringComparison.OrdinalIgnoreCase))
        {
            return template;
        }

        return Path.HasExtension(template)
            ? template
            : $"{template}.%(ext)s";
    }

    private static string Quote(string value)
    {
        return value.Contains(' ') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
    }
}
