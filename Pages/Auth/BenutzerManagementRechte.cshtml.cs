using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace TOPqwWeb_Core.Pages.Auth
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class BenutzerManagementRechteModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
