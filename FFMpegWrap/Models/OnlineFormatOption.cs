namespace FFMpegWrap.Models;

public sealed class OnlineFormatOption
{
    public string FormatId { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public string Resolution { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string DisplayName => $"{FormatId} | {Resolution} | {Extension} | {Notes}";
}
