using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace TOPqwWeb_Core.Pages.QM
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class QMArchivModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
