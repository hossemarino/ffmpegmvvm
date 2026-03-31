using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using FFMpegWrap.Models;

namespace FFMpegWrap.Services;

/// <summary>
/// Loads and parses external tool help output into searchable categories.
/// Live process output is cached per tool so the explorer remains responsive after the first load.
/// </summary>
public sealed partial class HelpPageService : IHelpPageService
{
    private static readonly Regex OptionWithDescriptionRegex = new(@"^(?<flags>-{1,2}.+?)(?:\s{2,}|\t+)(?<description>.+)$", RegexOptions.Compiled);
    private readonly ConcurrentDictionary<string, IReadOnlyList<HelpCategory>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IProcessRunner _processRunner;
    private readonly IToolPathService _toolPathService;

    public HelpPageService(IProcessRunner processRunner, IToolPathService toolPathService)
    {
        _processRunner = processRunner;
        _toolPathService = toolPathService;
    }

    public async Task<IReadOnlyList<HelpCategory>> GetHelpCategoriesAsync(string toolName, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(toolName, out var cachedCategories))
        {
            return CloneCategories(cachedCategories);
        }

        var helpOutput = await GetHelpOutputAsync(toolName, cancellationToken);
        var categories = ParseHelpOutput(toolName, helpOutput);
        _cache[toolName] = categories;
        return CloneCategories(categories);
    }

    private async Task<string> GetHelpOutputAsync(string toolName, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var errors = new StringBuilder();
        var executablePath = _toolPathService.ResolveToolPath(toolName);
        var arguments = GetHelpArguments(toolName);

        try
        {
            var exitCode = await _processRunner.RunAsync(
                executablePath,
                arguments,
                line => output.AppendLine(line),
                line => errors.AppendLine(line),
                cancellationToken);

            var combinedOutput = CombineOutput(output, errors);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(combinedOutput))
            {
                return combinedOutput;
            }
        }
        catch
        {
        }

        return GetSampleHelp(toolName);
    }

    private static string CombineOutput(StringBuilder output, StringBuilder errors)
    {
        return $"{output}{Environment.NewLine}{errors}".Trim();
    }

    private static IReadOnlyList<HelpCategory> ParseHelpOutput(string toolName, string helpText)
    {
        var categories = new List<HelpCategory>();
        var currentCategory = new HelpCategory
        {
            Name = "General",
            Entries = []
        };

        categories.Add(currentCategory);
        HelpEntry? lastEntry = null;

        foreach (var rawLine in helpText.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var trimmedLine = rawLine.Trim();
            if (IsCategoryLine(rawLine, trimmedLine))
            {
                currentCategory = new HelpCategory
                {
                    Name = trimmedLine.TrimEnd(':'),
                    Entries = []
                };

                categories.Add(currentCategory);
                lastEntry = null;
                continue;
            }

            if (TryParseOptionLine(trimmedLine, out var flags, out var description))
            {
                lastEntry = new HelpEntry
                {
                    Flags = flags,
                    Description = description,
                    ExampleUsage = BuildExample(toolName, flags)
                };

                currentCategory.Entries.Add(lastEntry);
                continue;
            }

            if (lastEntry is not null && char.IsWhiteSpace(rawLine[0]))
            {
                lastEntry.Description = $"{lastEntry.Description} {trimmedLine}".Trim();
            }
        }

        return categories
            .Where(category => category.Entries.Count > 0)
            .Select(category => new HelpCategory
            {
                Name = category.Name,
                Entries = new ObservableCollection<HelpEntry>(category.Entries)
            })
            .ToList();
    }

    private static bool IsCategoryLine(string rawLine, string trimmedLine)
    {
        if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.Length > 100)
        {
            return false;
        }

        return !char.IsWhiteSpace(rawLine[0])
            && trimmedLine.EndsWith(':')
            && !trimmedLine.StartsWith('-');
    }

    private static bool TryParseOptionLine(string trimmedLine, out string flags, out string description)
    {
        flags = string.Empty;
        description = string.Empty;

        if (!trimmedLine.StartsWith('-'))
        {
            return false;
        }

        var match = OptionWithDescriptionRegex.Match(trimmedLine);
        if (!match.Success)
        {
            return false;
        }

        flags = match.Groups["flags"].Value.Trim();
        description = match.Groups["description"].Value.Trim();
        return !string.IsNullOrWhiteSpace(flags) && !string.IsNullOrWhiteSpace(description);
    }

    private static string BuildExample(string toolName, string flags)
    {
        var primaryFlag = flags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? flags;

        return toolName.ToLowerInvariant() switch
        {
            "yt-dlp" => $"yt-dlp {NormalizeExampleFlag(primaryFlag)} https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            "ffplay" => $"ffplay {NormalizeExampleFlag(primaryFlag)} input.mp4",
            "ffprobe" => $"ffprobe {NormalizeExampleFlag(primaryFlag)} input.mp4",
            _ => $"ffmpeg {NormalizeExampleFlag(primaryFlag)} -i input.mp4 output.mp4"
        };
    }

    private static string NormalizeExampleFlag(string flag)
    {
        return flag
            .Replace("<file>", "value", StringComparison.OrdinalIgnoreCase)
            .Replace("<format>", "value", StringComparison.OrdinalIgnoreCase)
            .Replace("<time>", "00:00:10", StringComparison.OrdinalIgnoreCase)
            .Replace("<num>", "1", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetHelpArguments(string toolName)
    {
        return toolName.ToLowerInvariant() switch
        {
            "ffmpeg" => "-h long",
            "ffplay" => "-h",
            "ffprobe" => "-h",
            "yt-dlp" => "--help",
            _ => "--help"
        };
    }

    private static IReadOnlyList<HelpCategory> CloneCategories(IReadOnlyList<HelpCategory> categories)
    {
        return categories
            .Select(category => new HelpCategory
            {
                Name = category.Name,
                Entries = new ObservableCollection<HelpEntry>(
                    category.Entries.Select(entry => new HelpEntry
                    {
                        Flags = entry.Flags,
                        Description = entry.Description,
                        ExampleUsage = entry.ExampleUsage
                    }))
            })
            .ToList();
    }

    private static string GetSampleHelp(string toolName)
    {
        return toolName.ToLowerInvariant() switch
        {
            "ffplay" => "Playback options:\n  -autoexit            Exit at the end of playback\n  -fs                  Start in full screen\n  -volume <num>        Set startup volume\nWindow options:\n  -x <width>           Force displayed width\n  -y <height>          Force displayed height",
            "ffprobe" => "Inspection options:\n  -show_streams        Show stream information\n  -show_format         Show container information\n  -of <format>         Set output format\nSelection options:\n  -select_streams <s>  Select streams to read",
            "yt-dlp" => "General options:\n  -f, --format FORMAT  Video format code\n  -o, --output PATH    Output filename template\n  --write-info-json    Write video metadata to JSON\nNetwork options:\n  --proxy URL          Use the specified proxy\n  --cookies FILE       Read cookies from file",
            _ => "Input options:\n  -i <file>            Input media file\n  -ss <time>           Seek to a position\nCodec options:\n  -c:v <codec>         Set video codec\n  -c:a <codec>         Set audio codec\nOutput options:\n  -y                   Overwrite output files\n  -f <format>          Force output format"
        };
    }
}
