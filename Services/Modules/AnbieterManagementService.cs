using Microsoft.Data.SqlClient;
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
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
        _user = user;
        _permissions = permissions;
    }

    public async Task<bool> CanEditAsync(CancellationToken cancellationToken)
    {
        return _user.BEID is int beid && await _permissions.HasPermissionAsync(beid, EditPermission, cancellationToken);
    }

    public async Task<bool> UpdateSimpleValueAsync(string eiid, int itemId, string value, CancellationToken cancellationToken)
    {
        if (_user.BEID is not int beid || !await _permissions.HasPermissionAsync(beid, EditPermission, cancellationToken)) return false;

        const string sql = """
            DECLARE @stuid nvarchar(50) = (
                SELECT TOP (1) st.STUID
                FROM Einrichtungen e INNER JOIN Standort st ON st.FKEIID = e.EIID
                WHERE e.EIID = @eiid);
            IF @stuid IS NULL OR NOT EXISTS (
                SELECT 1 FROM AMLBegriff b
                INNER JOIN AMLUnterbegriff u ON u.AUID = b.fkAUID
                INNER JOIN AMLOberbegriff o ON o.AOID = u.FKAOID
                WHERE b.ABID = @abid AND b.Darstellungstyp IN (1,2,3,8,14))
                RETURN;
            DECLARE @type int = (SELECT Darstellungstyp FROM AMLBegriff WHERE ABID = @abid);
            IF EXISTS (SELECT 1 FROM AMWWert WHERE fkABID = @abid AND fkSTUID = @stuid AND ISNULL(fkAYID, 0) = 0 AND ISNULL(fkAXID, 0) = 0)
                UPDATE AMWWert SET WertTXT = CASE WHEN @type = 2 THEN @value ELSE WertTXT END,
                    WertBOOL = CASE WHEN @type IN (1,3) THEN TRY_CONVERT(bit, @value) ELSE WertBOOL END,
                    WertINT = CASE WHEN @type = 8 THEN TRY_CONVERT(int, @value) ELSE WertINT END,
                    WertDEC = CASE WHEN @type = 14 THEN TRY_CONVERT(decimal(18,6), @value) ELSE WertDEC END,
                    GeaendertAm = GETDATE()
                WHERE fkABID = @abid AND fkSTUID = @stuid AND ISNULL(fkAYID, 0) = 0 AND ISNULL(fkAXID, 0) = 0;
            ELSE
                INSERT INTO AMWWert (fkSTUID, fkABID, WertTXT, WertBOOL, WertINT, WertDEC, GeaendertAm)
                VALUES (@stuid, @abid, CASE WHEN @type = 2 THEN @value END,
                    CASE WHEN @type IN (1,3) THEN TRY_CONVERT(bit, @value) END,
                    CASE WHEN @type = 8 THEN TRY_CONVERT(int, @value) END,
                    CASE WHEN @type = 14 THEN TRY_CONVERT(decimal(18,6), @value) END, GETDATE());
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eiid", eiid);
        command.Parameters.AddWithValue("@abid", itemId);
        command.Parameters.AddWithValue("@value", value ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<AmFacility>> GetFacilitiesAsync(CancellationToken cancellationToken)
    {
        if (_user.BEID is not int beid || beid <= 0) return [];

        const string sql = """
            SELECT DISTINCT ei.EIID, ei.Einrichtungsname, ei.Einrichtungskennzeichen,
                   st.STUID
            FROM Einrichtungen ei
            INNER JOIN Standorte st ON st.FKEIID = ei.EIID
            WHERE ei.Status <> 9
              AND (EXISTS (SELECT 1 FROM IndexBenutzerEinrichtung ibe WHERE ibe.FKBEID = @beid AND ibe.FKEIID = ei.EIID)
                   OR ei.FKTRID IN (SELECT FKTRID FROM Benutzer WHERE BEID = @beid)
                   OR EXISTS (SELECT 1 FROM IndexBenutzerKreise ibk WHERE ibk.FKBEID = @beid AND ibk.FKKRID = ei.FKKRID))
            ORDER BY ei.Einrichtungskennzeichen, st.STUID;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@beid", beid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<AmFacility>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(ReadString(reader, 0), ReadString(reader, 1), ReadString(reader, 2), ReadString(reader, 3)));
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
                   b.Darstellungstyp, b.Datenherkunft,
                   CASE b.Darstellungstyp
                       WHEN 1 THEN CONVERT(nvarchar(max), w.WertBOOL)
                       WHEN 2 THEN CONVERT(nvarchar(max), w.WertTXT)
                       WHEN 3 THEN CONVERT(nvarchar(max), w.WertBOOL)
                       WHEN 8 THEN CONVERT(nvarchar(max), w.WertINT)
                       WHEN 14 THEN CONVERT(nvarchar(max), w.WertDEC)
                       ELSE COALESCE(CONVERT(nvarchar(max), w.WertTXT), CONVERT(nvarchar(max), w.WertDEC),
                                     CONVERT(nvarchar(max), w.WertINT), CONVERT(nvarchar(max), w.WertBOOL))
                   END AS Wert
            FROM AMLUnterbegriff u
            INNER JOIN AMLBegriff b ON b.fkAUID = u.AUID
            LEFT JOIN AMWWert w ON w.fkABID = b.ABID AND w.fkSTUID = @stuid
            WHERE u.FKAOID = @aoid
              AND NOT EXISTS (
                  SELECT 1 FROM AMLIndexBegriffEinrichtungstyp x
                  INNER JOIN Einrichtungen e ON e.FKETID = x.FKETID
                  WHERE x.FKBFID = b.ABID AND x.fkUID = b.fkAUID AND e.EIID = @eiid)
            ORDER BY u.Reihenfolge, u.Spalte, b.Reihenfolge;
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
            var displayType = GetDisplayTypeName(displayTypeCode);
            subcategory.Items.Add(new(ReadInt(reader, 5), ReadString(reader, 7), ReadInt(reader, 8), ReadInt(reader, 9),
                displayType, displayTypeCode, ReadString(reader, 11), ReadString(reader, 12), IsSimpleEditableType(displayTypeCode)));
        } while (await reader.ReadAsync(cancellationToken));

        category.Subcategories.AddRange(groups.Values);
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
    private static bool IsSimpleEditableType(int displayType) => displayType is 1 or 2 or 3 or 8 or 14;
    private static string GetDisplayTypeName(int displayType) => displayType switch
    {
        1 => "JaNein",
        2 => "Text",
        3 => "Check",
        8 => "Ganzzahl",
        14 => "Dezimalzahl",
        _ => $"Typ {displayType}"
    };
}

public sealed record AmFacility(string EIID, string Name, string Indicator, string STUID);
public sealed record AmCategory(int Id, string Name, int Order, string Symbol, List<AmSubcategory> Subcategories);
public sealed record AmSubcategory(int Id, string Name, int Order, int Column, List<AmItem> Items);
public sealed record AmItem(int Id, string Name, int Order, int Width, string DisplayType, int DisplayTypeCode, string DataSource, string Value, bool Editable);
