using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Services.Administration;

namespace TOPqwWeb_Core.Pages.Auth;

[Authorize(Policy = "AuthenticatedUser")]
public class AssistOptionenModel : PageModel
{
    private const string AdminRights = "ea0adb75-cea8-47a9-943c-f6a34f0a943e";
    private readonly AssistOptionenService _service;
    private readonly LegacyPermissionService _permissions;
    private readonly CurrentUserAccessor _currentUser;

    public AssistOptionenModel(
        AssistOptionenService service,
        LegacyPermissionService permissions,
        CurrentUserAccessor currentUser)
    {
        _service = service;
        _permissions = permissions;
        _currentUser = currentUser;
    }

    public AssistOptionenSettings Settings { get; private set; } = new(false, "Assistierte Suche");
    public IReadOnlyList<AssistServiceItem> Services { get; private set; } = [];
    public AssistServiceDetails? SelectedService { get; private set; }
    [BindProperty]
    public int? SelectedId { get; set; }
    public bool SettingsSaved { get; private set; }
    public bool ServiceSaved { get; private set; }

    [BindProperty]
    public bool AssistVisible { get; set; }

    [BindProperty]
    public string AssistName { get; set; } = string.Empty;

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string Category { get; set; } = string.Empty;

    [BindProperty]
    public string Description { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int? selectedId, bool settingsSaved = false, bool serviceSaved = false, CancellationToken cancellationToken = default)
    {
        if (!await HasAccessAsync(cancellationToken)) return Forbid();
        SelectedId = selectedId;
        SettingsSaved = settingsSaved;
        ServiceSaved = serviceSaved;
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAssistSettingsAsync(CancellationToken cancellationToken)
    {
        if (!await HasAccessAsync(cancellationToken)) return Forbid();
        await _service.SaveSettingsAsync(AssistVisible, AssistName, cancellationToken);
        return RedirectToPage(new { selectedId = SelectedId, settingsSaved = true });
    }

    public async Task<IActionResult> OnPostApplyAsync(CancellationToken cancellationToken)
    {
        if (!await HasAccessAsync(cancellationToken)) return Forbid();
        if (SelectedId is null || SelectedId <= 0) return RedirectToPage();
        await _service.UpdateServiceAsync(SelectedId.Value, Name, Category, Description, cancellationToken);
        return RedirectToPage(new { selectedId = SelectedId, serviceSaved = true });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Settings = await _service.GetSettingsAsync(cancellationToken);
        AssistVisible = Settings.Visible;
        AssistName = Settings.Name;
        Services = await _service.GetServicesAsync(cancellationToken);
        if (SelectedId is int selectedId)
        {
            SelectedService = await _service.GetServiceAsync(selectedId, cancellationToken);
            if (SelectedService is not null)
            {
                Name = SelectedService.Name;
                Category = SelectedService.Category;
                Description = SelectedService.Description;
            }
        }
    }

    private Task<bool> HasAccessAsync(CancellationToken cancellationToken)
        => _currentUser.BEID is int beid
            ? _permissions.HasPermissionAsync(beid, AdminRights, cancellationToken)
            : Task.FromResult(false);
}
