using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using TOPqwWeb_Core.Infrastructure.Session;

namespace TOPqwWeb_Core.Pages
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class PasswortModel : PageModel
    {
        private readonly LegacySessionContext _session;

        public PasswortModel(LegacySessionContext session)
        {
            _session = session;
        }

        public IActionResult OnGet()
        {
            if (_session.BEIDtoChange is not int userId || userId <= 0)
                return RedirectToPage("/PasswortAendern");

            return RedirectToPage("/PasswortAendern", new { UserId = userId });
        }
    }
}
