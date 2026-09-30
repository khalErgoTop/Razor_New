using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Pages.Auth;

[Authorize(Policy = "AuthenticatedUser")]
public class QWWebAdminModel : PageModel
{
    private const string AdminRights = "ea0adb75-cea8-47a9-943c-f6a34f0a943e";
    private const string PasswordPage = "ea064807-813b-49c9-b71d-a010abc3af70";
    private const string OptionsPage = "69CEA34F-ADA6-4C70-84E8-C4852F7BF1C2";
    private const string UserManagementPage = "9ecf8e04-838f-4f5e-bab8-245d0b0c2b73";
    private const string HelpTextsPage = "e7a1a0f0-08ff-4376-9a80-129a0ad5de7b";
    private readonly LegacyPermissionService _permissions;
    private readonly CurrentUserAccessor _user;

    public QWWebAdminModel(LegacyPermissionService permissions, CurrentUserAccessor user)
    {
        _permissions = permissions;
        _user = user;
    }

    public IReadOnlyList<AdminLink> Links { get; private set; } = [];
    public string? Hint { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (_user.BEID is not int beid) return Forbid();
        var rights = await _permissions.GetPermissionsAsync(beid, [AdminRights, PasswordPage, OptionsPage, UserManagementPage, HelpTextsPage], cancellationToken);
        var links = new List<AdminLink>();
        if (rights.GetValueOrDefault(PasswordPage))
        {
            Hint = "Hier können Sie Ihr Passwort ändern.";
            links.Add(new("Passwort ändern", "/PasswortAendern"));
        }
        if (rights.GetValueOrDefault(OptionsPage)) links.Add(new("Optionen bearbeiten", "/Auth/Optionen"));
        if (rights.GetValueOrDefault(UserManagementPage))
        {
            Hint = "Hier können Sie Ihre Benutzer verwalten und die Passwörter ändern.";
            links.Add(new("Benutzer-Management", "/Auth/BenutzerManagement"));
        }
        if (rights.GetValueOrDefault(HelpTextsPage)) links.Add(new("Leit- und Hilfetexte bearbeiten", "/Auth/qwWebAdminEdit"));
        if (rights.GetValueOrDefault(AdminRights))
        {
            links.Add(new("Fehlermeldungen anzeigen", "/Auth/Fehlermeldungen"));
            links.Add(new("Assistierte Suche Optionen", "/Auth/AssistOptionen"));
        }
        Links = links;
        return Page();
    }
}

public sealed record AdminLink(string Text, string Page);