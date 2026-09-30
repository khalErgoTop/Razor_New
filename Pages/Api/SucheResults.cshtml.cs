using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Public;

namespace TOPqwWeb_Core.Pages.Api
{
    public class SucheResultsModel : PageModel
    {
        private readonly SuchePageService _service;
        private readonly SearchStateStore _searchState;

        public SucheResultsModel(SuchePageService service, SearchStateStore searchState)
        {
            _service = service;
            _searchState = searchState;
        }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            var response = _searchState.Results
                ?? await _service.SearchAsync(_searchState.Request ?? new PublicSearchRequest(), cancellationToken);
            return new JsonResult(new SucheResultsResponse
            {
                Results = response.Results,
                HasMoreResults = response.HasMoreResults
            });
        }
    }
}
