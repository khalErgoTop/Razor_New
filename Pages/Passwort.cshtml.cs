using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace TOPqwWeb_Core.Pages
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class PasswortModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
