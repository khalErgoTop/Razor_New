using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Account;

namespace TOPqwWeb_Core.Pages
{
    public class PasswordRequestModel : PageModel
    {
        private readonly PasswordRequestPageService _service;

        public PasswordRequestModel(PasswordRequestPageService service)
        {
            _service = service;
        }

        [BindProperty]
        public PasswordInput Input { get; set; } = new();

        public bool IsValidToken { get; private set; }
        public string? ErrorMessage { get; private set; }
        public PasswordPolicy PasswordPolicy { get; private set; } = new();

        public async Task OnGetAsync(string? TK, CancellationToken cancellationToken)
        {
            PasswordPolicy = await _service.GetPasswordPolicyAsync(cancellationToken);
            IsValidToken = await GetUserIdAsync(TK, cancellationToken) > 0;
            if (!IsValidToken)
            {
                ErrorMessage = "Ihr Token ist ungültig oder abgelaufen.";
            }
        }

        public async Task<IActionResult> OnPostAsync(string? TK, CancellationToken cancellationToken)
        {
            PasswordPolicy = await _service.GetPasswordPolicyAsync(cancellationToken);
            var uid = await GetUserIdAsync(TK, cancellationToken);
            IsValidToken = uid > 0;
            if (!IsValidToken)
            {
                ErrorMessage = "Ihr Token ist ungültig oder abgelaufen.";
                return Page();
            }

            if (Input.Passwort != Input.PasswortBestaetigung || string.IsNullOrWhiteSpace(Input.Passwort) || !PasswordPolicy.IsValid(Input.Passwort))
            {
                ErrorMessage = "Die Passwörter stimmen nicht überein oder entsprechen nicht der Passwortvorgabe.";
                return Page();
            }

            await _service.ChangePasswordAsync(uid, Input.Passwort, cancellationToken);
            return RedirectToPage("/Login");
        }

        private async Task<int> GetUserIdAsync(string? TK, CancellationToken cancellationToken)
        {
            return string.IsNullOrWhiteSpace(TK) || TK.Length != 32 || TK.Contains(';') || TK.Contains(',') || TK.Contains('\'')
                ? 0
                : await _service.CheckPasswordRequestHashAsync(TK, cancellationToken);
        }

        public sealed class PasswordInput
        {
            [DataType(DataType.Password)]
            public string? Passwort { get; set; }

            [DataType(DataType.Password)]
            public string? PasswortBestaetigung { get; set; }
        }
    }
}
