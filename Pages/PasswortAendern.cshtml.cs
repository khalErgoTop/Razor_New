using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Account;

namespace TOPqwWeb_Core.Pages
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class PasswortAendernModel : PageModel
    {
        private readonly PasswortAendernPageService _service;
        private readonly CurrentUserAccessor _currentUser;

        public PasswortAendernModel(PasswortAendernPageService service, CurrentUserAccessor currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        public PasswordPolicy PasswordPolicy { get; private set; } = new();
        public string? Message { get; private set; }
        public bool IsSuccess { get; private set; }
        public bool IsAdministrativeChange { get; private set; }
        public string? TargetUsername { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int? UserId { get; set; }

        [BindProperty]
        public PasswordInput Input { get; set; } = new();

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            PasswordPolicy = await _service.GetPasswordPolicyAsync(cancellationToken);
            await LoadTargetAsync(cancellationToken);
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        {
            PasswordPolicy = await _service.GetPasswordPolicyAsync(cancellationToken);
            if (_currentUser.BEID is not int userId || userId <= 0)
            {
                Message = "Keine ID bekannt!";
                return Page();
            }

            if (string.IsNullOrEmpty(Input.AltesPasswort) || string.IsNullOrEmpty(Input.NeuesPasswort))
            {
                Message = "Bitte füllen Sie alle Passwortfelder aus.";
                return Page();
            }

            var result = await _service.ChangeOwnPasswordAsync(
                userId, Input.AltesPasswort, Input.NeuesPasswort,
                Input.PasswortBestaetigung ?? string.Empty, cancellationToken);
            PasswordPolicy = result.Policy;
            Message = result.Message;
            IsSuccess = result.Succeeded;
            return Page();
        }

        public async Task<IActionResult> OnPostAdministrativeAsync(CancellationToken cancellationToken)
        {
            PasswordPolicy = await _service.GetPasswordPolicyAsync(cancellationToken);
            if (_currentUser.BEID is not int currentUserId ||
                !await _service.HasUserManagementAdminRightsAsync(currentUserId, cancellationToken))
                return Forbid();
            if (UserId is not int targetUserId || targetUserId <= 0 || targetUserId == currentUserId)
            {
                Message = "Kein gültiger fremder Benutzer ausgewählt.";
                return Page();
            }

            var result = await _service.ChangeUserPasswordAsync(
                targetUserId, Input.NeuesPasswort ?? string.Empty,
                Input.PasswortBestaetigung ?? string.Empty, cancellationToken);
            PasswordPolicy = result.Policy;
            Message = result.Message;
            IsSuccess = result.Succeeded;
            await LoadTargetAsync(cancellationToken);
            return Page();
        }

        private async Task LoadTargetAsync(CancellationToken cancellationToken)
        {
            if (UserId is not int targetUserId || targetUserId <= 0 ||
                _currentUser.BEID is not int currentUserId || targetUserId == currentUserId)
                return;

            if (!await _service.HasUserManagementAdminRightsAsync(currentUserId, cancellationToken))
                return;

            IsAdministrativeChange = true;
            TargetUsername = await _service.GetUsernameAsync(targetUserId, cancellationToken);
        }

        public IActionResult OnPostCancel() => RedirectToPage("/Auth/QWWebAdmin");

        public sealed class PasswordInput
        {
            [DataType(DataType.Password)]
            public string? AltesPasswort { get; set; }

            [DataType(DataType.Password)]
            public string? NeuesPasswort { get; set; }

            [DataType(DataType.Password)]
            public string? PasswortBestaetigung { get; set; }
        }
    }
}
