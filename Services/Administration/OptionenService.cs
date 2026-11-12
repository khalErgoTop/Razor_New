using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Data;
using System.Text.Json;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Services.Administration;

public sealed class OptionenService
{
    private const string MapsSearchSetting = "StammdatenAnzeige.MapsSucheMit";
    private const string AdminRights = "ea0adb75-cea8-47a9-943c-f6a34f0a943e";
    private readonly string _connectionString;
    private readonly LegacyPermissionService _permissionService;
    private readonly CurrentUserAccessor _currentUser;
    private readonly ILogger<OptionenService> _logger;

    public OptionenService(IConfiguration configuration, LegacyPermissionService permissionService, CurrentUserAccessor currentUser, ILogger<OptionenService> logger)
    {
        _connectionString = configuration.GetConnectionString("TOPqm")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqm muss konfiguriert werden.");
        _permissionService = permissionService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<bool> CanAccessAsync(CancellationToken cancellationToken)
        => _currentUser.BEID is int beid &&
           await _permissionService.HasPermissionAsync(beid, AdminRights, cancellationToken);

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
        var where = new List<string> { "ISNULL(Aenderbar,1) <> 0", "ISNULL(FKUSID, 0) = 0" };
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

    public async Task<string> GetMapsSearchServiceAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT TOP 1 WertTXT FROM TOPqw_Einstellungen WHERE Einstellung = @setting";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@setting", MapsSearchSetting);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value ? "bing" : Convert.ToString(value) ?? "bing";
    }

    public async Task<IReadOnlyList<DisplayOption>> GetDisplayOptionsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT Einstellung, ISNULL(Bezeichnung, Einstellung), ISNULL(Beschreibung, ''), ISNULL(WertBOOL, 0), ISNULL(Aenderbar, 1) FROM TOPqw_Einstellungen WHERE Einstellung LIKE 'StammdatenAnzeige.Einrichtung.%' ORDER BY WertLONG, Einstellung";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var options = new List<DisplayOption>();
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new DisplayOption(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetBoolean(4)));
        }
        return options;
    }

    public Task<bool> SaveMapsSearchServiceAsync(string value, CancellationToken cancellationToken) =>
        SaveAsync(MapsSearchSetting, "WertTXT", value, cancellationToken);

    public Task<bool> SaveDisplayOptionAsync(string setting, bool value, CancellationToken cancellationToken) =>
        SaveAsync(setting, "WertBOOL", value ? "true" : "false", cancellationToken);

    public async Task<bool> SaveAsync(string setting, string dataType, string changedValue, CancellationToken cancellationToken)
    {
        var column = dataType.ToUpperInvariant() switch
        {
            "WERTBOOL" => "WertBOOL",
            "WERTLONG" => "WertLONG",
            "WERTINT" => "WertLONG",
            "WERTSNG" => "WertSNG",
            "WERTTXT" => "WertTXT",
            "WERTMEMO" => "WertMEMO",
            _ => throw new ArgumentException("Der Datentyp wird nicht unterstützt.", nameof(dataType))
        };

        const string sql = """
            UPDATE TOPqw_Einstellungen
            SET WertBOOL = CASE WHEN @column = 'WertBOOL' THEN @boolValue ELSE WertBOOL END,
                WertLONG = CASE WHEN @column = 'WertLONG' THEN @longValue ELSE WertLONG END,
                WertSNG = CASE WHEN @column = 'WertSNG' THEN @singleValue ELSE WertSNG END,
                WertTXT = CASE WHEN @column = 'WertTXT' THEN @textValue ELSE WertTXT END,
                WertMEMO = CASE WHEN @column = 'WertMEMO' THEN @textValue ELSE WertMEMO END
            WHERE Einstellung = @setting AND ISNULL(fkUSID, 0) = 0 AND ISNULL(Aenderbar, 1) <> 0;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        _logger.LogInformation("Option save started: setting={Setting}, dataType={DataType}, column={Column}, valueLength={ValueLength}", setting, dataType, column, changedValue?.Length ?? 0);
        command.Parameters.AddWithValue("@column", column);
        command.Parameters.AddWithValue("@setting", setting);
        command.Parameters.AddWithValue("@boolValue", changedValue.Equals("true", StringComparison.OrdinalIgnoreCase) || changedValue == "1");
        command.Parameters.AddWithValue("@longValue", ParseLong(changedValue, dataType));
        command.Parameters.AddWithValue("@singleValue", ParseDecimal(changedValue, dataType));
        command.Parameters.AddWithValue("@textValue", changedValue);
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Option save update completed: setting={Setting}, affectedRows={AffectedRows}", setting, affectedRows);

        await using var verify = new SqlCommand("SELECT EinstellungDatentyp, WertTXT, WertBOOL, WertLONG, WertSNG, WertMEMO FROM TOPqw_Einstellungen WHERE Einstellung = @setting AND ISNULL(fkUSID, 0) = 0", connection);
        verify.Parameters.AddWithValue("@setting", setting);
        await using var reader = await verify.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            _logger.LogInformation("Option save read-back: setting={Setting}, dataType={DataType}, valueColumn={ValueColumn}, valueLength={ValueLength}", setting, reader.IsDBNull(0) ? null : reader.GetString(0), column, GetValueLength(reader, column));
        }
        else
        {
            _logger.LogWarning("Option save read-back found no global row: setting={Setting}", setting);
        }

        return affectedRows > 0;
    }

    private static long ParseLong(string value, string dataType)
        => dataType.Equals("WertLONG", StringComparison.OrdinalIgnoreCase) || dataType.Equals("WertINT", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : throw new ArgumentException("Der Wert ist keine gültige Ganzzahl.", nameof(value))
            : 0;

    private static int GetValueLength(SqlDataReader reader, string column)
    {
        var ordinal = column switch
        {
            "WertTXT" => 1,
            "WertBOOL" => 2,
            "WertLONG" => 3,
            "WertSNG" => 4,
            "WertMEMO" => 5,
            _ => -1
        };
        return ordinal < 0 || reader.IsDBNull(ordinal) ? 0 : reader.GetValue(ordinal).ToString()?.Length ?? 0;
    }

    private static decimal ParseDecimal(string value, string dataType)
        => dataType.Equals("WertSNG", StringComparison.OrdinalIgnoreCase)
            ? string.IsNullOrWhiteSpace(value) ? 0 : decimal.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : throw new ArgumentException("Der Wert ist keine gültige Dezimalzahl.", nameof(value))
            : 0;

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
public sealed record DisplayOption(string Einstellung, string Bezeichnung, string Beschreibung, bool WertBOOL, bool Aenderbar);
