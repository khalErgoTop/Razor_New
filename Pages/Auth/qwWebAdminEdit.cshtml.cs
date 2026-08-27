using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Services.Common;

namespace TOPqwWeb_Core.Pages.Auth;

[Authorize(Policy = "AuthenticatedUser")]
public class QwWebAdminEditModel : PageModel
{
    private const string Permission = "e7a1a0f0-08ff-4376-9a80-129a0ad5de7b";
    private readonly LegacyPermissionService _permissions;
    private readonly CurrentUserAccessor _user;
    private readonly LeittextService _service;

    public QwWebAdminEditModel(LegacyPermissionService permissions, CurrentUserAccessor user, LeittextService service)
    {
        _permissions = permissions;
        _user = user;
        _service = service;
    }

    public IReadOnlyList<LeittextPage> Pages { get; private set; } = [];
    public LeittextEntry CurrentEntry { get; private set; } = new("", false, "", "", false);
    public string? Message { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedPage { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool HelpText { get; set; } = true;

    [BindProperty]
    public string Text { get; set; } = "";

    [BindProperty]
    public string Title { get; set; } = "";

    [BindProperty]
    public bool Visible { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(cancellationToken)) return Forbid();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadAsync(CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(cancellationToken)) return Forbid();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(cancellationToken)) return Forbid();
        if (string.IsNullOrWhiteSpace(SelectedPage))
        {
            Message = "Bitte wählen Sie eine Seite aus.";
            await LoadAsync(cancellationToken);
            return Page();
        }

        await _service.SaveAsync(new LeittextEntry(SelectedPage, HelpText, Text, Title, Visible), cancellationToken);
        Message = "Die Änderungen wurden übernommen.";
        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Pages = await _service.GetPagesAsync(cancellationToken);
        SelectedPage ??= Pages.FirstOrDefault()?.Value;
        if (SelectedPage is null) return;
        CurrentEntry = await _service.GetAsync(SelectedPage, HelpText, cancellationToken);
        Text = CurrentEntry.Text;
        Title = CurrentEntry.Title;
        Visible = CurrentEntry.Visible;
    }

    private async Task<bool> HasPermissionAsync(CancellationToken cancellationToken)
    {
        return _user.BEID is int beid && await _permissions.HasPermissionAsync(beid, Permission, cancellationToken);
    }
}
