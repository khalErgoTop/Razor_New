using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.AM
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class AMBogenModel : PageModel
    {
        private readonly AnbieterManagementService _service;
        private readonly LegacySessionContext _session;
        private readonly ILogger<AMBogenModel> _logger;

        public AMBogenModel(AnbieterManagementService service, LegacySessionContext session, ILogger<AMBogenModel> logger)
        {
            _service = service;
            _session = session;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)] public string? EIID { get; set; }
        [BindProperty(SupportsGet = true)] public string? STUID { get; set; }
        [BindProperty(SupportsGet = true)] public int? OID { get; set; }
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [BindProperty] public Dictionary<int, string?> Values { get; set; } = [];
        [BindProperty] public Dictionary<int, string?> OriginalValues { get; set; } = [];
        [BindProperty] public Dictionary<int, string?> Selections { get; set; } = [];
        [BindProperty] public Dictionary<int, string?> SingleSelections { get; set; } = [];
        [BindProperty] public Dictionary<int, string?> OriginalSelections { get; set; } = [];
        public IReadOnlyList<AmFacility> Facilities { get; private set; } = [];
        public AmFacility? SelectedFacility { get; private set; }
        public IReadOnlyList<AmCategory> Categories { get; private set; } = [];
        public AmCategory? Category { get; private set; }
        public bool CanEdit { get; private set; }
        public bool MoreFacilities { get; private set; }
        public string? SearchMessage { get; private set; }
        [TempData] public string? SaveMessage { get; set; }
        public string? ErrorMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            CanEdit = await _service.CanEditAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(STUID))
            {
                if (string.IsNullOrWhiteSpace(EIID)) return Forbid();
                SelectedFacility = await _service.GetFacilityAsync(EIID, STUID, cancellationToken);
                if (SelectedFacility is null) return Forbid();
            }
            else if (!string.IsNullOrWhiteSpace(EIID))
            {
                SelectedFacility = await _service.GetFacilityAsync(EIID, null, cancellationToken);
                if (SelectedFacility is null) return Forbid();
            }
            else
            {
                var previousEiid = _session.CurrentEIID;
                var previousStuid = HttpContext.Session.GetString("CurrentSTUID");
                if (!string.IsNullOrWhiteSpace(previousEiid))
                {
                    if (!string.IsNullOrWhiteSpace(previousStuid))
                        SelectedFacility = await _service.GetFacilityAsync(previousEiid, previousStuid, cancellationToken);
                    SelectedFacility ??= await _service.GetFacilityAsync(previousEiid, null, cancellationToken);
                }
                SelectedFacility ??= await _service.GetFacilityAsync(null, null, cancellationToken);
            }
            if (SelectedFacility is null) return Page();

            EIID = SelectedFacility.EIID;
            STUID = SelectedFacility.STUID;
            _session.CurrentEIID = EIID;
            HttpContext.Session.SetString("CurrentSTUID", STUID);
            if (Search is not null)
            {
                Search = Search.Trim();
                if (Search.Length < 2)
                    SearchMessage = "Bitte geben Sie mindestens zwei Zeichen ein.";
                else if (Search.Length > 100)
                    SearchMessage = "Bitte geben Sie höchstens 100 Zeichen ein.";
                else
                {
                    const int maxResults = 20;
                    var results = await _service.SearchFacilitiesAsync(Search, maxResults + 1, cancellationToken);
                    MoreFacilities = results.Count > maxResults;
                    Facilities = results.Take(maxResults).ToArray();
                    if (Facilities.Count == 0) SearchMessage = "Keine passenden Standorte gefunden.";
                }
            }
            Categories = await _service.GetCategoriesAsync(SelectedFacility.STUID, cancellationToken);
            if (OID.HasValue && !Categories.Any(category => category.Id == OID.Value)) return NotFound();
            OID ??= Categories.FirstOrDefault()?.Id;
            if (OID.HasValue)
                Category = await _service.GetCategoryAsync(OID.Value, SelectedFacility.EIID, SelectedFacility.STUID, cancellationToken);
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync(string? eiid, string? stuid, int? oid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid) || string.IsNullOrWhiteSpace(stuid) || !oid.HasValue)
                return Forbid();
            EIID = eiid;
            STUID = stuid;
            OID = oid;
            var result = await OnGetAsync(cancellationToken);
            if (result is not PageResult) return result;
            if (!CanEdit) return Forbid();
            try
            {
                if (!ModelState.IsValid || !await _service.SaveValuesAsync(eiid, stuid, oid.Value, Values, OriginalValues, Selections, SingleSelections, OriginalSelections, cancellationToken))
                {
                    ErrorMessage = "Die Angaben konnten nicht gespeichert werden. Prüfen Sie Ihre Eingaben; möglicherweise wurden Werte zwischenzeitlich geändert.";
                    return Page();
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "AM-Bogen konnte nicht gespeichert werden.");
                ErrorMessage = "Die Angaben konnten nicht gespeichert werden. Möglicherweise wurden Werte zwischenzeitlich geändert oder es liegt ein Datenbankfehler vor.";
                return Page();
            }
            SaveMessage = "Die Änderungen wurden gespeichert.";
            return RedirectToPage("/TOPAM/AMBogen", new { EIID = eiid, STUID = stuid, OID = oid });
        }
    }
}
