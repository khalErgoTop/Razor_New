using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.AM
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class AMBogenModel : PageModel
    {
        private readonly AnbieterManagementService _service;

        public AMBogenModel(AnbieterManagementService service) => _service = service;

        [BindProperty(SupportsGet = true)] public string? EIID { get; set; }
        [BindProperty(SupportsGet = true)] public int? OID { get; set; }
        [BindProperty] public int ItemId { get; set; }
        [BindProperty] public string? ItemValue { get; set; }
        public IReadOnlyList<AmFacility> Facilities { get; private set; } = [];
        public AmFacility? SelectedFacility { get; private set; }
        public IReadOnlyList<AmCategory> Categories { get; private set; } = [];
        public AmCategory? Category { get; private set; }
        public bool CanEdit { get; private set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            Facilities = await _service.GetFacilitiesAsync(cancellationToken);
            CanEdit = await _service.CanEditAsync(cancellationToken);
            SelectedFacility = Facilities.FirstOrDefault(facility => facility.EIID == EIID) ?? Facilities.FirstOrDefault();
            if (SelectedFacility is null) return Page();

            EIID = SelectedFacility.EIID;
            Categories = await _service.GetCategoriesAsync(SelectedFacility.STUID, cancellationToken);
            Category = await _service.GetCategoryAsync(OID ?? Categories.FirstOrDefault()?.Id ?? 0,
                SelectedFacility.EIID, SelectedFacility.STUID, cancellationToken);
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync(string? eiid, int? oid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid) || ItemId <= 0 || !await _service.UpdateSimpleValueAsync(eiid, ItemId, ItemValue ?? string.Empty, cancellationToken))
                return Forbid();

            return RedirectToPage("/AM/AMBogen", new { EIID = eiid, OID = oid });
        }
    }
}
