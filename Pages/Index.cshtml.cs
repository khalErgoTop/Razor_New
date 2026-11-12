using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Services.Public;

namespace TOPqwWeb_Core.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IndexPageService _indexPageService;

        public IndexModel(IndexPageService indexPageService)
        {
            _indexPageService = indexPageService;
        }

        public IHtmlContent LandingContent { get; private set; } = HtmlString.Empty;

        public async Task OnGetAsync()
        {
            LandingContent = new HtmlString(await _indexPageService.GetLandingContentAsync(Request.PathBase));
        }
    }
}
