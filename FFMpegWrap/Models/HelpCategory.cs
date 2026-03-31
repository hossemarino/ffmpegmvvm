using System.Collections.ObjectModel;

namespace FFMpegWrap.Models;

public sealed class HelpCategory
{
    public string Name { get; set; } = string.Empty;

    public ObservableCollection<HelpEntry> Entries { get; set; } = [];
}
