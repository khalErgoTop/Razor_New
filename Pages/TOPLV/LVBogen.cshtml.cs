using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.LV
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class LVBogenModel : PageModel
    {
        private readonly LvService _service;

        public LVBogenModel(LvService service)
        {
            _service = service;
        }

        public LvBogen? Bogen { get; private set; }
        public IReadOnlyList<LvCategory> Categories { get; private set; } = [];
        public int? BID { get; private set; }

        public async Task<IActionResult> OnGetAsync(int? bid, CancellationToken cancellationToken)
        {
            if (bid is not int bogenId || bogenId <= 0) return BadRequest("Eine gültige Bogen-ID ist erforderlich.");

            Bogen = await _service.GetBogenAsync(bogenId, cancellationToken);
            if (Bogen is null) return NotFound();

            BID = bogenId;
            Categories = await _service.GetCategoriesAsync(cancellationToken);
            return Page();
        }
    }
}
