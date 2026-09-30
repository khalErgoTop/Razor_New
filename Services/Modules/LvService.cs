
using Microsoft.Data.SqlClient;
using System.Xml.Linq;

namespace TOPqwWeb_Core.Services.Modules;

public sealed class LvService
{
    private readonly string _connectionString;

    public LvService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
    }

    public async Task<LvBogen?> GetBogenAsync(int bogenId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT BID, FKEIID, FKLID, Bezeichnung, Status
            FROM LVWBogen
            WHERE BID = @bogenId;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@bogenId", bogenId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(
            reader.GetInt32(0),
            ReadString(reader, 1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            ReadString(reader, 3),
            reader.IsDBNull(4) ? null : reader.GetInt32(4));
    }

    public async Task<IReadOnlyList<LvCategory>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT OID, Bezeichnung, Reihenfolge, Symbol
            FROM LVLOberbegriff
            ORDER BY Reihenfolge, OID;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<LvCategory>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new(
                reader.GetInt32(0),
                ReadString(reader, 1),
                reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                ReadString(reader, 3)));
        }

        return result;
    }

    public async Task<string?> CreateXmlAsync(int bogenId, CancellationToken cancellationToken)
    {
        var bogen = await GetBogenAsync(bogenId, cancellationToken);
        if (bogen is null) return null;

        var categories = await GetCategoriesAsync(cancellationToken);
        var structure = await GetStructureAsync(bogen.EIID, cancellationToken);
        var categoryElements = new List<XElement>();
        foreach (var category in categories)
        {
            var subcategoryElements = new List<XElement>();
            foreach (var group in structure
                .Where(item => item.CategoryId == category.Id)
                .GroupBy(item => new { item.Id, item.Name, item.Order })
                .OrderBy(group => group.Key.Order))
            {
                var itemElements = group.OrderBy(item => item.ItemOrder).Select(item =>
                    new XElement("Begriff",
                        new XElement("Name", item.ItemName),
                        new XElement("ID", item.ItemId),
                        new XElement("Typ", item.DisplayType),
                        new XElement("Wert", item.Value)));
                subcategoryElements.Add(new XElement("Unterkategorie",
                    new XElement("Name", group.Key.Name),
                    new XElement("ID", group.Key.Id),
                    new XElement("Begriffe", itemElements)));
            }

            categoryElements.Add(new XElement("Oberkategorie",
                new XElement("Name", category.Name),
                new XElement("ID", category.Id),
                new XElement("Unterkategorien", subcategoryElements)));
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("Bogen",
                new XElement("Name", bogen.Name),
                new XElement("BogenID", bogen.Id),
                new XElement("EinrichtungsID", bogen.EIID),
                new XElement("Status", bogen.Status),
                new XElement("Oberkategorien", categoryElements)));
        return document.ToString(SaveOptions.DisableFormatting);
    }

    public async Task<IReadOnlyList<LvSubcategory>> GetStructureAsync(string eiid, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT o.OID, o.Bezeichnung, o.Reihenfolge,
                   u.UID, u.Bezeichnung, u.Reihenfolge,
                   b.BFID, b.Bezeichnung, b.Reihenfolge, b.Darstellungstyp,
                   COALESCE(CONVERT(nvarchar(max), w.WertTXT),
                            CONVERT(nvarchar(max), w.WertMEMO),
                            CONVERT(nvarchar(max), w.WertDEC),
                            CONVERT(nvarchar(max), w.WertINT),
                            CONVERT(nvarchar(max), w.WertBOOL))
            FROM LVLOberbegriff o
            INNER JOIN LVLUnterbegriff u ON u.FKOID = o.OID
            INNER JOIN LVLBegriff b ON b.FKUID = u.UID
            LEFT JOIN LVWWert w ON w.fkBFID = b.BFID AND w.fkEIID = @eiid
            WHERE NOT EXISTS (
                SELECT 1
                FROM LVLIndexBegriffEinrichtungstyp ix
                INNER JOIN Einrichtungen ei ON ei.FKETID = ix.FKETID
                WHERE ix.FKBFID = b.BFID
                  AND ix.FKUID = u.UID
                  AND ei.EIID = @eiid)
            ORDER BY o.Reihenfolge, o.OID, u.Reihenfolge, u.UID, b.Reihenfolge, b.BFID;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eiid", eiid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<LvSubcategory>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new(
                reader.GetInt32(0), ReadString(reader, 1), reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                reader.GetInt32(3), ReadString(reader, 4), reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                reader.GetInt32(6), ReadString(reader, 7), reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                reader.IsDBNull(9) ? null : reader.GetInt32(9), ReadString(reader, 10)));
        }

        return result;
    }

    public async Task<IReadOnlyList<LvSelectionItem>> GetSelectionAsync(
        int? status,
        int? kreisId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ei.EIID, ei.Einrichtungsname, ei.Einrichtungskennzeichen,
                   ei.FKKRID, kr.Kreis, b.BID, b.Bezeichnung, b.Status
            FROM Einrichtungen ei
            INNER JOIN LVWBogen b ON b.FKEIID = ei.EIID
            LEFT JOIN Kreise kr ON kr.KRID = ei.FKKRID
            WHERE (@status IS NULL OR b.Status = @status)
              AND (@kreisId IS NULL OR ei.FKKRID = @kreisId)
            ORDER BY ei.Einrichtungsname, b.Bezeichnung
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@status", (object?)status ?? DBNull.Value);
        command.Parameters.AddWithValue("@kreisId", (object?)kreisId ?? DBNull.Value);
        command.Parameters.AddWithValue("@offset", Math.Max(0, page - 1) * pageSize);
        command.Parameters.AddWithValue("@pageSize", pageSize);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<LvSelectionItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new(
                ReadString(reader, 0), ReadString(reader, 1), ReadString(reader, 2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3), ReadString(reader, 4),
                reader.GetInt32(5), ReadString(reader, 6), reader.IsDBNull(7) ? null : reader.GetInt32(7)));
        }

        return result;
    }

    public async Task<int> CountSelectionAsync(int? status, int? kreisId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM Einrichtungen ei
            INNER JOIN LVWBogen b ON b.FKEIID = ei.EIID
            WHERE (@status IS NULL OR b.Status = @status)
              AND (@kreisId IS NULL OR ei.FKKRID = @kreisId);
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@status", (object?)status ?? DBNull.Value);
        command.Parameters.AddWithValue("@kreisId", (object?)kreisId ?? DBNull.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<LvDistrict>> GetDistrictsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT KRID, Kreis FROM Kreise ORDER BY Reihenfolge, KRID;";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<LvDistrict>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetInt32(0), ReadString(reader, 1)));
        return result;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string ReadString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
}

public sealed record LvBogen(int Id, string EIID, int? LayoutId, string Name, int? Status);
public sealed record LvCategory(int Id, string Name, int Order, string Symbol);
public sealed record LvSubcategory(int CategoryId, string CategoryName, int CategoryOrder, int Id, string Name, int Order, int ItemId, string ItemName, int ItemOrder, int? DisplayType, string Value);
public sealed record LvSelectionItem(string EIID, string FacilityName, string FacilityCode, int? DistrictId, string DistrictName, int BogenId, string BogenName, int? Status);
public sealed record LvDistrict(int Id, string Name);
