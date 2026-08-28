using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Pages.Api
{
    public class SuchePreviewModel : PageModel
    {
        public IActionResult OnGet(string? suchbegriff)
        {
            return new JsonResult(new SuchePreviewResponse
            {
                Suggestions = string.IsNullOrWhiteSpace(suchbegriff) ? [] : [suchbegriff.Trim()]
            });
        }
    }
}
