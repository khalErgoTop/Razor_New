using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Services.Administration;

namespace TOPqwWeb_Core.Pages.Auth;

[Authorize(Policy = "AuthenticatedUser")]
public class SystemNewsModel : PageModel
{
    private readonly SystemNewsService _service;

    public SystemNewsModel(SystemNewsService service) => _service = service;

    public IReadOnlyList<SystemNewsItem> News { get; private set; } = [];
    public IReadOnlyList<SystemNewsPerson> SelectedPersons { get; private set; } = [];
    public IReadOnlyList<SystemNewsPerson> AvailablePersons { get; private set; } = [];
    public string? Message { get; private set; }

    [BindProperty(SupportsGet = true)] public int SelectedNewsId { get; set; }
    [BindProperty] public string InfoLabel { get; set; } = "";
    [BindProperty] public string InfoText { get; set; } = "";
    [BindProperty] public int[] PersonIds { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSelectAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (SelectedNewsId > 0 && !await _service.NewsExistsAsync(SelectedNewsId, cancellationToken)) return NotFound();
        SelectedNewsId = await _service.SaveNewsAsync(SelectedNewsId, InfoLabel, InfoText, cancellationToken);
        Message = "Die Nachricht wurde gespeichert.";
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        if (SelectedNewsId > 0)
        {
            if (!await _service.NewsExistsAsync(SelectedNewsId, cancellationToken)) return NotFound();
            await _service.DeleteNewsAsync(SelectedNewsId, cancellationToken);
        }
        SelectedNewsId = 0;
        Message = "Die Nachricht wurde gelöscht.";
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAddPersonsAsync(CancellationToken cancellationToken)
    {
        if (SelectedNewsId > 0) await _service.AddPersonsAsync(SelectedNewsId, PersonIds, cancellationToken);
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRemovePersonsAsync(CancellationToken cancellationToken)
    {
        if (SelectedNewsId > 0) await _service.RemovePersonsAsync(SelectedNewsId, PersonIds, cancellationToken);
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostNewAsync(CancellationToken cancellationToken)
    {
        SelectedNewsId = 0;
        InfoLabel = "";
        InfoText = "";
        await LoadAsync(cancellationToken);
        SelectedNewsId = 0;
        InfoLabel = "";
        InfoText = "";
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        News = await _service.GetNewsAsync(cancellationToken);
        if (SelectedNewsId > 0 && News.All(item => item.Id != SelectedNewsId)) SelectedNewsId = 0;
        if (SelectedNewsId <= 0) SelectedNewsId = News.FirstOrDefault()?.Id ?? 0;
        var selected = News.FirstOrDefault(item => item.Id == SelectedNewsId);
        InfoLabel = selected?.Label ?? "";
        InfoText = selected?.Text ?? "";
        if (SelectedNewsId <= 0) return;
        SelectedPersons = await _service.GetSelectedPersonsAsync(SelectedNewsId, cancellationToken);
        AvailablePersons = await _service.GetAvailablePersonsAsync(SelectedNewsId, cancellationToken);
    }
}
