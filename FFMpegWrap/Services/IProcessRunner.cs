namespace FFMpegWrap.Services;

public interface IProcessRunner
{
    Task<int> RunAsync(
        string fileName,
        string arguments,
        Action<string>? onStandardOutput = null,
        Action<string>? onStandardError = null,
        CancellationToken cancellationToken = default);
}
