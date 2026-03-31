using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace FFMpegWrap.Services;

/// <summary>
/// Shared process runner used by ffmpeg-family tools and yt-dlp.
/// It keeps process execution testable and isolates external process concerns from view models.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(
        string fileName,
        string arguments,
        Action<string>? onStandardOutput = null,
        Action<string>? onStandardError = null,
        CancellationToken cancellationToken = default)
    {
        var workingDirectory = ResolveWorkingDirectory(fileName);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        var exitSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                onStandardOutput?.Invoke(args.Data);
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                onStandardError?.Invoke(args.Data);
            }
        };

        process.Exited += (_, _) => exitSource.TrySetResult(process.ExitCode);

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Unable to start process '{fileName}'.");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Unable to start '{fileName}'. Verify that the executable exists or configure its full path. {ex.Message}",
                ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        });

        return await exitSource.Task.WaitAsync(cancellationToken);
    }

    private static string ResolveWorkingDirectory(string fileName)
    {
        if (Path.IsPathRooted(fileName))
        {
            var directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }

        return AppContext.BaseDirectory;
    }
}
