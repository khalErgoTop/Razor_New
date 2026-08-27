using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.Kalkulation;

[Authorize(Policy = "AuthenticatedUser")]
public class QWKalkulationsauswahlModel : PageModel
{
    private readonly KalkulationService _service;

    public QWKalkulationsauswahlModel(KalkulationService service) => _service = service;

    public IReadOnlyList<KalkulationDistrict> Districts { get; private set; } = [];
    public IReadOnlyList<KalkulationFacility> Facilities { get; private set; } = [];
    public string? Message { get; private set; }

    [BindProperty(SupportsGet = true)] public string District { get; set; } = "_00";
    [BindProperty(SupportsGet = true)] public int? Status { get; set; }
    [BindProperty] public string? SelectedFacility { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await _service.HasAccessAsync(cancellationToken)) return Forbid();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostFilterAsync(CancellationToken cancellationToken)
    {
        if (!await _service.HasAccessAsync(cancellationToken)) return Forbid();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostOpenAsync(CancellationToken cancellationToken)
    {
        if (!await _service.HasAccessAsync(cancellationToken)) return Forbid();
        if (string.IsNullOrWhiteSpace(SelectedFacility))
        {
            Message = "Bitte wählen Sie eine Einrichtung aus.";
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage("/Kalkulation/QWKalkulation", new { EIID = SelectedFacility });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Districts = await _service.GetDistrictsAsync(cancellationToken);
        Facilities = await _service.GetFacilitiesAsync(District, Status, cancellationToken);
    }
}
