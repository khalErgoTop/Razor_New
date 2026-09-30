using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Pages.QM
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class QMBogenModel : PageModel
    {
        private readonly QmService _service;
        private readonly LegacySessionContext _legacySessionContext;

        public QMBogenModel(QmService service, LegacySessionContext legacySessionContext)
        {
            _service = service;
            _legacySessionContext = legacySessionContext;
        }

        public QmBogenResponse? QmBogen { get; private set; }
        public IReadOnlyList<QmOberkategorieResponse> Oberkategorien { get; private set; } = Array.Empty<QmOberkategorieResponse>();
        public string? ErrorMessage { get; private set; }
        public string? SaveMessage { get; private set; }
        public bool CanEdit { get; private set; }
        public Dictionary<int, string> ValidationErrors { get; } = [];

        [BindProperty] public Dictionary<int, string?> Values { get; set; } = [];

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
            }
            else
            {
                var ETID = await _service.GetFacilityTypeAsync(QmBogen.EIID, cancellationToken);
                if (ETID.HasValue)
                {
                    Oberkategorien = await _service.GetQmBogenStrukturAsync(QmBogen.QLID, ETID.Value, QmBogen.ID, cancellationToken);
                }
                CanEdit = QmBogen.Status is 1 or 2 or 5 or 6;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
        {
            await OnGetAsync(cancellationToken);
            if (QmBogen is null || !CanEdit) return Forbid();

            var questions = Oberkategorien.SelectMany(category => category.Unterkategorien).SelectMany(category => category.Fragen);
            foreach (var frage in questions)
            {
                if (!IsSimpleFormat(frage.Format)) continue;

                Values.TryGetValue(frage.ID, out var value);
                if (frage.Mussfeld && string.IsNullOrWhiteSpace(value))
                {
                    ValidationErrors[frage.ID] = "Dieses Pflichtfeld muss ausgefüllt werden.";
                }
                else if (value is not null && !IsValidValue(frage.Format, value))
                {
                    ValidationErrors[frage.ID] = "Bitte einen gültigen Wert eingeben.";
                }
            }

            if (ValidationErrors.Count > 0)
            {
                SaveMessage = "Bitte prüfen Sie die markierten Werte.";
                return Page();
            }

            foreach (var frage in questions)
            {
                if (!Values.TryGetValue(frage.ID, out var value) || !IsSimpleFormat(frage.Format)) continue;
                if (!await _service.SaveFrageWertAsync(QmBogen.ID, frage.ID, frage.Format!, value ?? string.Empty, cancellationToken))
                {
                    SaveMessage = "Mindestens ein Wert konnte nicht gespeichert werden.";
                    return Page();
                }
            }

            if (!await _service.SetBogenInBearbeitungAsync(QmBogen.ID, QmBogen.EIID, cancellationToken))
            {
                SaveMessage = "Der Status des QM-Bogens konnte nicht aktualisiert werden.";
                return Page();
            }

            SaveMessage = "gespeichert";
            return Page();
        }

        public async Task<IActionResult> OnPostSendAsync(bool confirmSend, CancellationToken cancellationToken)
        {
            await OnGetAsync(cancellationToken);
            if (QmBogen is null || !CanEdit || !confirmSend || QmBogen.Status is not (2 or 6)) return Forbid();

            var questions = Oberkategorien.SelectMany(category => category.Unterkategorien).SelectMany(category => category.Fragen);
            foreach (var frage in questions.Where(frage => frage.Mussfeld))
            {
                if (frage.FrageWert is null || string.IsNullOrWhiteSpace(frage.FrageWert.Wert))
                {
                    SaveMessage = "Bitte füllen Sie zunächst alle Pflichtfelder aus.";
                    return Page();
                }
            }

            if (!await _service.SetBogenVersandtAsync(QmBogen.ID, QmBogen.EIID, cancellationToken))
            {
                SaveMessage = "Der QM-Bogen konnte nicht versendet werden.";
                return Page();
            }

            return RedirectToPage();
        }

        public static bool IsSimpleFormat(string? format) => format is "dbText" or "dbMemo" or "dbLong" or "dbBoolean" or "dbBool" or "dbReal" or "dbSingle" or "dbDouble";

        private static bool IsValidValue(string? format, string? value) => format switch
        {
            "dbLong" => int.TryParse(value, out _),
            "dbReal" or "dbSingle" or "dbDouble" => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out _),
            "dbBoolean" or "dbBool" => bool.TryParse(value, out _),
            _ => true
        };
    }
}
