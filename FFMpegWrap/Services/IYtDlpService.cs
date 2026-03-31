using FFMpegWrap.Models;

namespace FFMpegWrap.Services;

public interface IYtDlpService
{
    Task<IReadOnlyList<OnlineFormatOption>> GetAvailableFormatsAsync(string url, CancellationToken cancellationToken = default);

    Task DownloadAsync(
        string url,
        string? formatId,
        string? outputName,
        string? outputDirectory,
        IProgress<double>? progress = null,
        IProgress<string>? log = null,
        CancellationToken cancellationToken = default);
}
