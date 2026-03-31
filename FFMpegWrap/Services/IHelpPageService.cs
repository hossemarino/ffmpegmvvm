using FFMpegWrap.Models;

namespace FFMpegWrap.Services;

public interface IHelpPageService
{
    Task<IReadOnlyList<HelpCategory>> GetHelpCategoriesAsync(string toolName, CancellationToken cancellationToken = default);
}
