using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.QM
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class QMArchivModel : PageModel
    {
        private readonly QmService _service;
        private readonly LegacySessionContext _legacySessionContext;

        public QMArchivModel(QmService service, LegacySessionContext legacySessionContext)
        {
            _service = service;
            _legacySessionContext = legacySessionContext;
        }

        public IReadOnlyList<QmArchiveFacilityResponse> Facilities { get; private set; } = Array.Empty<QmArchiveFacilityResponse>();

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            _legacySessionContext.QMBogenCurrentOBID = 0;
            Facilities = await _service.GetArchiveFacilitiesAsync(cancellationToken);
        }

        public IActionResult OnPostOpen(string eiid, int qmbogenId, int qlid)
        {
            _legacySessionContext.QMAuswahlSource = "Archiv";
            _legacySessionContext.CurrentEIID = eiid;
            _legacySessionContext.QMBogenID = qmbogenId;
            _legacySessionContext.QMLayoutId = qlid;
            return RedirectToPage("/TOPqmV3/QMBogen");
        }
    }
}
