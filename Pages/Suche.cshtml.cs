using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Services.Public;

namespace TOPqwWeb_Core.Pages
{
    public class SucheModel : PageModel
    {
        private readonly SuchePageService _service;
        private readonly SearchStateStore _searchState;

        public SucheModel(SuchePageService service, SearchStateStore searchState)
        {
            _service = service;
            _searchState = searchState;
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
        public bool Einrichtungen { get; set; } = true;

        [BindProperty(SupportsGet = true)]
        public bool Leistungserbringer { get; set; } = true;

        [BindProperty(SupportsGet = true)]
        public List<int> Merkmale { get; set; } = [];

        public PublicSearchResponse? SearchResult { get; private set; }
        public bool SearchWasExecuted { get; private set; }

        public IActionResult OnGetReset()
        {
            _searchState.Request = null;
            _searchState.Results = null;
            return RedirectToPage("/Suche");
        }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            var request = _searchState.Request;
            var hasQuery = Request.Query.ContainsKey("search") || !string.IsNullOrWhiteSpace(Suchbegriff) || Einrichtungstyp > 0 || !string.IsNullOrWhiteSpace(Postleitzahl) || MitFreienPlaetzen || Merkmale.Count > 0;
            if (hasQuery)
            {
                request = new PublicSearchRequest
                {
                    SearchValue = Suchbegriff ?? string.Empty,
                    FacilityType = Einrichtungstyp,
                    PostalCode = Postleitzahl ?? string.Empty,
                    DistanceKm = Entfernung,
                    WithFreePlaces = MitFreienPlaetzen,
                    FacilityFeatures = Merkmale,
                    Search4Facilities = Einrichtungen,
                    Search4Institutions = Leistungserbringer
                };
                SearchResult = await _service.SearchAsync(request, cancellationToken);
                SearchWasExecuted = true;
                _searchState.Request = request;
                _searchState.Results = SearchResult;
            }
            else if (request is not null)
            {
                Suchbegriff = request.SearchValue;
                Einrichtungstyp = request.FacilityType;
                Postleitzahl = request.PostalCode;
                Entfernung = request.DistanceKm;
                MitFreienPlaetzen = request.WithFreePlaces;
                Merkmale = request.FacilityFeatures;
                Einrichtungen = request.Search4Facilities;
                Leistungserbringer = request.Search4Institutions;
                SearchResult = _searchState.Results ?? await _service.SearchAsync(request, cancellationToken);
                SearchWasExecuted = true;
            }
            else
            {
                SearchResult = await _service.SearchAsync(new PublicSearchRequest(), cancellationToken);
            }
        }
    }
}
