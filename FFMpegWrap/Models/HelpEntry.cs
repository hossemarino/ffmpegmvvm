namespace FFMpegWrap.Models;

public sealed class HelpEntry
{
    public string Flags { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ExampleUsage { get; set; } = string.Empty;
}
