using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly LegacySignOutService _legacySignOutService;

        public LogoutModel(LegacySignOutService legacySignOutService)
        {
            _legacySignOutService = legacySignOutService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await _legacySignOutService.LogoutAsync();
            return RedirectToPage("/Index");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await _legacySignOutService.LogoutAsync();
            return RedirectToPage("/Index");
        }
    }
}
