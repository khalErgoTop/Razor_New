using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.RegularExpressions;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Legacy.Adapters;

public sealed class StammdatenAnalysisAdapter
{
    private readonly string _connectionString;

    public StammdatenAnalysisAdapter(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
    }

    public IReadOnlyList<StammdatenAnalysisTypeResponse> GetAnalysisTypes()
    {
        return new[]
        {
            new StammdatenAnalysisTypeResponse(2, "TOPqw-Auswertungen"),
            new StammdatenAnalysisTypeResponse(1, "InfoCenter Auswertungen")
        };
    }

    public async Task<StammdatenAnalysisFiltersResponse> GetFiltersAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var facilityTypes = await ReadFacilityTypesAsync(connection, cancellationToken);
        var districts = await ReadDistrictsAsync(connection, cancellationToken);
        return new StammdatenAnalysisFiltersResponse(facilityTypes, districts);
    }

    public async Task<IReadOnlyList<StammdatenAnalysisResponse>> GetTqwAnalysesAsync(int BEID, CancellationToken cancellationToken)
    {
        const string sql = "SELECT Id, Displayname, AbfrageTyp, Kriterium FROM [Abfragen] WHERE FKUSID IN (0, @BEID)";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BEID", BEID);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<StammdatenAnalysisResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new StammdatenAnalysisResponse(
                Convert.ToInt32(reader.GetValue(0)),
                Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                Convert.ToInt32(reader.GetValue(2)),
                reader.IsDBNull(3) ? string.Empty : Convert.ToString(reader.GetValue(3)) ?? string.Empty));
        }

        return result;
    }

    public async Task<StammdatenDashboardResultResponse> GetDashboardAsync(
        string dashboardName,
        int BEID,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            throw new ArgumentException("Ein Dashboard muss angegeben werden.", nameof(dashboardName));
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("GetDashboardByName", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@dashName", dashboardName);
        command.Parameters.AddWithValue("@uid", BEID);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new KeyNotFoundException("Das ausgewählte Dashboard wurde nicht gefunden.");
        }

        var dashboardId = ReadInt(reader, "DID");
        var result = new StammdatenDashboardResultResponse
        {
            Id = dashboardId,
            AbfrageName = ReadString(reader, "Abfragename"),
            Type = ReadString(reader, "Typ"),
            CssTyp = ReadString(reader, "CssTyp"),
            Ueberschrift = ReadString(reader, "Ueberschrift"),
            Statement = ReadString(reader, "Statement"),
            Openfunc = ReadString(reader, "Openfunc"),
            ReloadFunc = ReadString(reader, "ReloadFunc"),
            DiagrammFarben = ReadString(reader, "DiagrammFarben"),
            AutoReload = ReadInt(reader, "AutoReload"),
            AllowShowMore = ReadInt(reader, "AllowShowMore")
        };

        await reader.CloseAsync();
        result.AvailableRestrictions = await GetDashboardRestrictionsAsync(connection, dashboardId, BEID, cancellationToken);
        result.UserRestrictions = result.AvailableRestrictions.Where(restriction => restriction.Selected).ToArray();

        switch (result.Type.ToLowerInvariant())
        {
            case "table":
                await LoadDashboardTableDataAsync(connection, result, cancellationToken);
                break;
            case "text":
                await LoadDashboardTextDataAsync(connection, result, cancellationToken);
                break;
            case "pie":
            case "bar":
            case "line":
                await LoadDashboardChartDataAsync(connection, result, cancellationToken);
                break;
            default:
                result.ExecutionMessage = "Dieser Dashboard-Typ ist in Razor Pages noch nicht portiert.";
                break;
        }

        return result;
    }

    private static async Task LoadDashboardTableDataAsync(
        SqlConnection connection,
        StammdatenDashboardResultResponse result,
        CancellationToken cancellationToken)
    {
        if (!IsSafeReadOnlySelect(result.Statement))
        {
            result.ExecutionMessage = "Das gespeicherte Dashboard-Statement ist kein zulässiges SELECT-Statement.";
            return;
        }

        var statement = new Regex("(?i)\\bselect\\s+").Replace(result.Statement, "SELECT TOP 50 ", 1);
        await using var command = new SqlCommand(statement, connection);
        var parameterIndex = 0;
        foreach (var restriction in result.UserRestrictions)
        {
            if (!IsSafeIdentifier(restriction.Table) || !IsSafeIdentifier(restriction.Column) || string.IsNullOrWhiteSpace(restriction.UserSelection)) continue;
            if (!string.Equals(restriction.UserOperator, "IN", StringComparison.OrdinalIgnoreCase)) continue;

            var values = restriction.UserSelection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var parameters = new List<string>();
            foreach (var value in values)
            {
                var parameterName = "@dashRestriction" + parameterIndex++;
                parameters.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, value);
            }

            if (parameters.Count > 0)
            {
                statement += (statement.Contains(" WHERE ", StringComparison.OrdinalIgnoreCase) ? " AND " : " WHERE ")
                    + $"[{restriction.Table}].[{restriction.Column}] IN ({string.Join(",", parameters)})";
            }
        }

        command.CommandText = statement;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var structure = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in structure)
            {
                var value = reader[column];
                row[column] = value == DBNull.Value ? null : value;
            }

            rows.Add(row);
        }

        result.Structure = structure;
        result.Rows = rows;
    }

    private static async Task LoadDashboardTextDataAsync(
        SqlConnection connection,
        StammdatenDashboardResultResponse result,
        CancellationToken cancellationToken)
    {
        var rows = await ExecuteDashboardStatementAsync(connection, result, cancellationToken);
        result.Structure = rows.Structure;
        result.Rows = rows.Rows;
        var firstRow = rows.Rows.FirstOrDefault();
        if (firstRow is not null && firstRow.TryGetValue("value", out var value))
        {
            result.TextValue = Convert.ToString(value);
        }
    }

    private static async Task LoadDashboardChartDataAsync(
        SqlConnection connection,
        StammdatenDashboardResultResponse result,
        CancellationToken cancellationToken)
    {
        var rows = await ExecuteDashboardStatementAsync(connection, result, cancellationToken);
        result.Structure = rows.Structure;
        result.Rows = rows.Rows;
        result.ChartType = result.Type;
        result.ChartLabels = rows.Rows
            .Select(row => row.TryGetValue("label", out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty)
            .ToArray();
        result.ChartValues = rows.Rows
            .Select(row => row.TryGetValue("data", out var value) ? value : null)
            .ToArray();
    }

    private static async Task<(IReadOnlyList<string> Structure, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows)> ExecuteDashboardStatementAsync(
        SqlConnection connection,
        StammdatenDashboardResultResponse result,
        CancellationToken cancellationToken)
    {
        if (!IsSafeReadOnlySelect(result.Statement))
        {
            result.ExecutionMessage = "Das gespeicherte Dashboard-Statement ist kein zulässiges SELECT-Statement.";
            return ([], []);
        }

        var statement = new Regex("(?i)\\bselect\\s+").Replace(result.Statement, "SELECT TOP 50 ", 1);
        await using var command = new SqlCommand(statement, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var structure = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in structure)
            {
                var value = reader[column];
                row[column] = value == DBNull.Value ? null : value;
            }

            rows.Add(row);
        }

        return (structure, rows);
    }

    private static bool IsSafeIdentifier(string value)
        => Regex.IsMatch(value ?? string.Empty, "^[A-Za-z_][A-Za-z0-9_]*$");

    public async Task SaveDashboardRestrictionsAsync(
        string dashboardName,
        int BEID,
        IReadOnlyList<StammdatenDashboardRestrictionInput> restrictions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) || BEID <= 0)
        {
            throw new ArgumentException("Dashboard oder Benutzer ist ungültig.");
        }

        var dashboards = await GetDashboardsAsync(BEID, cancellationToken);
        if (!dashboards.Any(item => string.Equals(item.AbfrageName, dashboardName, StringComparison.Ordinal)))
        {
            throw new KeyNotFoundException("Das Dashboard ist für den Benutzer nicht verfügbar.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var restriction in restrictions)
        {
            if (string.IsNullOrWhiteSpace(restriction.Name)) continue;
            await using var command = new SqlCommand("SaveDashboardUserRestrictionsByName", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@DashName", dashboardName);
            command.Parameters.AddWithValue("@RestName", restriction.Name);
            command.Parameters.AddWithValue("@selection", restriction.Selection ?? string.Empty);
            command.Parameters.AddWithValue("@operator", restriction.Name is "Einrichtungstyp" or "Kreis" ? "IN" : restriction.UserOperator ?? string.Empty);
            command.Parameters.AddWithValue("@uid", BEID);
            command.Parameters.AddWithValue("@selected", !string.IsNullOrWhiteSpace(restriction.Selection));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<StammdatenDashboardRestrictionResponse>> GetDashboardRestrictionsAsync(
        SqlConnection connection,
        int dashboardId,
        int BEID,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("GetDashboardUserRestrictions", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@uid", BEID);
        command.Parameters.AddWithValue("@did", dashboardId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<StammdatenDashboardRestrictionResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new StammdatenDashboardRestrictionResponse(
                ReadInt(reader, "DSRID"),
                ReadInt(reader, "DRID"),
                ReadString(reader, "Bezeichnung"),
                ReadString(reader, "Datentyp"),
                ReadString(reader, "Spalte"),
                ReadString(reader, "Tabelle"),
                ReadString(reader, "Selection"),
                ReadString(reader, "Operator"),
                ReadBool(reader, "Selected")));
        }

        return result;
    }

    private static string ReadString(SqlDataReader reader, string name)
        => reader.GetSchemaTable()?.Rows.Cast<DataRow>().Any(row => string.Equals(Convert.ToString(row["ColumnName"]), name, StringComparison.OrdinalIgnoreCase)) == true
            && !reader.IsDBNull(reader.GetOrdinal(name))
            ? Convert.ToString(reader[name]) ?? string.Empty
            : string.Empty;

    private static int ReadInt(SqlDataReader reader, string name)
        => int.TryParse(ReadString(reader, name), out var value) ? value : 0;

    private static bool ReadBool(SqlDataReader reader, string name)
        => bool.TryParse(ReadString(reader, name), out var value) && value;

    public async Task<StammdatenAnalysisResultResponse> GetTqwAnalysisAsync(
        int id,
        int BEID,
        IReadOnlyDictionary<string, string?> selections,
        CancellationToken cancellationToken)
    {
        const string analysisSql = "SELECT [AbfrageTyp], [Kriterium], [SQLStatement] FROM [Abfragen] WHERE [Id] = @id AND [FKUSID] IN (0, @BEID)";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var analysisCommand = new SqlCommand(analysisSql, connection);
        analysisCommand.Parameters.AddWithValue("@id", id);
        analysisCommand.Parameters.AddWithValue("@BEID", BEID);
        await using var analysisReader = await analysisCommand.ExecuteReaderAsync(cancellationToken);
        if (!await analysisReader.ReadAsync(cancellationToken))
        {
            throw new KeyNotFoundException("Die ausgewählte Auswertung wurde nicht gefunden.");
        }

        var abfrageTyp = Convert.ToInt32(analysisReader["AbfrageTyp"]);
        var kriterium = Convert.ToString(analysisReader["Kriterium"]) ?? string.Empty;
        var sqlStatement = Convert.ToString(analysisReader["SQLStatement"]) ?? string.Empty;
        await analysisReader.CloseAsync();

        if (abfrageTyp != 11)
        {
            throw new NotSupportedException("Dieser Auswertungstyp ist in Razor Pages noch nicht portiert.");
        }

        if (!IsSafeReadOnlySelect(sqlStatement))
        {
            throw new InvalidOperationException("Das gespeicherte Auswertungsstatement ist kein zulässiges SELECT-Statement.");
        }

        var sql = new Regex("(?i)\\bselect\\s+").Replace(sqlStatement, "SELECT TOP 50 ", 1);
        await using var command = new SqlCommand(sql, connection);
        AddAnalysisParameters(command, kriterium, selections);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var structure = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var data = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in structure)
            {
                var value = reader[column];
                row[column] = value == DBNull.Value ? null : value;
            }

            data.Add(row);
        }

        return new StammdatenAnalysisResultResponse
        {
            Structure = structure,
            Data = data,
            AbfrageTyp = abfrageTyp,
            AbfrageTyKriteria = kriterium
        };
    }

    public async Task<IReadOnlyList<StammdatenDashboardResponse>> GetDashboardsAsync(int BEID, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = new SqlCommand("GetDashboardsAvailable", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@uid", BEID);
            command.Parameters.AddWithValue("@style", "TOPqw");
            return await ReadDashboardsAsync(command, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            await using var fallback = new SqlCommand("""
                SELECT ds.AbfrageName, ds.Ueberschrift
                FROM DashboardStatements ds
                LEFT JOIN DashboardUserParameter dup
                    ON dup.FKDID = ds.DID AND dup.FKUID = @uid
                INNER JOIN (
                    SELECT ms.Modul, SUM(CONVERT(int, ms.Visible)) AS DBVisible
                    FROM Modulstruktur ms
                    INNER JOIN Module m ON m.MDID = ms.FKMDID
                    WHERE ms.Bundesland = (SELECT DBBundesland FROM Version)
                    GROUP BY ms.Modul
                ) modules ON modules.Modul = ds.CssTyp
                WHERE ISNULL(dup.Visible, 1) = 1
                  AND ISNULL(ds.TOPqwSichtbar, 0) = 1
                  AND modules.DBVisible > 0
                  AND NOT ds.Typ LIKE '%_web'
                ORDER BY AbfrageName;
                """, connection);
            fallback.Parameters.AddWithValue("@uid", BEID);
            return await ReadDashboardsAsync(fallback, cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<StammdatenDashboardResponse>> ReadDashboardsAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<StammdatenDashboardResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new StammdatenDashboardResponse(
                Convert.ToString(reader["AbfrageName"]) ?? string.Empty,
                Convert.ToString(reader["Ueberschrift"]) ?? string.Empty));
        }

        return result;
    }

    private static bool IsSafeReadOnlySelect(string sqlStatement)
    {
        var normalized = sqlStatement.Trim();
        if (!Regex.IsMatch(normalized, "(?i)^select\\b") || normalized.Contains(';') || normalized.Contains("--") || normalized.Contains("/*"))
        {
            return false;
        }

        return !Regex.IsMatch(normalized, "(?i)\\b(insert|update|delete|merge|drop|alter|exec|execute|truncate|declare)\\b");
    }

    private static void AddAnalysisParameters(SqlCommand command, string kriterium, IReadOnlyDictionary<string, string?> selections)
    {
        foreach (var criterion in kriterium.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parameterName = criterion.Split('=', 2)[0].Trim();
            if (!Regex.IsMatch(parameterName, "^@?[A-Za-z_][A-Za-z0-9_]*$"))
            {
                throw new InvalidOperationException("Das Auswertungskriterium enthält einen ungültigen Parameter.");
            }

            var normalizedParameterName = parameterName.StartsWith('@') ? parameterName : "@" + parameterName;
            var selectionName = normalizedParameterName.TrimStart('@') switch
            {
                "JAHR" => "Stichtag",
                "KRID" => "Kreis",
                "ETID" => "Einrichtungstyp",
                "TYP" => "Typ",
                "ZeitraumVon" => "ZeitraumVon",
                "ZeitraumBis" => "ZeitraumBis",
                _ => null
            };

            if (selectionName is null)
            {
                throw new NotSupportedException($"Das Auswertungskriterium {parameterName} ist noch nicht portiert.");
            }

            var value = selections.TryGetValue(selectionName, out var selectedValue) && !string.IsNullOrWhiteSpace(selectedValue)
                ? selectedValue
                : GetDefaultAnalysisValue(selectionName);
            command.Parameters.AddWithValue(normalizedParameterName, value);
        }
    }

    private static string GetDefaultAnalysisValue(string selectionName)
    {
        var today = DateTime.Now;
        return selectionName switch
        {
            "Stichtag" => today.ToString("yyyyMMdd"),
            "ZeitraumVon" => new DateTime(today.Year, today.Month, 1).ToString("yyyyMMdd"),
            "ZeitraumBis" => new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)).ToString("yyyyMMdd"),
            _ => "0"
        };
    }

    private static async Task<IReadOnlyList<StammdatenFacilityTypeResponse>> ReadFacilityTypesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT [ETID], [Typkennzeichnung] FROM [Einrichtungstypen] ORDER BY [Reihenfolge], [Kurzbezeichnung], [Typkennzeichnung]";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<StammdatenFacilityTypeResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new StammdatenFacilityTypeResponse(Convert.ToInt32(reader.GetValue(0)), Convert.ToString(reader.GetValue(1)) ?? string.Empty));
        }

        return result;
    }

    private static async Task<IReadOnlyList<StammdatenDistrictResponse>> ReadDistrictsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT [KRID], [Kreis] FROM [Kreise] ORDER BY [Reihenfolge]";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<StammdatenDistrictResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new StammdatenDistrictResponse(Convert.ToInt32(reader.GetValue(0)), Convert.ToString(reader.GetValue(1)) ?? string.Empty));
        }

        return result;
    }
}