using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Public;

namespace TOPqwWeb_Core.Pages
{
    public class SuchAssistentModel : PageModel
    {
        private readonly SuchAssistentPageService _service;

        public SuchAssistentModel(SuchAssistentPageService service)
        {
            _service = service;
        }

        [BindProperty(SupportsGet = true)]
        public string? Suchtyp { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Alter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Kategorie { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Leistung { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Postleitzahl { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Kreis { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal? Latitude { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal? Longitude { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool BeschreibungBestaetigt { get; set; }

        public SuchAssistentResponse? Assistant { get; private set; }
        public List<string> Postleitzahlen { get; private set; } = [];
        public List<SuchAssistentKreisResponse> Kreise { get; private set; } = [];
        public List<SuchAssistentErgebnisResponse> Ergebnisse { get; private set; } = [];
        public string? Leistungsbeschreibung { get; private set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            if (string.Equals(Suchtyp, "typ", StringComparison.OrdinalIgnoreCase) || Alter.HasValue)
            {
                Assistant = await _service.GetCategoriesAsync(Alter, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(Kategorie))
            {
                Assistant ??= new SuchAssistentResponse();
                Assistant.Leistungen = await _service.GetLeistungenAsync(Kategorie, Alter, cancellationToken);
            }

            if (Leistung.HasValue)
            {
                Leistungsbeschreibung = await _service.GetDescriptionForLeistungAsync(Leistung.Value, cancellationToken);
                Kreise = await _service.GetKreiseAsync(Leistung.Value, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(Postleitzahl) && Postleitzahl.Length >= 1)
            {
                Postleitzahlen = await _service.GetPostalCodesAsync(Postleitzahl, cancellationToken);
            }

            if (Leistung.HasValue && Kreis.HasValue)
            {
                Ergebnisse = await _service.GetResultsByKreisAsync(Kreis.Value, Leistung.Value, cancellationToken);
            }
            else if (Leistung.HasValue && IsPostalCode(Postleitzahl))
            {
                Ergebnisse = await _service.GetResultsByPostalCodeAsync(Postleitzahl!, Leistung.Value, cancellationToken);
            }
            else if (Leistung.HasValue && Latitude.HasValue && Longitude.HasValue)
            {
                Ergebnisse = await _service.GetResultsByGeolocationAsync(Latitude.Value, Longitude.Value, Leistung.Value, cancellationToken);
            }
        }

        private static bool IsPostalCode(string? value)
        {
            return value is { Length: 5 } && value.All(char.IsDigit);
        }
    }
}
