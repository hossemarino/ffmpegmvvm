namespace FFMpegWrap.Services;

public interface IFileDialogService
{
    IReadOnlyList<string> OpenMediaFiles();

    string? PickOutputFolder();

    string? PickExecutableFile(string toolName, string? initialPath = null);
}
