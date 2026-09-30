using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Services.Administration;

namespace TOPqwWeb_Core.Pages.Auth
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class FehlermeldungenModel : PageModel
    {
        private const string AdminRights = "ea0adb75-cea8-47a9-943c-f6a34f0a943e";
        private readonly FehlermeldungenService _service;
        private readonly LegacyPermissionService _permissions;
        private readonly CurrentUserAccessor _currentUser;

        public FehlermeldungenModel(FehlermeldungenService service, LegacyPermissionService permissions, CurrentUserAccessor currentUser)
        {
            _service = service;
            _permissions = permissions;
            _currentUser = currentUser;
        }

        public IReadOnlyList<Fehlermeldung> Errors { get; private set; } = [];
        [BindProperty(SupportsGet = true)] public string ConstraintType { get; set; } = "1";

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            if (!await HasAccessAsync(cancellationToken)) return Forbid();
            Errors = await _service.GetErrorsAsync(ConstraintType, cancellationToken);
            return Page();
        }

        public async Task<IActionResult> OnGetErrorsAsync(CancellationToken cancellationToken)
        {
            if (!await HasAccessAsync(cancellationToken)) return Forbid();
            return new JsonResult(await _service.GetErrorsAsync(ConstraintType, cancellationToken));
        }

        private Task<bool> HasAccessAsync(CancellationToken cancellationToken)
            => _currentUser.BEID is int beid
                ? _permissions.HasPermissionAsync(beid, AdminRights, cancellationToken)
                : Task.FromResult(false);
    }
}
