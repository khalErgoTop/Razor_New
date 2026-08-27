using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.QM
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class QMAuswahlModel : PageModel
    {
        private readonly QmService _service;
        private readonly LegacySessionContext _legacySessionContext;

        public QMAuswahlModel(QmService service, LegacySessionContext legacySessionContext)
        {
            _service = service;
            _legacySessionContext = legacySessionContext;
        }

        public IReadOnlyList<QmBogenStatus> Bogenstatus { get; private set; } = Array.Empty<QmBogenStatus>();
        public IReadOnlyList<QmFacilityResponse> Facilities { get; private set; } = Array.Empty<QmFacilityResponse>();
        public QmBogenResponse? QmBogen { get; private set; }
        public IReadOnlyList<QmSelectableBogenResponse> SelectableBoegen { get; private set; } = Array.Empty<QmSelectableBogenResponse>();

        [BindProperty(SupportsGet = true)]
        public int BogenstatusSelection { get; set; } = -1;

        [BindProperty(SupportsGet = true)]
        public string? EIID { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? QLID { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ETID { get; set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            ResetQmSelection();
            LoadBogenstatus();
            await LoadFacilitiesAsync(cancellationToken);
            await LoadSelectableBoegenAsync(cancellationToken);
            await LoadQmBogenAsync(cancellationToken);
        }

        public async Task OnPostFilterAsync(CancellationToken cancellationToken)
        {
            ResetQmSelection();
            LoadBogenstatus();
            await LoadFacilitiesAsync(cancellationToken);
            await LoadSelectableBoegenAsync(cancellationToken);
            await LoadQmBogenAsync(cancellationToken);
        }

        private async Task LoadSelectableBoegenAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(EIID) && ETID.HasValue)
            {
                SelectableBoegen = await _service.GetSelectableBoegenAsync(EIID, ETID.Value, BogenstatusSelection, cancellationToken);
            }
        }

        public async Task<IActionResult> OnPostOpenAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(EIID) || !QLID.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Bitte wählen Sie einen QM-Bogen aus.");
                ResetQmSelection();
                LoadBogenstatus();
                await LoadFacilitiesAsync(cancellationToken);
                return Page();
            }

            QmBogen = await _service.GetQmBogenAsync(EIID, QLID.Value, cancellationToken);
            if (QmBogen is null)
            {
                ModelState.AddModelError(string.Empty, "Der ausgewählte QM-Bogen wurde nicht gefunden.");
                ResetQmSelection();
                LoadBogenstatus();
                await LoadFacilitiesAsync(cancellationToken);
                return Page();
            }

            _legacySessionContext.CurrentEIID = QmBogen.EIID;
            _legacySessionContext.QMBogenID = QmBogen.ID;
            _legacySessionContext.QMLayoutId = QmBogen.QLID;
            return RedirectToPage("/TOPqmV3/QMBogen");
        }

        private async Task LoadFacilitiesAsync(CancellationToken cancellationToken)
        {
            Facilities = await _service.GetFacilitiesAvailableAsync(BogenstatusSelection, cancellationToken);
        }

        private async Task LoadQmBogenAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(EIID) && QLID.HasValue)
            {
                QmBogen = await _service.GetQmBogenAsync(EIID, QLID.Value, cancellationToken);
            }
        }

        private void LoadBogenstatus()
        {
            Bogenstatus = _service.GetBogenstatus();
            if (Bogenstatus.All(status => status.Value != BogenstatusSelection))
            {
                BogenstatusSelection = -1;
            }
        }

        private void ResetQmSelection()
        {
            _legacySessionContext.QMBogenCurrentOBID = 0;
            _legacySessionContext.QMBogenCurrentUBID = 0;
            _legacySessionContext.QMBogenContentCurrentPage = 1;
            _legacySessionContext.QMBogenContentCurrentPageMAX = null;
            _legacySessionContext.QMAuswahlSource = "Bogenauswahl";
        }
    }
}
