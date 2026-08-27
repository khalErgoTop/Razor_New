using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Services.Auth;

public sealed class StammdatenKostenPageService
{
    private const string Anzeigen = "15d4d8ac-6229-4517-a728-1c863f4a20d4";
    private const string Editieren = "9c0f8b94-0f54-4f55-9de3-14c8e4328273";
    private const string Loeschen = "1e6a1b88-0d3a-4b1e-be22-68ea42c78c81";
    private readonly StammdatenKostenAdapter _adapter;
    private readonly LegacyPermissionService _permissions;
    private readonly CurrentUserAccessor _currentUser;

    public StammdatenKostenPageService(StammdatenKostenAdapter adapter, LegacyPermissionService permissions, CurrentUserAccessor currentUser)
    {
        _adapter = adapter;
        _permissions = permissions;
        _currentUser = currentUser;
    }

    public async Task<StammdatenKostenAccess> GetAccessAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.BEID is not int beid || beid <= 0) return new(false, false, false, false);
        var permissions = await _permissions.GetPermissionsAsync(beid, [Anzeigen, Editieren, Loeschen], cancellationToken);
        return new(permissions[Anzeigen], permissions[Editieren], permissions[Loeschen], true);
    }

    public Task<IReadOnlyList<StammdatenKostenTimeFrame>> GetTimeFramesAsync(string eiid, CancellationToken cancellationToken)
        => _adapter.GetTimeFramesAsync(eiid, cancellationToken);

    public Task<IReadOnlyList<StammdatenKostenRow>> GetRowsAsync(string eiid, int hbgwzid, CancellationToken cancellationToken)
        => _adapter.GetRowsAsync(eiid, hbgwzid, cancellationToken);

    public Task<bool> TimeFrameBelongsToFacilityAsync(string eiid, int hbgwzid, CancellationToken cancellationToken)
        => _adapter.TimeFrameBelongsToFacilityAsync(eiid, hbgwzid, cancellationToken);

    public async Task<bool> SaveTimeFrameAsync(string eiid, int hbgwzid, DateTime dateFrom, DateTime dateTo, CancellationToken cancellationToken)
    {
        if (!await _adapter.TimeFrameBelongsToFacilityAsync(eiid, hbgwzid, cancellationToken) && hbgwzid != 0) return false;
        var etid = await _adapter.GetFacilityTypeAsync(eiid, cancellationToken);
        if (!etid.HasValue || string.IsNullOrWhiteSpace(_currentUser.Username)) return false;
        await _adapter.SaveTimeFrameAsync(eiid, etid.Value, hbgwzid, dateFrom, dateTo, _currentUser.Username!, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteTimeFrameAsync(string eiid, int hbgwzid, CancellationToken cancellationToken)
    {
        if (!await _adapter.TimeFrameBelongsToFacilityAsync(eiid, hbgwzid, cancellationToken)) return false;
        return await _adapter.DeleteTimeFrameAsync(eiid, hbgwzid, cancellationToken) > 0;
    }

    public async Task<bool> SaveRowAsync(string eiid, int hbgwzid, StammdatenKostenRow row, CancellationToken cancellationToken)
    {
        if (!await _adapter.TimeFrameBelongsToFacilityAsync(eiid, hbgwzid, cancellationToken)) return false;
        if (!await _adapter.HbgBelongsToFacilityTypeAsync(eiid, row.HBGID, cancellationToken)) return false;
        return await _adapter.SaveRowAsync(eiid, hbgwzid, row, _currentUser.Username ?? string.Empty, cancellationToken);
    }
}

public sealed record StammdatenKostenAccess(bool CanView, bool CanEdit, bool CanDelete, bool IsAuthenticated);
