using FFMpegWrap.ViewModels;

namespace FFMpegWrap.Models;

public sealed class ToolCommandOption : ObservableObject
{
    private bool _isEnabled;
    private string? _value;

    public string ToolName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Flag { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ExampleUsage { get; set; } = string.Empty;

    public bool RequiresValue { get; set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string? Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}
