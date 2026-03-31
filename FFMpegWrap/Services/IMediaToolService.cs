using FFMpegWrap.Models;

namespace FFMpegWrap.Services;

public interface IMediaToolService
{
    IReadOnlyList<string> GetSupportedTools();

    Task<IReadOnlyList<ToolCommandOption>> GetOptionsAsync(string toolName, CancellationToken cancellationToken = default);

    Task<Uri?> CreatePreviewSourceAsync(string? filePath, CancellationToken cancellationToken = default);

    string BuildCommandLine(string toolName, string? inputPath, string? outputPath, IEnumerable<ToolCommandOption> options);

    Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        string? inputPath,
        string? outputPath,
        IEnumerable<ToolCommandOption> options,
        IProgress<string>? log = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaMetadataItem>> GetMetadataAsync(string? filePath, CancellationToken cancellationToken = default);
}
