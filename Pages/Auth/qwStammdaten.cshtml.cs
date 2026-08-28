using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Auth;

namespace TOPqwWeb_Core.Pages.Auth
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class qwStammdatenModel : PageModel
    {
        private readonly QwStammdatenPageService _service;
        private readonly LegacySessionContext _legacySessionContext;
        private readonly ILogger<qwStammdatenModel> _logger;

        public qwStammdatenModel(
            QwStammdatenPageService service,
            LegacySessionContext legacySessionContext,
            ILogger<qwStammdatenModel> logger)
        {
            _service = service;
            _legacySessionContext = legacySessionContext;
            _logger = logger;
        }

        public QwStammdatenResponse? Facility { get; private set; }
        public IReadOnlyList<StammdatenFacilityChoice> Facilities { get; private set; } = [];
        [BindProperty]
        public ContactDataInput ContactData { get; set; } = new();
        [BindProperty]
        public IFormFile? FacilityImage { get; set; }
        [BindProperty]
        public IFormFile? AttachmentFile { get; set; }
        [BindProperty]
        public IFormFile? DocumentFile { get; set; }
        [BindProperty]
        public int DocumentType { get; set; } = 2;
        [BindProperty]
        public StammdatenLocationInput NewLocation { get; set; } = new();
        [BindProperty]
        public StammdatenLocationInput EditedLocation { get; set; } = new();
        [BindProperty]
        public string FacilityDescription { get; set; } = string.Empty;
        [BindProperty]
        public List<FreePlaceQuestionInput> FreePlaceQuestions { get; set; } = [];

        public sealed class ContactDataInput
        {
            public string Phone { get; set; } = string.Empty;
            public string Fax { get; set; } = string.Empty;
            public string Internet { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Mobile { get; set; } = string.Empty;
        }
        public async Task<IActionResult> OnGetAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            _logger.LogInformation("qwStammdaten request: Authenticated={Authenticated}, Name={Name}, EIID={EIID}, STUID={STUID}", User.Identity?.IsAuthenticated, User.Identity?.Name, eiid, stuid);
            var selectedEIID = string.IsNullOrWhiteSpace(eiid)
                ? _legacySessionContext.CurrentEIID
                : eiid;
            Facility = string.IsNullOrWhiteSpace(selectedEIID)
                ? await _service.GetFirstForUserAsync(cancellationToken)
                : await _service.GetAsync(selectedEIID, stuid, cancellationToken);
            FacilityDescription = Facility?.Facility.Description ?? string.Empty;
            Facilities = await _service.GetFacilitiesForUserAsync(cancellationToken);
            var mainLocation = Facility?.Facility.Locations.FirstOrDefault(location => location.IsMainLocation)
                ?? Facility?.Facility.Locations.FirstOrDefault();
            if (mainLocation is not null)
            {
                ContactData = new ContactDataInput
                {
                    Phone = mainLocation.Phone ?? string.Empty,
                    Fax = mainLocation.Fax ?? string.Empty,
                    Internet = mainLocation.Internet ?? string.Empty,
                    Email = mainLocation.Email ?? string.Empty,
                    Mobile = mainLocation.Mobile ?? string.Empty
                };
            }
            if (Facility is null)
            {
                _logger.LogWarning("qwStammdaten PageModel found no accessible facility. EIID={EIID}, STUID={STUID}", eiid, stuid);
                return Forbid();
            }

            _legacySessionContext.CurrentEIID = Facility.Facility.EIID;

            return Page();
        }

        public async Task<IActionResult> OnGetAttachmentAsync(string? eiid, string? attachmentId, bool document, int attachmentType = 102, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(eiid) || string.IsNullOrWhiteSpace(attachmentId)) return NotFound();
            var attachment = await _service.GetAttachmentAsync(eiid, attachmentId, document, attachmentType, cancellationToken);
            return attachment is null ? NotFound() : File(attachment.Value.Content, "application/octet-stream", attachment.Value.FileName);
        }

        public async Task<IActionResult> OnPostSaveContactDataAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid)) return RedirectToPage("/Index");

            var saved = await _service.SaveMainLocationContactDataAsync(
                eiid,
                stuid,
                ContactData.Phone,
                ContactData.Fax,
                ContactData.Internet,
                ContactData.Email,
                ContactData.Mobile,
                cancellationToken);
            if (!saved) return Forbid();

            return RedirectToPage(new { eiid, stuid });
        }

        public async Task<IActionResult> OnPostSaveFreePlacesAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid)) return BadRequest();
            if (!await _service.SaveFreePlacesAsync(eiid, FreePlaceQuestions, cancellationToken)) return Forbid();
            return RedirectToPage(new { eiid, stuid });
        }

        public async Task<IActionResult> OnPostSaveFacilityDescriptionAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid)) return BadRequest();
            if (!await _service.SaveFacilityDescriptionAsync(eiid, FacilityDescription, cancellationToken)) return Forbid();
            return RedirectToPage(new { eiid, stuid });
        }

        public async Task<IActionResult> OnPostSaveAttachmentAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid) || AttachmentFile is null) return BadRequest();
            if (!await _service.SaveAttachmentAsync(eiid, AttachmentFile, false, 102, cancellationToken)) return Forbid();
            return RedirectToPage(new { eiid, stuid });
        }

        public async Task<IActionResult> OnPostSaveDocumentAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid) || DocumentFile is null) return BadRequest();
            if (!await _service.SaveAttachmentAsync(eiid, DocumentFile, true, DocumentType, cancellationToken)) return Forbid();
            return RedirectToPage(new { eiid, stuid });
        }

        public async Task<IActionResult> OnPostDeleteAttachmentAsync(string? eiid, string? stuid, string? attachmentId, bool document, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid) || string.IsNullOrWhiteSpace(attachmentId)) return BadRequest();
            if (!await _service.DeleteAttachmentAsync(eiid, attachmentId, document, cancellationToken)) return Forbid();
            return RedirectToPage(new { eiid, stuid });
        }

        public async Task<IActionResult> OnPostAddLocationAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid)) return BadRequest();
            if (!await _service.AddLocationAsync(eiid, NewLocation, cancellationToken)) return Forbid();
            return RedirectToPage(new { eiid, stuid });
        }

        public async Task<IActionResult> OnPostSaveLocationAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid) || string.IsNullOrWhiteSpace(stuid)) return BadRequest();
            if (!await _service.SaveLocationAsync(eiid, stuid, EditedLocation, cancellationToken)) return Forbid();
            return RedirectToPage(new { eiid, stuid });
        }

        public async Task<IActionResult> OnPostToggleFacilityVisibilityAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid)) return RedirectToPage("/Index");
            if (!await _service.ToggleFacilityVisibilityAsync(eiid, cancellationToken)) return Forbid();
            return RedirectToPage(new { eiid, stuid });
        }

        public async Task<IActionResult> OnPostToggleLocationVisibilityAsync(string? eiid, string? stuid, int? stid, bool currentlyVisible, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid) || !stid.HasValue) return BadRequest();
            if (!await _service.ToggleLocationVisibilityAsync(eiid, stid.Value, currentlyVisible, cancellationToken)) return Forbid();
            return RedirectToPage(new { eiid, stuid });
        }

        public async Task<IActionResult> OnPostSaveFacilityImageAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid) || FacilityImage is null) return BadRequest();
            if (!await _service.SaveFacilityImageAsync(eiid, FacilityImage, cancellationToken)) return Forbid();
            return RedirectToPage(new { eiid, stuid });
        }

        public async Task<IActionResult> OnPostDeleteFacilityImageAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid)) return BadRequest();
            if (!await _service.DeleteFacilityImageAsync(eiid, cancellationToken)) return Forbid();
            return RedirectToPage(new { eiid, stuid });
        }
    }
}
