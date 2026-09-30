using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.LV;

[Authorize(Policy = "AuthenticatedUser")]
public class LVBogenPrintPreviewModel : PageModel
{
    private readonly LvService _service;

    public LVBogenPrintPreviewModel(LvService service)
    {
        _service = service;
    }

    public LvBogen? Bogen { get; private set; }
    public IReadOnlyList<LvCategory> Categories { get; private set; } = [];
    public IReadOnlyList<LvSubcategory> Structure { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? bid, CancellationToken cancellationToken)
    {
        if (bid is not int bogenId || bogenId <= 0) return BadRequest("Eine gültige Bogen-ID ist erforderlich.");

        Bogen = await _service.GetBogenAsync(bogenId, cancellationToken);
        if (Bogen is null) return NotFound();

        Categories = await _service.GetCategoriesAsync(cancellationToken);
        Structure = await _service.GetStructureAsync(Bogen.EIID, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnGetDownloadXmlAsync(int? bid, CancellationToken cancellationToken)
    {
        if (bid is not int bogenId || bogenId <= 0) return BadRequest("Eine gültige Bogen-ID ist erforderlich.");

        var xml = await _service.CreateXmlAsync(bogenId, cancellationToken);
        return xml is null
            ? NotFound()
            : File(System.Text.Encoding.UTF8.GetBytes(xml), "application/xml; charset=utf-8", $"LV-{bogenId}.xml");
    }
}
