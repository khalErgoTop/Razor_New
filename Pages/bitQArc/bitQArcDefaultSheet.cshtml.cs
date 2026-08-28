using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Legacy.Adapters;

namespace TOPqwWeb_Core.Pages.bitQArc;

[Authorize(Policy = "AuthenticatedUser")]
public sealed class bitQArcDefaultSheetModel : PageModel
{
    private readonly QArcAdapter _adapter;
    private readonly LegacySessionContext _session;

    public bitQArcDefaultSheetModel(QArcAdapter adapter, LegacySessionContext session)
    {
        _adapter = adapter;
        _session = session;
    }

    public QArcSheet? Sheet { get; private set; }
    public IReadOnlyList<QArcCategory> Categories { get; private set; } = [];
    public string TablePrefix { get; private set; } = "KV";

    public async Task<IActionResult> OnGetAsync(
        string? f,
        string? e,
        int? l,
        int? fk,
        CancellationToken cancellationToken)
    {
        TablePrefix = string.IsNullOrWhiteSpace(f) ? "KV" : f.ToUpperInvariant();
        var eiid = string.IsNullOrWhiteSpace(e) ? _session.CurrentEIID : e;
        if (string.IsNullOrWhiteSpace(eiid) || l is not int layoutId || layoutId <= 0 || fk is not int foreignKeyId || foreignKeyId < 0)
        {
            return BadRequest("Die Parameter f, e, l und fk sind erforderlich.");
        }

        Sheet = await _adapter.GetSheetAsync(TablePrefix, eiid, layoutId, foreignKeyId, cancellationToken);
        if (Sheet is null) return NotFound();
        Categories = await _adapter.GetCategoriesAsync(TablePrefix, eiid, layoutId, foreignKeyId, cancellationToken);
        _session.CurrentEIID = eiid;
        return Page();
    }
}
