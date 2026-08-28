using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Services.Administration;

namespace TOPqwWeb_Core.Pages.Auth;

[Authorize(Policy = "AuthenticatedUser")]
public class BenutzerManagementRechteModel : PageModel
{
    private readonly BenutzerManagementRechteService _service;

    public BenutzerManagementRechteModel(BenutzerManagementRechteService service) => _service = service;

    public IReadOnlyList<UserGroupItem> AvailableGroups { get; private set; } = [];
    public IReadOnlyList<UserGroupItem> SelectedGroups { get; private set; } = [];
    public IReadOnlyList<UserRightItem> Rights { get; private set; } = [];
    public string? Message { get; private set; }

    [BindProperty(SupportsGet = true)] public int UserId { get; set; }
    [BindProperty] public int GroupId { get; set; }
    [BindProperty] public Guid RightId { get; set; }
    [BindProperty] public bool Allowed { get; set; }
    [BindProperty] public bool Denied { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await _service.HasAdminRightsAsync(cancellationToken)) return Forbid();
        if (UserId <= 0) return BadRequest();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAddGroupAsync(CancellationToken cancellationToken)
    {
        if (!await _service.HasAdminRightsAsync(cancellationToken)) return Forbid();
        if (UserId <= 0 || GroupId <= 0 || !await _service.AddUserGroupAsync(UserId, GroupId, cancellationToken)) return BadRequest();
        Message = "Die Gruppe wurde zugeordnet.";
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveGroupAsync(CancellationToken cancellationToken)
    {
        if (!await _service.HasAdminRightsAsync(cancellationToken)) return Forbid();
        if (UserId <= 0 || GroupId <= 0 || !await _service.RemoveUserGroupAsync(UserId, GroupId, cancellationToken)) return BadRequest();
        Message = "Die Gruppe wurde entfernt.";
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveRightAsync(CancellationToken cancellationToken)
    {
        if (!await _service.HasAdminRightsAsync(cancellationToken)) return Forbid();
        if (UserId <= 0 || RightId == Guid.Empty || !await _service.SaveUserRightAsync(UserId, RightId, Allowed, Denied, cancellationToken)) return BadRequest();
        Message = "Das Recht wurde gespeichert.";
        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        AvailableGroups = await _service.GetAvailableGroupsAsync(cancellationToken);
        SelectedGroups = await _service.GetGroupsByUserAsync(UserId, cancellationToken);
        Rights = await _service.GetRightsByUserAsync(UserId, cancellationToken);
    }
}
