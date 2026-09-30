using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Services.Administration;

namespace TOPqwWeb_Core.Pages;

public sealed class DBCheckModel : PageModel
{
    private readonly DbCheckService _service;
    private const string DbCheckCompletedKey = "DBCheck.Completed";

    public DBCheckModel(DbCheckService service) => _service = service;

    public DbCheckResult Result { get; private set; } = new("0.1.84", string.Empty, false, null);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Result = await _service.CheckAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            Result = DbCheckService.Failure(Result.MinimumVersion, new InvalidOperationException());
        }

        HttpContext.Session.SetString(DbCheckCompletedKey, bool.TrueString);
        return Page();
    }

    public IActionResult OnPostContinue()
    {
        return RedirectToPage("/Index");
    }
}
