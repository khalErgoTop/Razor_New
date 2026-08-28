using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Services.Account;

namespace TOPqwWeb_Core.Pages
{
    public class LoginModel : PageModel
    {
        private readonly LoginPageService _service;
        private readonly ClaimsPrincipalFactory _claimsPrincipalFactory;
        private readonly LegacySessionContext _legacySessionContext;

        public LoginModel(LoginPageService service, ClaimsPrincipalFactory claimsPrincipalFactory, LegacySessionContext legacySessionContext)
        {
            _service = service;
            _claimsPrincipalFactory = claimsPrincipalFactory;
            _legacySessionContext = legacySessionContext;
        }

        [BindProperty]
        public LoginInput Input { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string? ErrorMessage { get; private set; }
        public bool ShowMailLink { get; private set; }
        public string? SupportMail { get; private set; }
        public int WaitSeconds { get; private set; }
        public string? PasswordRequestMessage { get; private set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        {
            if (Input.Passwort?.Length > 64)
            {
                ErrorMessage = "Benutzername unbekannt oder falsches Passwort!";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Input.Username) || string.IsNullOrWhiteSpace(Input.Passwort))
            {
                ErrorMessage = "Benutzername unbekannt oder falsches Passwort!";
                return Page();
            }

            var user = await _service.LoginEncAsync(Input.Username.Trim(), Input.Passwort, cancellationToken);
            if (user is not null && user.WaitSeconds > 0)
            {
                WaitSeconds = user.WaitSeconds;
                ErrorMessage = "Benutzername unbekannt oder falsches Passwort!";
                return Page();
            }

            if (user is not null && user.UserId > 0)
            {
                var LastPage = GetLocalReturnUrl(ReturnUrl);
                var sessionId = Guid.NewGuid().ToString("N");
                _legacySessionContext.UserData = new LegacyUserSession
                {
                    Id = sessionId,
                    BEID = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    IsDeactivated = user.IsDeactivated,
                    KeepLoggedIn = Input.StayLoggedIn,
                    LastPage = LastPage ?? string.Empty,
                    Modules = user.Modules
                };
                _legacySessionContext.LogOutWanted = false;
                _legacySessionContext.LastPage = LastPage;
                if (LastPage is not null)
                {
                    _legacySessionContext.SetLastPage(LastPage);
                }
                await HttpContext.SignInAsync(
                    "LegacyCookie",
                    _claimsPrincipalFactory.Create(user, LastPage, sessionId),
                    new AuthenticationProperties { IsPersistent = Input.StayLoggedIn });

                return LocalRedirect(LastPage ?? "/Index");
            }

            ErrorMessage = "Benutzername unbekannt oder falsches Passwort!";
            ShowMailLink = true;
            SupportMail = await _service.GetMailToAsync(cancellationToken);
            return Page();
        }

        private string? GetLocalReturnUrl(string? ReturnUrl)
        {
            if (string.IsNullOrWhiteSpace(ReturnUrl) || !Url.IsLocalUrl(ReturnUrl))
            {
                return null;
            }

            return ReturnUrl;
        }

        public async Task<IActionResult> OnPostPasswordRequestAsync(CancellationToken cancellationToken)
        {
            var username = Input.Username?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                PasswordRequestMessage = "Bitte geben Sie zuerst Ihren Anmeldenamen ein.";
                return Page();
            }

            var result = await _service.RequestPasswordAsync(username, cancellationToken);
            if (result.WaitSeconds > 0)
            {
                WaitSeconds = result.WaitSeconds;
                PasswordRequestMessage = $"Bitte warten Sie noch {result.WaitSeconds} Sekunden, bevor Sie eine neue Anfrage stellen.";
            }
            else if (result.Requested && !string.IsNullOrWhiteSpace(result.Email))
            {
                var url = $"{Request.Scheme}://{Request.Host}/PasswordRequest?TK={Uri.EscapeDataString(result.Hash!)}";
                await _service.SendPasswordRequestEmailAsync(username, result, url, cancellationToken);
                PasswordRequestMessage = "Prüfen Sie bitte den Posteingang Ihres E-Mail-Kontos und folgen Sie dem Bestätigungslink.";
            }
            else
            {
                PasswordRequestMessage = "Die Passwortanforderung konnte nicht automatisch verarbeitet werden. Bitte wenden Sie sich an den Support.";
                ShowMailLink = true;
                SupportMail = await _service.GetMailToAsync(cancellationToken);
            }

            return Page();
        }

        public sealed class LoginInput
        {
            [Display(Name = "Anmeldename")]
            public string? Username { get; set; }

            [Display(Name = "Passwort")]
            public string? Passwort { get; set; }

            [Display(Name = "angemeldet bleiben?")]
            public bool StayLoggedIn { get; set; }
        }
    }
}
