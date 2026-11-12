using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Text;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Services.Modules;

public sealed class AnbieterManagementService
{
    private const string EditPermission = "459CE8E4-81C7-4D25-8804-EA71E04E3DDB";
    private readonly string _connectionString;
    private readonly CurrentUserAccessor _user;
    private readonly LegacyPermissionService _permissions;

    public AnbieterManagementService(IConfiguration configuration, CurrentUserAccessor user, LegacyPermissionService permissions)
    {
        _connectionString = configuration.GetConnectionString("TOPqm")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqm muss konfiguriert werden.");
        _user = user;
        _permissions = permissions;
    }

    public async Task<bool> CanEditAsync(CancellationToken cancellationToken)
    {
        return _user.BEID is int beid && await _permissions.HasPermissionAsync(beid, EditPermission, cancellationToken);
    }

    public async Task<bool> SaveValuesAsync(string eiid, string stuid, int categoryId, IReadOnlyDictionary<int, string?> values, IReadOnlyDictionary<int, string?> originals, IReadOnlyDictionary<int, string?> selections, IReadOnlyDictionary<int, string?> singleSelections, IReadOnlyDictionary<int, string?> originalSelections, CancellationToken cancellationToken)
    {
        if (_user.BEID is not int beid || !await _permissions.HasPermissionAsync(beid, EditPermission, cancellationToken)) return false;
        var facility = await GetFacilityAsync(eiid, stuid, cancellationToken);
        if (facility is null) return false;
        var category = await GetCategoryAsync(categoryId, facility.EIID, facility.STUID, cancellationToken);
        if (category is null) return false;
        var allItems = category.Subcategories.SelectMany(group => group.Items).ToArray();
        var items = allItems.Where(item => item.Editable && !item.IsSelection).ToDictionary(item => item.Id);
        var lists = allItems.Where(item => item.Editable && item.IsSelection).ToArray();
        var listOptions = lists.SelectMany(item => item.Options).ToDictionary(option => option.Id);
        if (selections.Keys.Any(id => !listOptions.ContainsKey(id)) || originalSelections.Keys.Any(id => !listOptions.ContainsKey(id)) ||
            singleSelections.Keys.Any(id => !lists.Any(item => item.Id == id && item.IsSingleSelection))) return false;
        var updates = new List<(AmItem Item, object Value)>();
        var selectionUpdates = new List<(AmItem Item, AmOption Option, bool Original, bool Value)>();
        foreach (var entry in values)
        {
            if (!items.TryGetValue(entry.Key, out var item) || !originals.TryGetValue(entry.Key, out var encoded) || encoded is null || !encoded.StartsWith('v')) return false;
            string original;
            try { original = Encoding.UTF8.GetString(Convert.FromBase64String(encoded[1..])); }
            catch (FormatException) { return false; }
            var input = entry.Value ?? string.Empty;
            if (input.Replace("\r\n", "\n") == original.Replace("\r\n", "\n")) continue;
            if (item.DisplayTypeCode == 12 && TryParseTime(input, out var inputTime) && TryParseTime(original, out var originalTime) && inputTime == originalTime) continue;
            if (original != item.Value) return false;
            object value;
            if (item.DisplayTypeCode is 1 or 3)
            {
                if (input == "") value = DBNull.Value;
                else if (input == "1") value = true;
                else if (input == "0") value = false;
                else return false;
            }
            else if (item.DisplayTypeCode == 8)
            {
                if (input == "") value = DBNull.Value;
                else if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) value = number;
                else return false;
            }
            else if (item.DisplayTypeCode == 14)
            {
                if (input == "") value = DBNull.Value;
                else if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)) value = number;
                else return false;
            }
            else if (item.DisplayTypeCode == 12 && !IsValidTime(input)) return false;
            else value = input;
            updates.Add((item, value));
        }
        foreach (var list in lists)
        {
            if (list.Options.Count == 0) continue;
            var hasInput = list.IsSingleSelection ? singleSelections.ContainsKey(list.Id) : list.Options.Any(option => selections.ContainsKey(option.Id));
            if (!hasInput) continue;
            var desired = new Dictionary<int, bool>();
            if (list.IsSingleSelection)
            {
                var input = singleSelections[list.Id];
                if (input == "__multiple") continue;
                if (!string.IsNullOrEmpty(input) && (!int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var selectedId) || !list.Options.Any(option => option.Id == selectedId))) return false;
                foreach (var option in list.Options) desired[option.Id] = input == option.Id.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                foreach (var option in list.Options)
                {
                    if (!selections.TryGetValue(option.Id, out var input) || (input != "0" && input != "1")) return false;
                    desired[option.Id] = input == "1";
                }
            }
            var original = new Dictionary<int, bool>();
            foreach (var option in list.Options)
            {
                if (!originalSelections.TryGetValue(option.Id, out var input) || (input != "0" && input != "1")) return false;
                original[option.Id] = input == "1";
            }
            if (list.Options.All(option => desired[option.Id] == original[option.Id])) continue;
            if (list.Options.Any(option => option.Selected != original[option.Id])) return false;
            foreach (var option in list.Options)
                selectionUpdates.Add((list, option, original[option.Id], desired[option.Id]));
        }
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        foreach (var (item, value) in updates)
        {
            var column = item.DisplayTypeCode switch { 1 or 3 => "WertBOOL", 8 => "WertINT", 14 => "WertSNG", _ => "WertMEMO" };
            var currentValue = item.DisplayTypeCode == 14
                ? "CONVERT(nvarchar(max), CONVERT(varchar(100), CONVERT(float, WertSNG), 3))"
                : $"CONVERT(nvarchar(max), {column})";
            var sql = $"""
                IF NOT EXISTS (SELECT 1 FROM AMLBegriff b JOIN AMLUnterbegriff u ON u.AUID = b.fkAUID
                    WHERE b.ABID = @abid AND u.FKAOID = @aoid AND LOWER(b.Darstellungstyp) = @type
                      AND LOWER(b.Datenherkunft) = 'amwwert')
                    THROW 50000, 'Der Begriff ist nicht mehr bearbeitbar.', 1;
                IF (SELECT COUNT(*) FROM AMWWert WITH (UPDLOCK, HOLDLOCK) WHERE fkABID = @abid AND fkSTUID = @stuid AND ISNULL(fkAYID, 0) = 0 AND ISNULL(fkAXID, 0) = 0) > 1
                    THROW 50000, 'Für diesen Begriff sind mehrere einfache Werte vorhanden.', 1;
                IF EXISTS (SELECT 1 FROM AMWWert WHERE fkABID = @abid AND fkSTUID = @stuid AND ISNULL(fkAYID, 0) = 0 AND ISNULL(fkAXID, 0) = 0
                    AND (COALESCE({currentValue}, N'') COLLATE Latin1_General_100_BIN2 <> @original COLLATE Latin1_General_100_BIN2
                         OR DATALENGTH(COALESCE({currentValue}, N'')) <> DATALENGTH(@original)))
                    OR (@original <> N'' AND NOT EXISTS (SELECT 1 FROM AMWWert WHERE fkABID = @abid AND fkSTUID = @stuid AND ISNULL(fkAYID, 0) = 0 AND ISNULL(fkAXID, 0) = 0))
                    THROW 50000, 'Der Wert wurde zwischenzeitlich geändert.', 1;
                IF EXISTS (SELECT 1 FROM AMWWert WITH (UPDLOCK, HOLDLOCK) WHERE fkABID = @abid AND fkSTUID = @stuid AND ISNULL(fkAYID, 0) = 0 AND ISNULL(fkAXID, 0) = 0)
                    UPDATE AMWWert SET {column} = @value, GeaendertAm = GETDATE()
                    WHERE fkABID = @abid AND fkSTUID = @stuid AND ISNULL(fkAYID, 0) = 0 AND ISNULL(fkAXID, 0) = 0;
                ELSE
                    INSERT INTO AMWWert (fkSTUID, fkABID, {column}, GeaendertAm) VALUES (@stuid, @abid, @value, GETDATE());
                """;
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@stuid", facility.STUID);
            command.Parameters.AddWithValue("@abid", item.Id);
            command.Parameters.AddWithValue("@aoid", categoryId);
            command.Parameters.AddWithValue("@type", item.DisplayType.ToLowerInvariant());
            command.Parameters.Add("@original", SqlDbType.NVarChar, -1).Value = item.Value;
            var parameter = command.Parameters.Add("@value", item.DisplayTypeCode switch { 1 or 3 => SqlDbType.Bit, 8 => SqlDbType.Int, 14 => SqlDbType.Float, _ => SqlDbType.NVarChar });
            if (parameter.SqlDbType == SqlDbType.NVarChar) parameter.Size = -1;
            parameter.Value = value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        const string selectionSql = """
            IF NOT EXISTS (SELECT 1 FROM AMLYKategorie y JOIN AMLBegriff b ON b.ABID = y.fkABID
                JOIN AMLUnterbegriff u ON u.AUID = b.fkAUID
                WHERE y.AYID = @ayid AND b.ABID = @abid AND u.FKAOID = @aoid
                  AND LOWER(b.Darstellungstyp) = @type AND LOWER(b.Datenherkunft) = 'amwwert')
                THROW 50000, 'Die Auswahl ist nicht mehr bearbeitbar.', 1;
            IF (SELECT COUNT(*) FROM AMWWert WITH (UPDLOCK, HOLDLOCK) WHERE fkSTUID = @stuid AND fkABID = @abid AND fkAYID = @ayid AND ISNULL(fkAXID, 0) = 0) > 1
                THROW 50000, 'Für diese Auswahl sind mehrere Werte vorhanden.', 1;
            IF COALESCE((SELECT CONVERT(int, WertBOOL) FROM AMWWert WHERE fkSTUID = @stuid AND fkABID = @abid AND fkAYID = @ayid AND ISNULL(fkAXID, 0) = 0), 0) <> @original
                THROW 50000, 'Die Auswahl wurde zwischenzeitlich geändert.', 1;
            IF @value <> @original
            BEGIN
                IF EXISTS (SELECT 1 FROM AMWWert WHERE fkSTUID = @stuid AND fkABID = @abid AND fkAYID = @ayid AND ISNULL(fkAXID, 0) = 0)
                    UPDATE AMWWert SET WertBOOL = @value, GeaendertAm = GETDATE() WHERE fkSTUID = @stuid AND fkABID = @abid AND fkAYID = @ayid AND ISNULL(fkAXID, 0) = 0;
                ELSE
                    INSERT INTO AMWWert (fkSTUID, fkABID, fkAYID, WertBOOL, GeaendertAm) VALUES (@stuid, @abid, @ayid, @value, GETDATE());
            END;
            """;
        foreach (var (item, option, original, value) in selectionUpdates.OrderBy(update => update.Option.Id))
        {
            await using var command = new SqlCommand(selectionSql, connection, transaction);
            command.Parameters.AddWithValue("@stuid", facility.STUID);
            command.Parameters.AddWithValue("@abid", item.Id);
            command.Parameters.AddWithValue("@ayid", option.Id);
            command.Parameters.AddWithValue("@aoid", categoryId);
            command.Parameters.AddWithValue("@type", item.DisplayType.ToLowerInvariant());
            command.Parameters.Add("@original", SqlDbType.Bit).Value = original;
            command.Parameters.Add("@value", SqlDbType.Bit).Value = value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AmFacility?> GetFacilityAsync(string? eiid, string? stuid, CancellationToken cancellationToken)
    {
        if ((eiid is not null && !Guid.TryParse(eiid, out _)) ||
            (stuid is not null && !Guid.TryParse(stuid, out _))) return null;
        var facilities = await GetFacilitiesAsync(eiid, stuid, null, 1, cancellationToken);
        return facilities.FirstOrDefault();
    }

    public Task<IReadOnlyList<AmFacility>> SearchFacilitiesAsync(string search, int limit, CancellationToken cancellationToken)
        => GetFacilitiesAsync(null, null, search, limit, cancellationToken);

    private async Task<IReadOnlyList<AmFacility>> GetFacilitiesAsync(string? eiid, string? stuid, string? search, int limit, CancellationToken cancellationToken)
    {
        if (_user.BEID is not int beid || beid <= 0) return [];

        // Match cBenutzerWeb.GetUserBaseFacilities and AMBogen.GetSites in the legacy project.
        var joins = string.Empty;
        var userFilter = string.Empty;
        if (!_user.IsAdmin)
        {
            joins = _user.UserType switch
            {
                CurrentUserAccessor.LegacyUserTypes.Traeger =>
                    "INNER JOIN Benutzer ON ei.FKTRID = Benutzer.FKTRID",
                CurrentUserAccessor.LegacyUserTypes.Verband or CurrentUserAccessor.LegacyUserTypes.VerbandUser =>
                    "INNER JOIN Traeger ON ei.FKTRID = Traeger.TRID AND ISNULL(Traeger.VerbandszugriffErlauben, 1) = 1 " +
                    "INNER JOIN Benutzer ON Benutzer.TOPqwID = Traeger.FKVEID AND Benutzer.BEID = @beid " +
                    (_user.UserType == CurrentUserAccessor.LegacyUserTypes.VerbandUser
                        ? "INNER JOIN IndexBenutzerEinrichtung ON ei.EIID = IndexBenutzerEinrichtung.FKEIID AND IndexBenutzerEinrichtung.FKBEID = @beid" : ""),
                CurrentUserAccessor.LegacyUserTypes.Jugendamt =>
                    "INNER JOIN IndexBenutzerKreise ibk ON ibk.FKKRID = ei.FKKRID AND ibk.FKBEID = @beid " +
                    "INNER JOIN IndexBenutzerEinrichtungstyp ibe ON ibe.FKETID = ei.FKETID AND ibe.FKBEID = @beid",
                _ when _user.IsKosoz =>
                    "INNER JOIN IndexBenutzerEinrichtungstyp ON ei.FKETID = IndexBenutzerEinrichtungstyp.FKETID " +
                    "INNER JOIN Benutzer ON IndexBenutzerEinrichtungstyp.FKBEID = Benutzer.BEID",
                _ =>
                    "INNER JOIN IndexBenutzerEinrichtung ON ei.EIID = IndexBenutzerEinrichtung.FKEIID " +
                    "INNER JOIN Benutzer ON IndexBenutzerEinrichtung.FKBEID = Benutzer.BEID"
            };
            if (_user.UserType != CurrentUserAccessor.LegacyUserTypes.Jugendamt)
            {
                joins += " LEFT JOIN Kreise ON Kreise.KRID = ei.FKKRID";
                userFilter = "AND (Benutzer.BEID = @beid OR Kreise.KRID IN (SELECT FKKRID FROM IndexBenutzerKreise WHERE FKBEID = @beid))";
            }
        }

        var sql = $"""
            DECLARE @now datetime = GETDATE();
            DECLARE @facilitiesNow smalldatetime = GETDATE();
            DECLARE @hideClosed bit = ISNULL((SELECT WertBOOL FROM TOPqw_Einstellungen
                WHERE Einstellung LIKE 'Stammdaten.Verberge.geschlossene.Einrichtungen.in.eigener.Einrichtungsliste'), 0);
            DECLARE @onlyMain bit = ISNULL((SELECT TOP (1) WertBOOL FROM TOPqw_Einstellungen
                WHERE Einstellung = 'Anbietermanagement.NurHauptstandorte' AND ISNULL(fkUSID, 0) = 0), 1);
            SELECT DISTINCT TOP (@limit) ei.EIID, ei.Einrichtungsname, ei.Einrichtungskennzeichen, st.STUID,
                   st.Standortname, st.Hauptstandort, ei.FKETID
            FROM Einrichtungen ei
            {joins}
            INNER JOIN Standorte st ON st.FKEIID = ei.EIID
            WHERE ei.Status <> 11
              AND (@hideClosed = 0 OR ISNULL(ei.GeschlossenAm, @facilitiesNow) >= @facilitiesNow)
              AND ISNULL(CONVERT(datetime, st.GeschlossenAmStandort), @now + 1) > @now
              AND ISNULL(CONVERT(datetime, ei.GeschlossenAm), @now + 1) > @now
              AND (@onlyMain = 0 OR st.Hauptstandort = 1)
              AND (@eiid IS NULL OR ei.EIID = TRY_CONVERT(uniqueidentifier, @eiid))
              AND (@stuid IS NULL OR st.STUID = TRY_CONVERT(uniqueidentifier, @stuid))
              AND (@search IS NULL OR CHARINDEX(@search, ei.Einrichtungsname) > 0
                   OR CHARINDEX(@search, ei.Einrichtungskennzeichen) > 0 OR CHARINDEX(@search, st.Standortname) > 0)
              {userFilter}
            ORDER BY ei.Einrichtungskennzeichen, st.Hauptstandort DESC, ei.FKETID, ei.EIID, st.STUID;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@beid", beid);
        command.Parameters.Add("@limit", SqlDbType.Int).Value = limit;
        command.Parameters.Add("@eiid", SqlDbType.NVarChar, 128).Value = (object?)eiid ?? DBNull.Value;
        command.Parameters.Add("@stuid", SqlDbType.NVarChar, 128).Value = (object?)stuid ?? DBNull.Value;
        command.Parameters.Add("@search", SqlDbType.NVarChar, 100).Value = (object?)search ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<AmFacility>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(ReadString(reader, 0), ReadString(reader, 1), ReadString(reader, 2), ReadString(reader, 3), ReadString(reader, 4)));
        return result;
    }

    public async Task<IReadOnlyList<AmCategory>> GetCategoriesAsync(string stuid, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT o.AOID, o.Bezeichnung, o.Reihenfolge
            FROM AMLOberbegriff o
            ORDER BY o.Reihenfolge;
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<AmCategory>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(ReadInt(reader, 0), ReadString(reader, 1), ReadInt(reader, 2), string.Empty, []));
        return result;
    }

    public async Task<AmCategory?> GetCategoryAsync(int categoryId, string eiid, string stuid, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT o.AOID, o.Bezeichnung, o.Reihenfolge
            FROM AMLOberbegriff o WHERE o.AOID = @aoid;
            SELECT u.AUID, u.FKAOID, u.Bezeichnung, u.Reihenfolge, u.Spalte,
                   b.ABID, b.fkAUID, b.Bezeichnung, b.Reihenfolge, b.Spaltenbreite,
                   CASE LOWER(b.Darstellungstyp)
                       WHEN 'janein' THEN 1
                        WHEN 'text' THEN 2
                        WHEN 'memo' THEN 2
                       WHEN 'check' THEN 3
                       WHEN 'ganzzahl' THEN 8
                        WHEN 'zeit' THEN 12
                       WHEN 'dezimalzahl' THEN 14
                       ELSE 0
                   END AS Darstellungstyp, b.Datenherkunft,
                    CASE LOWER(b.Darstellungstyp)
                        WHEN 'janein' THEN CONVERT(nvarchar(max), w.WertBOOL)
                        WHEN 'check' THEN CONVERT(nvarchar(max), w.WertBOOL)
                        WHEN 'text' THEN CONVERT(nvarchar(max), w.WertMEMO)
                        WHEN 'memo' THEN CONVERT(nvarchar(max), w.WertMEMO)
                         WHEN 'zeit' THEN CONVERT(nvarchar(max), w.WertMEMO)
                        WHEN 'ganzzahl' THEN CONVERT(nvarchar(max), w.WertINT)
                        WHEN 'dezimalzahl' THEN CONVERT(nvarchar(max), CONVERT(varchar(100), CONVERT(float, w.WertSNG), 3))
                        ELSE NULL
                    END AS Wert, b.Darstellungstyp
            FROM AMLUnterbegriff u
            INNER JOIN AMLBegriff b ON b.fkAUID = u.AUID
            OUTER APPLY (SELECT TOP (1) * FROM AMWWert v WHERE v.fkABID = b.ABID AND v.fkSTUID = @stuid
                AND ISNULL(v.fkAYID, 0) = 0 AND ISNULL(v.fkAXID, 0) = 0 ORDER BY v.AWID) w
            WHERE u.FKAOID = @aoid
            ORDER BY u.Reihenfolge, u.Spalte, b.Reihenfolge;
            SELECT y.fkABID, y.AYID, y.Bezeichnung, y.TreeParentAYID, y.Reihenfolge, w.WertBOOL
            FROM AMLYKategorie y
            INNER JOIN AMLBegriff b ON b.ABID = y.fkABID
            INNER JOIN AMLUnterbegriff u ON u.AUID = b.fkAUID
            OUTER APPLY (SELECT TOP (1) v.WertBOOL FROM AMWWert v
                WHERE v.fkABID = y.fkABID AND v.fkAYID = y.AYID AND v.fkSTUID = @stuid
                  AND ISNULL(v.fkAXID, 0) = 0 ORDER BY v.AWID) w
            WHERE u.FKAOID = @aoid AND LOWER(b.Darstellungstyp) IN ('liste', 'optionenliste')
            ORDER BY y.Reihenfolge, y.AYID;
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@aoid", categoryId);
        command.Parameters.AddWithValue("@eiid", eiid);
        command.Parameters.AddWithValue("@stuid", stuid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var category = new AmCategory(ReadInt(reader, 0), ReadString(reader, 1), ReadInt(reader, 2), string.Empty, []);
        if (!await reader.NextResultAsync(cancellationToken)) return category;
        if (!await reader.ReadAsync(cancellationToken)) return category;

        var groups = new Dictionary<int, AmSubcategory>();
        do
        {
            var subId = ReadInt(reader, 0);
            if (!groups.TryGetValue(subId, out var subcategory))
            {
                subcategory = new(subId, ReadString(reader, 2), ReadInt(reader, 3), ReadInt(reader, 4), []);
                groups.Add(subId, subcategory);
            }
            var displayTypeCode = ReadInt(reader, 10);
            var displayType = ReadString(reader, 13);
            var isSelection = IsSelectionType(displayType);
            subcategory.Items.Add(new(ReadInt(reader, 5), ReadString(reader, 7), ReadInt(reader, 8), ReadInt(reader, 9),
                displayType, displayTypeCode, ReadString(reader, 11), ReadString(reader, 12), (IsSimpleEditableType(displayTypeCode) || isSelection) && string.Equals(ReadString(reader, 11), "AMWWert", StringComparison.OrdinalIgnoreCase)));
        } while (await reader.ReadAsync(cancellationToken));

        category.Subcategories.AddRange(groups.Values);
        var selectionItems = category.Subcategories.SelectMany(group => group.Items).Where(item => item.IsSelection).ToDictionary(item => item.Id);
        if (await reader.NextResultAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                if (selectionItems.TryGetValue(ReadInt(reader, 0), out var item))
                    item.Options.Add(new(ReadInt(reader, 1), ReadString(reader, 2), ReadInt(reader, 3), ReadInt(reader, 4), !reader.IsDBNull(5) && Convert.ToBoolean(reader.GetValue(5))));
        return category;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static int ReadInt(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    private static string ReadString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    public static bool IsValidTime(string value) => string.IsNullOrEmpty(value) || TryParseTime(value, out _);
    private static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, ["HH:mm", "HH:mm:ss", "HH:mm:ss.FFF"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    private static bool IsSimpleEditableType(int displayType) => displayType is 1 or 2 or 3 or 8 or 12 or 14;
    private static bool IsSelectionType(string displayType) => displayType.Equals("Liste", StringComparison.OrdinalIgnoreCase) || displayType.Equals("OptionenListe", StringComparison.OrdinalIgnoreCase);
}

public sealed record AmFacility(string EIID, string Name, string Indicator, string STUID, string LocationName);
public sealed record AmCategory(int Id, string Name, int Order, string Symbol, List<AmSubcategory> Subcategories);
public sealed record AmSubcategory(int Id, string Name, int Order, int Column, List<AmItem> Items);
public sealed record AmItem(int Id, string Name, int Order, int Width, string DisplayType, int DisplayTypeCode, string DataSource, string Value, bool Editable)
{
    public bool IsSelection => DisplayType.Equals("Liste", StringComparison.OrdinalIgnoreCase) || IsSingleSelection;
    public bool IsSingleSelection => DisplayType.Equals("OptionenListe", StringComparison.OrdinalIgnoreCase);
    public List<AmOption> Options { get; } = [];
}
public sealed record AmOption(int Id, string Name, int ParentId, int Order, bool Selected);
