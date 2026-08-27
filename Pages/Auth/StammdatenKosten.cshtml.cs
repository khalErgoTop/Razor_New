using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Auth;

namespace TOPqwWeb_Core.Pages.Auth;

[Authorize(Policy = "AuthenticatedUser")]
public sealed class StammdatenKostenModel : PageModel
{
    private readonly StammdatenKostenPageService _service;
    private readonly LegacySessionContext _session;

    public StammdatenKostenModel(StammdatenKostenPageService service, LegacySessionContext session)
    {
        _service = service;
        _session = session;
    }

    public string EIID { get; private set; } = string.Empty;
    public StammdatenKostenAccess Access { get; private set; } = new(false, false, false, false);
    public IReadOnlyList<StammdatenKostenTimeFrame> TimeFrames { get; private set; } = [];
    public IReadOnlyList<StammdatenKostenRow> Rows { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public int? HBGWZID { get; set; }
    [BindProperty] public DateTime DateFrom { get; set; }
    [BindProperty] public DateTime DateTo { get; set; }

    public async Task<IActionResult> OnGetAsync(string? eiid, CancellationToken cancellationToken)
    {
        EIID = string.IsNullOrWhiteSpace(eiid) ? _session.CurrentEIID ?? string.Empty : eiid;
        if (!string.IsNullOrWhiteSpace(EIID)) _session.CurrentEIID = EIID;
        Access = await _service.GetAccessAsync(cancellationToken);
        if (!Access.CanView || string.IsNullOrWhiteSpace(EIID)) return Forbid();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveTimeFrameAsync(string? eiid, CancellationToken cancellationToken)
    {
        EIID = ResolveEiid(eiid);
        Access = await _service.GetAccessAsync(cancellationToken);
        if (!Access.CanEdit || string.IsNullOrWhiteSpace(EIID)) return Forbid();
        if (DateFrom == default || DateTo == default || DateTo < DateFrom) return BadRequest();
        if (!await _service.SaveTimeFrameAsync(EIID, HBGWZID ?? 0, DateFrom, DateTo, cancellationToken)) return Forbid();
        return RedirectToPage(new { eiid = EIID });
    }

    public async Task<IActionResult> OnPostDeleteTimeFrameAsync(string? eiid, int hbgwzid, CancellationToken cancellationToken)
    {
        EIID = ResolveEiid(eiid);
        Access = await _service.GetAccessAsync(cancellationToken);
        if (!Access.CanDelete || string.IsNullOrWhiteSpace(EIID)) return Forbid();
        if (!await _service.DeleteTimeFrameAsync(EIID, hbgwzid, cancellationToken)) return Forbid();
        return RedirectToPage(new { eiid = EIID });
    }

    public async Task<IActionResult> OnPostSaveRowAsync(string? eiid, int hbgwzid, StammdatenKostenRow row, CancellationToken cancellationToken)
    {
        EIID = ResolveEiid(eiid);
        Access = await _service.GetAccessAsync(cancellationToken);
        if (!Access.CanEdit || string.IsNullOrWhiteSpace(EIID)) return Forbid();
        if (!await _service.SaveRowAsync(EIID, hbgwzid, row, cancellationToken)) return Forbid();
        return RedirectToPage(new { eiid = EIID, HBGWZID = hbgwzid });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        TimeFrames = await _service.GetTimeFramesAsync(EIID, cancellationToken);
        if (!HBGWZID.HasValue || !TimeFrames.Any(timeframe => timeframe.HBGWZID == HBGWZID.Value))
        {
            HBGWZID = TimeFrames.FirstOrDefault()?.HBGWZID;
        }

        Rows = HBGWZID is int id ? await _service.GetRowsAsync(EIID, id, cancellationToken) : [];
    }

    private string ResolveEiid(string? eiid)
    {
        var resolved = string.IsNullOrWhiteSpace(eiid) ? _session.CurrentEIID ?? string.Empty : eiid;
        if (!string.IsNullOrWhiteSpace(resolved)) _session.CurrentEIID = resolved;
        return resolved;
    }
}
