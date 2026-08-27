using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;

namespace TOPqwWeb_Core.Pages.QM
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class QMMainModel : PageModel
    {
        public IReadOnlyList<QMMainLink> Links { get; private set; } = Array.Empty<QMMainLink>();

        public void OnGet()
        {
            Links = new[]
            {
                new QMMainLink("aktuelle Berichte", "/TOPqmV3/QMAuswahl"),
                new QMMainLink("alte Berichte", "/TOPqmV3/QMArchiv")
            };
        }

        public sealed record QMMainLink(string Caption, string NavigateUrl);
    }
}
