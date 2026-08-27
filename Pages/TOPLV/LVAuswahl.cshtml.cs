using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace TOPqwWeb_Core.Pages.LV
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class LVAuswahlModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
