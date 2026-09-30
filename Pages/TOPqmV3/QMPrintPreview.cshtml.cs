using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.QM
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class QMPrintPreviewModel : PageModel
    {
        private readonly QmService _service;
        private readonly LegacySessionContext _legacySessionContext;

        public QMPrintPreviewModel(QmService service, LegacySessionContext legacySessionContext)
        {
            _service = service;
            _legacySessionContext = legacySessionContext;
        }

        public QmBogenResponse? QmBogen { get; private set; }
        public IReadOnlyList<QmOberkategorieResponse> Oberkategorien { get; private set; } = Array.Empty<QmOberkategorieResponse>();
        public string? ErrorMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            if (!_legacySessionContext.QMBogenID.HasValue ||
                string.IsNullOrWhiteSpace(_legacySessionContext.CurrentEIID) ||
                !_legacySessionContext.QMLayoutId.HasValue)
            {
                return RedirectToPage("/TOPqmV3/QMAuswahl");
            }

            QmBogen = await _service.GetQmBogenAsync(
                _legacySessionContext.CurrentEIID,
                _legacySessionContext.QMLayoutId.Value,
                cancellationToken);

            if (QmBogen is null || QmBogen.ID != _legacySessionContext.QMBogenID.Value)
            {
                ErrorMessage = "Der ausgewählte QM-Bogen wurde nicht gefunden oder ist nicht mehr verfügbar.";
                QmBogen = null;
                return Page();
            }

            var ETID = await _service.GetFacilityTypeAsync(QmBogen.EIID, cancellationToken);
            if (ETID.HasValue)
            {
                Oberkategorien = await _service.GetQmBogenStrukturAsync(QmBogen.QLID, ETID.Value, QmBogen.ID, cancellationToken);
            }

            return Page();
        }

        public async Task<IActionResult> OnGetDownloadXmlAsync(CancellationToken cancellationToken)
        {
            if (!_legacySessionContext.QMBogenID.HasValue ||
                string.IsNullOrWhiteSpace(_legacySessionContext.CurrentEIID) ||
                !_legacySessionContext.QMLayoutId.HasValue)
            {
                return RedirectToPage("/TOPqmV3/QMAuswahl");
            }

            var xml = await _service.CreateQmXmlAsync(
                _legacySessionContext.CurrentEIID,
                _legacySessionContext.QMLayoutId.Value,
                _legacySessionContext.QMBogenID.Value,
                cancellationToken);
            return xml is null
                ? NotFound()
                : File(System.Text.Encoding.UTF8.GetBytes(xml), "application/xml; charset=utf-8", "QM-Bogen.xml");
        }
    }
}
