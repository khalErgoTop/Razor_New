using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Auth;

namespace TOPqwWeb_Core.Pages;

[Authorize(Policy = "AuthenticatedUser")]
public sealed class StammdatenAuswertungModel : PageModel
{
    private readonly StammdatenAnalysisPageService _service;

    public StammdatenAuswertungModel(StammdatenAnalysisPageService service)
    {
        _service = service;
    }

    public IReadOnlyList<StammdatenAnalysisTypeResponse> AnalysisTypes { get; private set; } = [];
    public IReadOnlyList<StammdatenAnalysisResponse> TqwAnalyses { get; private set; } = [];
    public IReadOnlyList<StammdatenDashboardResponse> Dashboards { get; private set; } = [];
    public StammdatenAnalysisFiltersResponse Filters { get; private set; } =
        new([], []);

    [BindProperty]
    public int AnalysisType { get; set; } = 2;
    [BindProperty]
    public int? AnalysisId { get; set; }
    [BindProperty]
    public string? DashboardName { get; set; }
    [BindProperty]
    public List<int> FacilityTypeSelection { get; set; } = [];
    [BindProperty]
    public List<int> DistrictSelection { get; set; } = [];
    public StammdatenAnalysisResultResponse? AnalysisResult { get; private set; }
    public StammdatenDashboardResultResponse? DashboardResult { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task OnPostLoadDashboardAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(DashboardName))
        {
            ErrorMessage = "Bitte wählen Sie ein Dashboard aus.";
            return;
        }

        try
        {
            DashboardResult = await _service.GetDashboardAsync(DashboardName, cancellationToken);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        {
            ErrorMessage = ex.Message;
        }
    }

    public async Task<IActionResult> OnPostSaveDashboardRestrictionsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(DashboardName)) return BadRequest();

        var restrictions = new List<StammdatenDashboardRestrictionInput>
        {
            new("Einrichtungstyp", string.Join(",", FacilityTypeSelection), "IN"),
            new("Kreis", string.Join(",", DistrictSelection), "IN")
        };

        try
        {
            await _service.SaveDashboardRestrictionsAsync(DashboardName, restrictions, cancellationToken);
            return RedirectToPage(new { DashboardName });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    public async Task OnPostLoadAnalysisAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        if (!AnalysisId.HasValue)
        {
            ErrorMessage = "Bitte wählen Sie eine Auswertung aus.";
            return;
        }

        var selections = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Einrichtungstyp"] = string.Join(",", FacilityTypeSelection),
            ["Kreis"] = string.Join(",", DistrictSelection)
        };

        try
        {
            AnalysisResult = await _service.GetTqwAnalysisAsync(AnalysisId.Value, selections, cancellationToken);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or NotSupportedException or InvalidOperationException)
        {
            ErrorMessage = ex.Message;
        }
    }

    public async Task OnPostReloadAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        AnalysisTypes = _service.GetAnalysisTypes();
        Filters = await _service.GetFiltersAsync(cancellationToken);
        TqwAnalyses = await _service.GetTqwAnalysesAsync(cancellationToken);
        Dashboards = await _service.GetDashboardsAsync(cancellationToken);
    }
}