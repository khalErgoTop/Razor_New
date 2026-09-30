using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.Kalkulation;

[Authorize(Policy = "AuthenticatedUser")]
public class QWKalkulationPrintPreviewModel : PageModel
{
    private readonly KalkulationService _service;
    private readonly KalkulationAdapter _adapter;

    public QWKalkulationPrintPreviewModel(KalkulationService service, KalkulationAdapter adapter)
    {
        _service = service;
        _adapter = adapter;
    }

    [BindProperty(SupportsGet = true)]
    public string? EIID { get; set; }

    public KalkulationPageData? Data { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await _service.HasAccessAsync(cancellationToken)) return Forbid();
        if (string.IsNullOrWhiteSpace(EIID)) return RedirectToPage("/Kalkulation/QWKalkulationsauswahl");

        Data = await _adapter.GetPageAsync(EIID, cancellationToken);
        if (Data is null)
        {
            ErrorMessage = "Die ausgewählte Kalkulation wurde nicht gefunden oder ist nicht mehr verfügbar.";
        }

        return Page();
    }

    public async Task<IActionResult> OnGetDownloadXmlAsync(CancellationToken cancellationToken)
    {
        if (!await _service.HasAccessAsync(cancellationToken)) return Forbid();
        if (string.IsNullOrWhiteSpace(EIID)) return RedirectToPage("/Kalkulation/QWKalkulationsauswahl");

        var xml = await _adapter.CreateXmlAsync(EIID, cancellationToken);
        return xml is null
            ? NotFound()
            : File(Encoding.UTF8.GetBytes(xml), "application/xml; charset=utf-8", "Kalkulation.xml");
    }
}
