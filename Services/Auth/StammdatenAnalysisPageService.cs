using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Services.Auth;

public sealed class StammdatenAnalysisPageService
{
    private readonly StammdatenAnalysisAdapter _adapter;
    private readonly CurrentUserAccessor _currentUserAccessor;

    public StammdatenAnalysisPageService(StammdatenAnalysisAdapter adapter, CurrentUserAccessor currentUserAccessor)
    {
        _adapter = adapter;
        _currentUserAccessor = currentUserAccessor;
    }

    public IReadOnlyList<StammdatenAnalysisTypeResponse> GetAnalysisTypes() => _adapter.GetAnalysisTypes();

    public Task<StammdatenAnalysisFiltersResponse> GetFiltersAsync(CancellationToken cancellationToken)
        => _adapter.GetFiltersAsync(cancellationToken);

    public Task<IReadOnlyList<StammdatenAnalysisResponse>> GetTqwAnalysesAsync(CancellationToken cancellationToken)
    {
        if (_currentUserAccessor.BEID is not int BEID || BEID <= 0)
        {
            return Task.FromResult<IReadOnlyList<StammdatenAnalysisResponse>>([]);
        }

        return _adapter.GetTqwAnalysesAsync(BEID, cancellationToken);
    }

    public Task<IReadOnlyList<StammdatenDashboardResponse>> GetDashboardsAsync(CancellationToken cancellationToken)
    {
        if (_currentUserAccessor.BEID is not int BEID || BEID <= 0)
        {
            return Task.FromResult<IReadOnlyList<StammdatenDashboardResponse>>([]);
        }

        return _adapter.GetDashboardsAsync(BEID, cancellationToken);
    }

    public Task<StammdatenAnalysisResultResponse> GetTqwAnalysisAsync(
        int id,
        IReadOnlyDictionary<string, string?> selections,
        CancellationToken cancellationToken)
    {
        if (_currentUserAccessor.BEID is not int BEID || BEID <= 0)
        {
            throw new UnauthorizedAccessException("Der Benutzer ist nicht authentifiziert.");
        }

        return _adapter.GetTqwAnalysisAsync(id, BEID, selections, cancellationToken);
    }

    public Task<StammdatenDashboardResultResponse> GetDashboardAsync(string dashboardName, CancellationToken cancellationToken)
    {
        if (_currentUserAccessor.BEID is not int BEID || BEID <= 0)
        {
            throw new UnauthorizedAccessException("Der Benutzer ist nicht authentifiziert.");
        }

        return _adapter.GetDashboardAsync(dashboardName, BEID, cancellationToken);
    }

    public Task SaveDashboardRestrictionsAsync(
        string dashboardName,
        IReadOnlyList<StammdatenDashboardRestrictionInput> restrictions,
        CancellationToken cancellationToken)
    {
        if (_currentUserAccessor.BEID is not int BEID || BEID <= 0)
        {
            throw new UnauthorizedAccessException("Der Benutzer ist nicht authentifiziert.");
        }

        return _adapter.SaveDashboardRestrictionsAsync(dashboardName, BEID, restrictions, cancellationToken);
    }
}