using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Public;

namespace TOPqwWeb_Core.Pages
{
    public class SucheModel : PageModel
    {
        private readonly SuchePageService _service;

        public SucheModel(SuchePageService service)
        {
            _service = service;
        }

        [BindProperty(SupportsGet = true)]
        public string? Suchbegriff { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Einrichtungstyp { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Postleitzahl { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Entfernung { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool MitFreienPlaetzen { get; set; }

        [BindProperty(SupportsGet = true)]
        public List<int> Merkmale { get; set; } = [];

        public PublicSearchResponse? SearchResult { get; private set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            var initialResult = await _service.SearchAsync(new PublicSearchRequest(), cancellationToken);
            if (!string.IsNullOrWhiteSpace(Suchbegriff) || Einrichtungstyp > 0 || !string.IsNullOrWhiteSpace(Postleitzahl) || MitFreienPlaetzen)
            {
                SearchResult = await _service.SearchAsync(new PublicSearchRequest
                {
                    SearchValue = Suchbegriff ?? string.Empty,
                    FacilityType = Einrichtungstyp,
                    PostalCode = Postleitzahl ?? string.Empty,
                    DistanceKm = Entfernung,
                    WithFreePlaces = MitFreienPlaetzen,
                    FacilityFeatures = Merkmale
                }, cancellationToken);
            }
            else
            {
                SearchResult = initialResult;
            }
        }
    }
}
