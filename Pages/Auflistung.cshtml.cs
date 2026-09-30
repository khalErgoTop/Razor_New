using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Public;

namespace TOPqwWeb_Core.Pages
{
    public class AuflistungModel : PageModel
    {
        private readonly AuflistungPageService _auflistungPageService;

        public AuflistungModel(AuflistungPageService auflistungPageService)
        {
            _auflistungPageService = auflistungPageService;
        }

        public AuflistungResponse Listing { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync(string? typ, string? typId, string? kreisId, CancellationToken cancellationToken)
        {
            if (!string.Equals(typ, "Typ", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(typ, "Kreis", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Index");
            }

            Listing = await _auflistungPageService.GetAsync(typ, typId, kreisId, cancellationToken);
            return Page();
        }
    }
}
