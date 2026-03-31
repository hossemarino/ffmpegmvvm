using System.Collections.ObjectModel;
using FFMpegWrap.Models;
using FFMpegWrap.Services;

namespace FFMpegWrap.ViewModels;

public sealed class CommandExplorerViewModel : ObservableObject
{
    private readonly IHelpPageService _helpPageService;
    private readonly AsyncRelayCommand _loadHelpCommand;
    private string _selectedTool = "ffmpeg";
    private HelpCategory? _selectedCategory;
    private string _searchText = string.Empty;
    private string _statusMessage = "Load a tool help page to explore command options.";

    public CommandExplorerViewModel(IHelpPageService helpPageService)
    {
        _helpPageService = helpPageService;
        _loadHelpCommand = new AsyncRelayCommand(LoadHelpAsync);

        foreach (var tool in new[] { "ffmpeg", "ffplay", "ffprobe", "yt-dlp" })
        {
            Tools.Add(tool);
        }

        _ = LoadHelpAsync();
    }

    public ObservableCollection<string> Tools { get; } = [];

    public ObservableCollection<HelpCategory> Categories { get; } = [];

    public ObservableCollection<HelpEntry> VisibleEntries { get; } = [];

    public AsyncRelayCommand LoadHelpCommand => _loadHelpCommand;

    public string SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (SetProperty(ref _selectedTool, value))
            {
                _ = LoadHelpAsync();
            }
        }
    }

    public HelpCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                RefreshVisibleEntries();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshVisibleEntries();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private async Task LoadHelpAsync()
    {
        try
        {
            StatusMessage = $"Loading {SelectedTool} help...";
            Categories.Clear();
            VisibleEntries.Clear();

            var categories = await _helpPageService.GetHelpCategoriesAsync(SelectedTool);
            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            SelectedCategory = Categories.FirstOrDefault();
            StatusMessage = Categories.Count == 0
                ? "No help content available."
                : $"Loaded {Categories.Count} help categor{(Categories.Count == 1 ? "y" : "ies")}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load help: {ex.Message}";
        }
    }

    private void RefreshVisibleEntries()
    {
        VisibleEntries.Clear();

        if (SelectedCategory is null)
        {
            return;
        }

        var entries = SelectedCategory.Entries
            .Where(entry => string.IsNullOrWhiteSpace(SearchText)
                || entry.Flags.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || entry.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || entry.ExampleUsage.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in entries)
        {
            VisibleEntries.Add(entry);
        }
    }
}
