using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.Kalkulation;

[Authorize(Policy = "AuthenticatedUser")]
public class QWKalkulationModel : PageModel
{
    private readonly KalkulationService _service;
    private readonly KalkulationAdapter _adapter;
    private readonly CurrentUserAccessor _currentUser;

    public QWKalkulationModel(KalkulationService service, KalkulationAdapter adapter, CurrentUserAccessor currentUser)
    {
        _service = service;
        _adapter = adapter;
        _currentUser = currentUser;
    }

    [BindProperty(SupportsGet = true)]
    public string? EIID { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Section { get; set; }

    [BindProperty]
    public Dictionary<int, double?> AngebotET { get; set; } = [];

    public bool CanEdit => _currentUser.IsInLegacyUserType(
        CurrentUserAccessor.LegacyUserTypes.Traeger,
        CurrentUserAccessor.LegacyUserTypes.Einrichtung,
        CurrentUserAccessor.LegacyUserTypes.Einrichtungsverwalter);

    public bool CanEditPost(KalkulationNavigationItem post) =>
        CanEdit && Data?.Status == 0 && post.Input &&
        post.Oberkategorie != 1 &&
        !string.Equals(post.Kostenbestandteil, "Abschreibungen", StringComparison.Ordinal) &&
        !string.Equals(post.Kostenbestandteil, "Instandhaltung Gebäude", StringComparison.Ordinal);

    public string? SaveMessage { get; private set; }

    public KalkulationPageData? Data { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await _service.HasAccessAsync(cancellationToken)) return Forbid();
        if (string.IsNullOrWhiteSpace(EIID)) return RedirectToPage("/Kalkulation/QWKalkulationsauswahl");
        Data = await _adapter.GetPageAsync(EIID, cancellationToken);
        if (Data is null) return NotFound();
        return Page();
    }

    public IEnumerable<KalkulationNavigationItem> VisiblePosts =>
        Data?.Posts.Where(post => Section is null && (Category is null || post.Oberkategorie == Category.Value)) ?? [];

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!await _service.HasAccessAsync(cancellationToken)) return Forbid();
        if (!CanEdit || string.IsNullOrWhiteSpace(EIID)) return Forbid();

        Data = await _adapter.GetPageAsync(EIID, cancellationToken);
        if (Data is null) return NotFound();

        foreach (var post in Data.Posts.Where(post => CanEditPost(post) && AngebotET.TryGetValue(post.Id, out var value) && value.HasValue))
        {
            if (!double.IsFinite(AngebotET[post.Id]!.Value))
            {
                ModelState.AddModelError($"AngebotET[{post.Id}]", "Der Wert ist ungültig.");
                continue;
            }

            var saved = await _adapter.UpdateAngebotETAsync(EIID, Data.Id, post.Id, AngebotET[post.Id]!.Value, cancellationToken);
            if (!saved) ModelState.AddModelError(string.Empty, "Mindestens ein Wert konnte nicht gespeichert werden.");
        }

        if (ModelState.IsValid) SaveMessage = "Die Eingabewerte wurden gespeichert.";
        Data = await _adapter.GetPageAsync(EIID, cancellationToken);
        return Page();
    }
}
