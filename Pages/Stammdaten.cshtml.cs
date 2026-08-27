using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Public;

namespace TOPqwWeb_Core.Pages
{
    public class StammdatenModel : PageModel
    {
        private readonly StammdatenPageService _service;
        private readonly LegacySessionContext _legacySessionContext;

        public StammdatenModel(StammdatenPageService service, LegacySessionContext legacySessionContext)
        {
            _service = service;
            _legacySessionContext = legacySessionContext;
        }

        public StammdatenResponse? Facility { get; private set; }

        public async Task<IActionResult> OnGetAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid)) return RedirectToPage("/Index");
            Facility = await _service.GetAsync(eiid, stuid, cancellationToken);
            if (Facility is not null)
            {
                _legacySessionContext.CurrentEIID = Facility.EIID;
            }

            return Facility is null ? NotFound() : Page();
        }

        public async Task<IActionResult> OnGetAttachmentAsync(string? eiid, string? attachmentId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid) || string.IsNullOrWhiteSpace(attachmentId)) return NotFound();
            var attachment = await _service.GetAttachmentAsync(eiid, attachmentId, cancellationToken);
            return attachment is null ? NotFound() : File(attachment.Value.Content, "application/octet-stream", attachment.Value.FileName);
        }

        public async Task<IActionResult> OnGetImageAsync(string? eiid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid)) return NotFound();
            var image = await _service.GetImageAsync(eiid, cancellationToken);
            return image is null ? NotFound() : File(image.Value.Content, image.Value.ContentType, image.Value.FileName);
        }
    }
}
