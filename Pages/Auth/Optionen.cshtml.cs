using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Services.Administration;

namespace TOPqwWeb_Core.Pages.Auth;

[Authorize(Policy = "AuthenticatedUser")]
public class OptionenModel : PageModel
{
    private readonly OptionenService _service;
    private readonly ILogger<OptionenModel> _logger;
    public OptionenModel(OptionenService service, ILogger<OptionenModel> logger)
    {
        _service = service;
        _logger = logger;
    }

    public IReadOnlyList<OptionCategory> Categories { get; private set; } = [];
    public OptionPage Options { get; private set; } = new([], 0);
    public string MapsSearchService { get; private set; } = "bing";
    public IReadOnlyList<DisplayOption> DisplayOptions { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageIndex { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public string SortField { get; set; } = "Einstellung";
    [BindProperty(SupportsGet = true)] public bool SortDescending { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await _service.CanAccessAsync(cancellationToken)) return Forbid();
        Categories = await _service.GetCategoriesAsync(cancellationToken);
        Options = await _service.GetOptionsAsync(new OptionQuery(Category, Search, PageIndex, PageSize, SortField, SortDescending), cancellationToken);
        MapsSearchService = await _service.GetMapsSearchServiceAsync(cancellationToken);
        DisplayOptions = await _service.GetDisplayOptionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(string? setting, string? dataType, string? changedValue, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Option save POST received: setting={Setting}, dataType={DataType}, valueLength={ValueLength}", setting, dataType, changedValue?.Length ?? 0);
        if (!await _service.CanAccessAsync(cancellationToken)) return Forbid();
        if (string.IsNullOrWhiteSpace(setting) || dataType is not ("WertTXT" or "WertMEMO" or "WertBOOL" or "WertSNG" or "WertLONG" or "WertINT")) return BadRequest();
        await _service.SaveAsync(setting, dataType, changedValue ?? string.Empty, cancellationToken);
        return RedirectToPage(new { Category, Search, PageIndex, PageSize, SortField, SortDescending });
    }

    public async Task<IActionResult> OnPostSaveSpecialAsync(string? mapsSearchService, string[]? displayOptionSettings, CancellationToken cancellationToken)
    {
        if (!await _service.CanAccessAsync(cancellationToken)) return Forbid();
        if (mapsSearchService is not ("bing" or "google" or "berlin.de")) return BadRequest();

        await _service.SaveMapsSearchServiceAsync(mapsSearchService, cancellationToken);
        var selected = displayOptionSettings is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : displayOptionSettings.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var displayOptions = await _service.GetDisplayOptionsAsync(cancellationToken);
        foreach (var option in displayOptions.Where(option => option.Aenderbar))
        {
            await _service.SaveDisplayOptionAsync(option.Einstellung, selected.Contains(option.Einstellung), cancellationToken);
        }

        return RedirectToPage(new { Category, Search, PageIndex, PageSize, SortField, SortDescending });
    }
}