namespace FFMpegWrap.Services;

public interface IToolPathService
{
    string ResolveToolPath(string toolName);

    ToolPathSettings GetSettings();

    void SaveSettings(ToolPathSettings settings);
}
