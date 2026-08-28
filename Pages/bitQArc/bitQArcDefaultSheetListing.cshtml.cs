using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Legacy.Adapters;

namespace TOPqwWeb_Core.Pages.bitQArc;

[Authorize(Policy = "AuthenticatedUser")]
public sealed class bitQArcDefaultSheetListingModel : PageModel
{
    private readonly QArcAdapter _adapter;
    private readonly LegacySessionContext _session;

    public bitQArcDefaultSheetListingModel(QArcAdapter adapter, LegacySessionContext session)
    {
        _adapter = adapter;
        _session = session;
    }

    public string TablePrefix { get; private set; } = "KV";
    public string EIID { get; private set; } = string.Empty;
    public IReadOnlyList<QArcLayout> Layouts { get; private set; } = [];
    public IReadOnlyList<QArcSheet> Sheets { get; private set; } = [];
    public int LayoutId { get; private set; }
    public int Status { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string? f,
        string? e,
        int? l,
        int? status,
        CancellationToken cancellationToken)
    {
        var requestedPrefix = string.IsNullOrWhiteSpace(f) ? "KV" : f.ToUpperInvariant();
        TablePrefix = requestedPrefix == "KB" ? "KV" : requestedPrefix;
        EIID = string.IsNullOrWhiteSpace(e) ? _session.CurrentEIID ?? string.Empty : e;
        if (string.IsNullOrWhiteSpace(EIID)) return BadRequest("Eine Einrichtung ist erforderlich.");

        Layouts = await _adapter.GetLayoutsAsync(TablePrefix, cancellationToken);
        LayoutId = l ?? Layouts.FirstOrDefault()?.Id ?? 0;
        Status = status ?? 0;
        if (LayoutId > 0)
        {
            Sheets = await _adapter.GetSheetsAsync(TablePrefix, EIID, LayoutId, Status, cancellationToken);
        }

        _session.CurrentEIID = EIID;
        return Page();
    }
}
