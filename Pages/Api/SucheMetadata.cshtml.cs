using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Public;

namespace TOPqwWeb_Core.Pages.Api
{
    public class SucheMetadataModel : PageModel
    {
        private readonly SuchePageService _service;

        public SucheMetadataModel(SuchePageService service) => _service = service;

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            var response = await _service.SearchAsync(new PublicSearchRequest(), cancellationToken);
            return new JsonResult(new SucheMetadataResponse
            {
                FacilityTypes = response.FacilityTypes,
                Distances = response.Distances,
                FacilityFeatures = response.FacilityFeatures,
                ShowFreePlaces = true
            });
        }
    }
}
