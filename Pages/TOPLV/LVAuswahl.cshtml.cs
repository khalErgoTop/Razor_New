using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using TOPqwWeb_Core.Services.Modules;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Pages.LV
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class LVAuswahlModel : PageModel
    {
        private readonly LvService _service;
        private readonly LegacySessionContext _session;
        private readonly CurrentUserAccessor _currentUser;
        private const int PageSize = 10;

        public LVAuswahlModel(LvService service, LegacySessionContext session, CurrentUserAccessor currentUser)
        {
            _service = service;
            _session = session;
            _currentUser = currentUser;
        }

        [BindProperty(SupportsGet = true)] public int? Bogenstatus { get; set; }
        [BindProperty(SupportsGet = true)] public int? Kreis { get; set; }
        [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
        public IReadOnlyList<LvSelectionItem> Items { get; private set; } = [];
        public IReadOnlyList<LvDistrict> Districts { get; private set; } = [];
        public int PageCount { get; private set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            if (Bogenstatus == -1) Bogenstatus = null;
            if (Kreis == -1) Kreis = null;
            PageNumber = Math.Max(1, PageNumber);
            Districts = await _service.GetDistrictsAsync(cancellationToken);
            var count = await _service.CountSelectionAsync(Bogenstatus, Kreis, cancellationToken);
            PageCount = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
            PageNumber = Math.Min(PageNumber, PageCount);
            Items = await _service.GetSelectionAsync(Bogenstatus, Kreis, PageNumber, PageSize, cancellationToken);
            return Page();
        }

        public async Task<IActionResult> OnPostOpenAsync(int bid, CancellationToken cancellationToken)
        {
            if (bid <= 0) return BadRequest();
            var bogen = await _service.GetBogenAsync(bid, cancellationToken);
            if (bogen is null) return NotFound();
            if (_currentUser.UserType is CurrentUserAccessor.LegacyUserTypes.Gast)
                return Forbid();
            if (_currentUser.UserType == CurrentUserAccessor.LegacyUserTypes.TOPqw && bogen.Status is 0 or 1 or 2)
                return Forbid();
            _session.CurrentEIID = bogen.EIID;
            _session.LVBogenID = bogen.Id;
            return RedirectToPage("/TOPLV/LVBogen", new { bid = bogen.Id });
        }
    }
}
