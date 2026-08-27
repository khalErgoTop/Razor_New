using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Services.Administration;

public sealed class OptionenService
{
    private const string AdminRights = "ea0adb75-cea8-47a9-943c-f6a34f0a943e";
    private const string PageRights = "69CEA34F-ADA6-4C70-84E8-C4852F7BF1C2";
    private readonly string _connectionString;
    private readonly LegacyPermissionService _permissionService;
    private readonly CurrentUserAccessor _currentUser;

    public OptionenService(IConfiguration configuration, LegacyPermissionService permissionService, CurrentUserAccessor currentUser)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    public async Task<bool> CanAccessAsync(CancellationToken cancellationToken)
        => _currentUser.BEID is int beid &&
           (await _permissionService.HasPermissionAsync(beid, AdminRights, cancellationToken) ||
            await _permissionService.HasPermissionAsync(beid, PageRights, cancellationToken));

    public async Task<IReadOnlyList<OptionCategory>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Kategorie AS Category,
                   CASE WHEN ISNULL(MAX(FKUSID),0) > 0 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS ContainsUserSettings
            FROM TOPqw_Einstellungen
            WHERE ISNULL(Kategorie,'') <> ''
            GROUP BY Kategorie
            UNION ALL SELECT N'Unsortiert', CAST(0 AS bit)
            ORDER BY Category;
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var categories = new List<OptionCategory>();
        while (await reader.ReadAsync(cancellationToken))
            categories.Add(new OptionCategory(reader.GetString(0), reader.GetBoolean(1)));
        return categories;
    }

    public async Task<OptionPage> GetOptionsAsync(OptionQuery query, CancellationToken cancellationToken)
    {
        var where = new List<string> { "ISNULL(Aenderbar,1) <> 0" };
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand { Connection = connection };
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            where.Add(query.Category == "Unsortiert" ? "Kategorie IS NULL" : "Kategorie = @category");
            if (query.Category != "Unsortiert") command.Parameters.AddWithValue("@category", query.Category);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("(ISNULL(Bezeichnung,Einstellung) LIKE @search OR Einstellung LIKE @search OR Beschreibung LIKE @search OR WertTXT LIKE @search)");
            command.Parameters.AddWithValue("@search", $"%{query.Search}%");
        }
        var allowedSort = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bezeichnung"] = "ISNULL(Bezeichnung,Einstellung)", ["Einstellung"] = "Einstellung", ["Beschreibung"] = "Beschreibung"
        };
        var sort = allowedSort.GetValueOrDefault(query.SortField, "Einstellung");
        var direction = query.SortDescending ? "DESC" : "ASC";
        var predicate = string.Join(" AND ", where);
        command.CommandText = $"""
            SELECT ID, FKUSID, ISNULL(Bezeichnung,Einstellung) AS Bezeichnung, Einstellung, WertTXT, WertBOOL, WertLONG, WertSNG, WertMEMO, ISNULL(Aenderbar,1) AS Aenderbar, EinstellungDatentyp, Beschreibung
            FROM TOPqw_Einstellungen WHERE {predicate}
            ORDER BY {sort} {direction}
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            SELECT COUNT(1) FROM TOPqw_Einstellungen WHERE {predicate};
            """;
        command.Parameters.AddWithValue("@offset", Math.Max(0, query.PageIndex - 1) * query.PageSize);
        command.Parameters.AddWithValue("@pageSize", Math.Clamp(query.PageSize, 1, 100));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var options = new List<OptionRow>();
        while (await reader.ReadAsync(cancellationToken)) options.Add(ReadOption(reader));
        await reader.NextResultAsync(cancellationToken);
        var count = await reader.ReadAsync(cancellationToken) ? reader.GetInt32(0) : 0;
        return new OptionPage(options, count);
    }

    public async Task<bool> SaveAsync(string setting, string dataType, string changedValue, CancellationToken cancellationToken)
    {
        const string sql = "EXEC dbo.SaveByTokenList @SemikolonSeperatedTokenList=@tokens, @tableName=N'TOPqw_Einstellungen', @idName=N'Einstellung', @id=@id;";
        var token = dataType.Equals("WertBOOL", StringComparison.OrdinalIgnoreCase)
            ? $"WertBOOL={(changedValue.Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0)}"
            : $"{dataType}={changedValue}";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tokens", token);
        command.Parameters.AddWithValue("@id", setting);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) >= 0;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static OptionRow ReadOption(SqlDataReader reader) => new(
        reader.GetString(3), reader.GetString(2), reader.IsDBNull(4) ? null : reader.GetString(4),
        !reader.IsDBNull(5) && reader.GetBoolean(5), reader.IsDBNull(6) ? null : reader.GetValue(6).ToString(),
        reader.IsDBNull(7) ? null : reader.GetValue(7).ToString(), reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11));
}

public sealed record OptionCategory(string Category, bool ContainsUserSettings);
public sealed record OptionQuery(string? Category, string? Search, int PageIndex = 1, int PageSize = 20, string SortField = "Einstellung", bool SortDescending = false);
public sealed record OptionPage(IReadOnlyList<OptionRow> Data, int ItemsCount);
public sealed record OptionRow(string Einstellung, string Bezeichnung, string? WertTXT, bool WertBOOL, string? WertLONG, string? WertSNG, string? WertMEMO, string? EinstellungDatentyp, string? Beschreibung);
