using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Services.Layout;

public sealed class LayoutNavigationService
{
    private const int Stammdaten = 1;
    private const int Administration = 2;
    private const int Leistungsvereinbarung = 4;
    private const int Kalkulation = 8;
    private const int Qualitaetsmanagement = 16;
    private const int Anbietermanagement = 32;
    private const int Kostenblaetter = 64;
    private const int Betriebserlaubnis = 128;

    private readonly IConfiguration _configuration;
    private readonly CurrentUserAccessor _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LayoutNavigationService(
        IConfiguration configuration,
        CurrentUserAccessor currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<LayoutNavigationModel> BuildAsync(CancellationToken cancellationToken = default)
    {
        var links = new List<LayoutNavigationLink>
        {
            new("home", "Startseite", "/", "home")
        };

        if (await GetBooleanOptionAsync("SuchAssistent_anzeigen", cancellationToken))
        {
            var name = await GetTextOptionAsync("SuchAssistent_Name", cancellationToken) ?? "Assistierte Suche";
            links.Add(new("assistant", name, "/SuchAssistent", ""));
        }

        links.Add(new(
            "offers",
            "Leistungsangebote",
            "/Sucharten",
            "",
            new[]
            {
                new LayoutNavigationLink("offers-type", "nach Art der Leistung", "/Auflistung?Typ=Typ", ""),
                new LayoutNavigationLink("offers-region", "nach Region", "/Auflistung?Typ=Kreis", "")
            }));
        links.Add(new("search", "Detailsuche", "/Suche", "search"));

        var modules = GetClaimModules();
        foreach (var module in modules)
        {
            var link = module switch
            {
                Stammdaten => new LayoutNavigationLink("stammdaten", "Stammdaten", "/Auth/qwStammdaten", ""),
                Administration => new LayoutNavigationLink("administration", "Administration", "/Auth/QWWebAdmin", ""),
                Anbietermanagement => new LayoutNavigationLink("anbietermanagement", "Anbietermanagement", "/TOPAM/AMBogen", ""),
                Kalkulation => new LayoutNavigationLink("kalkulation", "Kalkulation", "/QWKalkulation/QWKalkulationsauswahl", ""),
                Leistungsvereinbarung => new LayoutNavigationLink("leistungsvereinbarung", "Leistungsvereinbarung", "/TOPLV/LVAuswahl", ""),
                Qualitaetsmanagement => new LayoutNavigationLink("topqm", "TOPqm", "/TopQMV3/QMMain", ""),
                Kostenblaetter => new LayoutNavigationLink("kostenblaetter", "Kostenblätter", "/bitQArc/bitQArcDefaultSheetListing?f=KV", ""),
                Betriebserlaubnis => new LayoutNavigationLink("betriebserlaubnis", "Betriebserlaubnisverfahren", "/bitQArc/bitQArcDefaultSheetListing?f=BE", ""),
                _ => null
            };

            if (link is not null && links.All(existing => existing.Id != link.Id))
            {
                links.Add(link);
            }
        }

        return new LayoutNavigationModel(links, _currentUser.IsAuthenticated, _currentUser.Username);
    }

    private IReadOnlyList<int> GetClaimModules()
    {
        var claims = _currentUser.BEID.HasValue
            ? _httpContextAccessor.HttpContext?.User.Claims ?? Enumerable.Empty<Claim>()
            : Enumerable.Empty<Claim>();

        return claims
            .Where(claim => claim.Type == "LegacyModule")
            .Select(claim => int.TryParse(claim.Value, out var module) ? module : 0)
            .Where(module => module > 0)
            .Distinct()
            .ToArray();
    }

    private async Task<string?> GetTextOptionAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(
                "SELECT TOP 1 WertTXT FROM TOPqw_Einstellungen WHERE Einstellung = @name AND ISNULL(fkUSID, 0) = 0",
                connection);
            command.Parameters.AddWithValue("@name", name);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null || value == DBNull.Value ? null : Convert.ToString(value);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<bool> GetBooleanOptionAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(
                "SELECT TOP 1 WertBOOL FROM TOPqw_Einstellungen WHERE Einstellung = @name AND ISNULL(fkUSID, 0) = 0",
                connection);
            command.Parameters.AddWithValue("@name", name);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is not null && value != DBNull.Value && Convert.ToBoolean(value);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private string GetConnectionString() => _configuration.GetConnectionString("TOPqm")
        ?? throw new InvalidOperationException("ConnectionStrings:TOPqm muss konfiguriert werden.");
}

public sealed record LayoutNavigationModel(
    IReadOnlyList<LayoutNavigationLink> Links,
    bool IsAuthenticated,
    string? Username);

public sealed record LayoutNavigationLink(
    string Id,
    string Name,
    string Url,
    string CssClass,
    IReadOnlyList<LayoutNavigationLink>? Children = null);
