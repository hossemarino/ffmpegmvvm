using Microsoft.Win32;

namespace FFMpegWrap.Services;

public sealed class FileDialogService : IFileDialogService
{
    public IReadOnlyList<string> OpenMediaFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select audio or video files",
            Multiselect = true,
            Filter = "Media files|*.mp4;*.mkv;*.mov;*.avi;*.mp3;*.wav;*.flac;*.m4a;*.ogg|All files|*.*"
        };

        return dialog.ShowDialog() == true
            ? dialog.FileNames
            : [];
    }

    public string? PickOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select output folder"
        };

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }

    public string? PickExecutableFile(string toolName, string? initialPath = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = $"Locate {toolName}.exe",
            Filter = "Executable files|*.exe|All files|*.*",
            FileName = initialPath ?? string.Empty
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }
}
