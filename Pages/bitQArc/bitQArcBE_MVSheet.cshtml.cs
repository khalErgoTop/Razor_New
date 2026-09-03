using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.bitQArc;

[Authorize(Policy = "AuthenticatedUser")]
public sealed class bitQArcBE_MVSheetModel : PageModel
{
    private readonly QArcAdapter _adapter;
    private readonly LegacySessionContext _session;
    private readonly QArcAccessService _access;

    public bitQArcBE_MVSheetModel(QArcAdapter adapter, LegacySessionContext session, QArcAccessService access)
    {
        _adapter = adapter;
        _session = session;
        _access = access;
    }

    public QArcSheet? Sheet { get; private set; }
    public IReadOnlyList<QArcCategory> Categories { get; private set; } = [];
    public IReadOnlyList<QArcMatrix> Matrices { get; private set; } = [];
    public bool CanEdit { get; private set; }
    public string TablePrefix { get; private set; } = "BE";

    public async Task<IActionResult> OnGetAsync(
        string? f,
        string? e,
        int? l,
        int? fk,
        CancellationToken cancellationToken)
    {
        TablePrefix = string.IsNullOrWhiteSpace(f) ? "BE" : f.ToUpperInvariant();
        if (!TablePrefix.Equals("BE", StringComparison.OrdinalIgnoreCase)) return BadRequest("Die BE-Seite benötigt den Präfix BE.");

        var eiid = string.IsNullOrWhiteSpace(e) ? _session.CurrentEIID : e;
        if (string.IsNullOrWhiteSpace(eiid) || l is not int layoutId || layoutId <= 0 || fk is not int foreignKeyId || foreignKeyId < 0)
        {
            return BadRequest("Die Parameter f, e, l und fk sind erforderlich.");
        }

        Sheet = await _adapter.GetSheetAsync(TablePrefix, eiid, layoutId, foreignKeyId, cancellationToken);
        if (Sheet is null) return NotFound();
        var access = await _access.GetAccessAsync(eiid, TablePrefix, Sheet.Status, cancellationToken);
        if (!access.CanView) return Forbid();
        CanEdit = access.CanEdit;

        Categories = await _adapter.GetCategoriesAsync(TablePrefix, eiid, layoutId, foreignKeyId, cancellationToken);
        Matrices = await _adapter.GetMatricesAsync(TablePrefix, eiid, layoutId, foreignKeyId, cancellationToken);
        _session.CurrentEIID = eiid;
        return Page();
    }
}
