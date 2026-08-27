using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Services.Administration;

namespace TOPqwWeb_Core.Pages.Auth;

[Authorize(Policy = "AuthenticatedUser")]
public class OptionenModel : PageModel
{
    private readonly OptionenService _service;
    public OptionenModel(OptionenService service) => _service = service;

    public IReadOnlyList<OptionCategory> Categories { get; private set; } = [];
    public OptionPage Options { get; private set; } = new([], 0);
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
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(string setting, string dataType, string changedValue, CancellationToken cancellationToken)
    {
        if (!await _service.CanAccessAsync(cancellationToken)) return Forbid();
        if (string.IsNullOrWhiteSpace(setting) || dataType is not ("WertTXT" or "WertMEMO" or "WertBOOL" or "WertSNG" or "WertLONG")) return BadRequest();
        await _service.SaveAsync(setting, dataType, changedValue, cancellationToken);
        return RedirectToPage(new { Category, Search, PageIndex, PageSize, SortField, SortDescending });
    }
}