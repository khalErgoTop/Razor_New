using Microsoft.AspNetCore.Http;
using System.Text.Json;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Infrastructure.Session;

public sealed class LegacySessionContext
{
    private const string UserDataKey = "User.UserData";
    private const string CurrentEiidKey = "CurrentEIID";
    private const string LogOutWantedKey = "LogOutWanted";
    private const string LastPageKey = "LastPage";
    private const string PreviouspageKey = "Previouspage";
    private const string LastPagesKey = "User.LastPages";
    private const string QmBogenCurrentObidKey = "QMBogen.CurrentOKID";
    private const string QmBogenCurrentUbidKey = "QMBogen.CurrentUKID";
    private const string QmBogenIdKey = "QMBogen.ID";
    private const string QmLayoutIdKey = "QMBogen.LayoutId";
    private const string QmBogenContentCurrentPageKey = "QMBogen.ContentCurrentPage";
    private const string QmBogenContentCurrentPageMaxKey = "QMBogen.ContentCurrentPageMAX";
    private const string QmAuswahlSourceKey = "QMAuswahl.Source";
    private const string QwKalkulationIdKey = "QWKalkulation.ID";
    private const string QwKalkulationCurrentObidKey = "QWKalkulation.CurrentOKID";
    private const string FacilitiesPageIndexKey = "FacilitiesPageIndex";
    private const string FacilitiesPageFilterKey = "FacilitiesPageFilter";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public LegacySessionContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyList<LegacyPageVisit> LastPages
    {
        get
        {
            var value = Session.GetString(LastPagesKey);
            return string.IsNullOrWhiteSpace(value)
                ? Array.Empty<LegacyPageVisit>()
                : JsonSerializer.Deserialize<List<LegacyPageVisit>>(value) ?? [];
        }
    }

    public void SetLastPage(string lastPage)
    {
        if (string.IsNullOrWhiteSpace(lastPage))
        {
            return;
        }

        var pages = LastPages.ToList();
        pages.Add(new LegacyPageVisit(lastPage, DateTimeOffset.UtcNow));
        if (pages.Count > 20)
        {
            pages.RemoveRange(0, pages.Count - 20);
        }

        Session.SetString(LastPagesKey, JsonSerializer.Serialize(pages));
        LastPage = lastPage;
        Previouspage = pages.Count > 1 ? pages[^2].Path : null;
    }

    public int FacilitiesPageIndex
    {
        get => GetInt(FacilitiesPageIndexKey) ?? 0;
        set => SetInt(FacilitiesPageIndexKey, value);
    }

public sealed record LegacyPageVisit(string Path, DateTimeOffset VisitedAt);

    public string? FacilitiesPageFilter
    {
        get => GetString(FacilitiesPageFilterKey);
        set => SetString(FacilitiesPageFilterKey, value);
    }

    public int? QMBogenID
    {
        get => GetInt(QmBogenIdKey);
        set => SetInt(QmBogenIdKey, value);
    }

    public int? QMLayoutId
    {
        get => GetInt(QmLayoutIdKey);
        set => SetInt(QmLayoutIdKey, value);
    }

    public int? QMBogenCurrentOBID
    {
        get => GetInt(QmBogenCurrentObidKey);
        set => SetInt(QmBogenCurrentObidKey, value);
    }

    public int? QMBogenCurrentUBID
    {
        get => GetInt(QmBogenCurrentUbidKey);
        set => SetInt(QmBogenCurrentUbidKey, value);
    }

    public int? QMBogenContentCurrentPage
    {
        get => GetInt(QmBogenContentCurrentPageKey);
        set => SetInt(QmBogenContentCurrentPageKey, value);
    }

    public int? QMBogenContentCurrentPageMAX
    {
        get => GetInt(QmBogenContentCurrentPageMaxKey);
        set => SetInt(QmBogenContentCurrentPageMaxKey, value);
    }

    public string? QMAuswahlSource
    {
        get => GetString(QmAuswahlSourceKey);
        set => SetString(QmAuswahlSourceKey, value);
    }

    public string? CurrentEIID
    {
        get => GetString(CurrentEiidKey);
        set => SetString(CurrentEiidKey, value);
    }

    public LegacyUserSession? UserData
    {
        get => GetLegacyUserSession(UserDataKey);
        set => SetLegacyUserSession(UserDataKey, value);
    }

    public bool LogOutWanted
    {
        get => GetBool(LogOutWantedKey);
        set => SetBool(LogOutWantedKey, value);
    }

    public string? LastPage
    {
        get => GetString(LastPageKey);
        set => SetString(LastPageKey, value);
    }

    public string? Previouspage
    {
        get => GetString(PreviouspageKey);
        set => SetString(PreviouspageKey, value);
    }

    public int? QWKalkulationID
    {
        get => GetInt(QwKalkulationIdKey);
        set => SetInt(QwKalkulationIdKey, value);
    }

    public int? QWKalkulationCurrentOBID
    {
        get => GetInt(QwKalkulationCurrentObidKey);
        set => SetInt(QwKalkulationCurrentObidKey, value);
    }

    private ISession Session => _httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("Es ist keine HTTP-Session verfügbar.");

    private string? GetString(string key) => Session.GetString(key);

    private void SetString(string key, string? value)
    {
        if (value is null) Session.Remove(key);
        else Session.SetString(key, value);
    }

    private int? GetInt(string key)
    {
        var value = Session.GetString(key);
        return int.TryParse(value, out var result) ? result : null;
    }

    private void SetInt(string key, int? value)
    {
        if (value.HasValue) Session.SetString(key, value.Value.ToString());
        else Session.Remove(key);
    }

    private LegacyUserSession? GetLegacyUserSession(string key)
    {
        var value = Session.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? null : LegacyUserSession.FromJson(value);
    }

    private void SetLegacyUserSession(string key, LegacyUserSession? value)
    {
        if (value is null) Session.Remove(key);
        else Session.SetString(key, value.ToJson());
    }

    private bool GetBool(string key)
    {
        return bool.TryParse(Session.GetString(key), out var result) && result;
    }

    private void SetBool(string key, bool value)
    {
        Session.SetString(key, value.ToString());
    }
}
