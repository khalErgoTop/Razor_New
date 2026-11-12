using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;

namespace TOPqwWeb_Core.Legacy.Adapters;

public sealed class KalkulationAdapter
{
    private readonly string _connectionString;

    public KalkulationAdapter(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TOPqm")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqm muss konfiguriert werden.");
    }

    public async Task<KalkulationPageData?> GetPageAsync(string eiid, CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT TOP (1) k.KAID, k.FKEIID, k.FKKLID, k.Status, k.Bezeichnung,
                   k.GueltigVon, k.GueltigBis, k.Platzzahl, k.Auslastung,
                   ei.Einrichtungsname, ei.Einrichtungskennzeichen, kl.Bezeichnung AS LayoutBezeichnung,
                   kl.MitFahrten, kl.QuellZellePlatzzahl, kl.QuellZelleAuslastung
            FROM Kalkulation k
            INNER JOIN Einrichtungen ei ON ei.EIID = k.FKEIID
            LEFT JOIN KalkulationLayout kl ON kl.KLID = k.FKKLID
            WHERE k.FKEIID = @eiid
            ORDER BY k.KAID DESC;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(headerSql, connection);
        command.Parameters.AddWithValue("@eiid", eiid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var page = new KalkulationPageData(
            reader.GetInt32(0), ReadString(reader, 1), ReadNullableInt(reader, 2), ReadNullableInt(reader, 3) ?? 0,
            ReadString(reader, 4), ReadNullableDate(reader, 5), ReadNullableDate(reader, 6),
            ReadNullableDouble(reader, 7), ReadNullableDouble(reader, 8), ReadString(reader, 9),
            ReadString(reader, 10), ReadString(reader, 11), ReadBoolean(reader, 12),
            !string.IsNullOrWhiteSpace(ReadString(reader, 13)), !string.IsNullOrWhiteSpace(ReadString(reader, 14)), []);
        await reader.CloseAsync();

        var posts = await GetPostsAsync(connection, page.Id, cancellationToken);
        var totalInputAngebotET = posts.Where(post => post.Input).Sum(post => post.AngebotET ?? 0d);
        var summary = posts
            .Where(post => post.Input)
            .SelectMany(post => (post.Percentages ?? new Dictionary<string, double>()).Select(percent => new
            {
                percent.Key,
                Value = (post.AngebotET ?? 0d) * percent.Value / 100d
            }))
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Value));
        return page with
        {
            Posts = posts,
            TotalInputAngebotET = totalInputAngebotET,
            Summary = summary
        };
    }

    private static async Task<IReadOnlyList<KalkulationNavigationItem>> GetPostsAsync(SqlConnection connection, int kaid, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT kp.KPID, kp.Reihenfolge, kp.Oberkategorie, kp.Kostenart, kp.Ordnungszahl,
                   kp.Kostenbestandteil, kp.Input, kp.LockedValue, kp.SubTotal,
                   kw.KWID, kw.Bemerkung, kw.AngebotET, kw.AngebotKT, kw.Abschluss,
                   kw.Tagessatz, kw.MPBetrag, kw.MPGesamt, kw.GPBetrag, kw.GPGesamt,
                   kw.IBBetrag, kw.IBGesamt, kw.SKBetrag, kw.SKGesamt, kw.PKBetrag, kw.PKGesamt,
                   kw.EPBetrag, kw.EPGesamt,
                   kpp.MPProzent, kpp.GPProzent, kpp.IBProzent, kpp.PKProzent,
                   kpp.SKProzent, kpp.EPProzent, kpp.KDUProzent, kpp.FLProzent
            FROM Kalkulation k
            INNER JOIN Einrichtungen e ON e.EIID = k.FKEIID
            INNER JOIN KalkulationLayout kl ON ISNULL(k.FKKLID, 1) = kl.KLID
            INNER JOIN IndexEinrichtungstypKalkulationLayout ietl
                ON ietl.FKKLID = kl.KLID AND ietl.FKETID = e.FKETID
            INNER JOIN KalkulationPosten kp ON kp.FKKLID = kl.KLID
            LEFT JOIN KalkulationWert kw ON kw.FKKPID = kp.KPID AND kw.FKKAID = k.KAID
            LEFT JOIN KalkulationProzentverteilung kpv ON kpv.PVID = k.FKPVID AND kpv.FKKLID = kl.KLID
            LEFT JOIN KalkulationPostenProzente kpp ON kpp.KPID = kp.KPID AND kpp.FKPVID = kpv.PVID
            WHERE k.KAID = @kaid
            ORDER BY kp.Reihenfolge, kp.KPID;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@kaid", kaid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<KalkulationNavigationItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var kostenart = ReadString(reader, 3);
            var lockedValue = ReadBoolean(reader, 7);
            var subTotal = ReadBoolean(reader, 8);
            var percentages = new Dictionary<string, double>();
            AddPercentage(percentages, "MP", ReadNullableDouble(reader, 27));
            AddPercentage(percentages, "GP", ReadNullableDouble(reader, 28));
            AddPercentage(percentages, "IB", ReadNullableDouble(reader, 29));
            AddPercentage(percentages, "PK", ReadNullableDouble(reader, 30));
            AddPercentage(percentages, "SK", ReadNullableDouble(reader, 31));
            AddPercentage(percentages, "EP", ReadNullableDouble(reader, 32));
            AddPercentage(percentages, "KDU", ReadNullableDouble(reader, 33));
            AddPercentage(percentages, "FL", ReadNullableDouble(reader, 34));
            result.Add(new(
                ReadNullableInt(reader, 0) ?? 0,
                ReadNullableInt(reader, 2) ?? 0,
                kostenart,
                ReadString(reader, 4),
                ReadString(reader, 5),
                ReadBoolean(reader, 6),
                !string.IsNullOrWhiteSpace(kostenart) && !lockedValue && !subTotal,
                ReadNullableDouble(reader, 11),
                ReadNullableDouble(reader, 12),
                ReadNullableDouble(reader, 13),
                ReadNullableDouble(reader, 14),
                ReadNullableDouble(reader, 15),
                ReadNullableDouble(reader, 16),
                ReadNullableDouble(reader, 17),
                ReadNullableDouble(reader, 18),
                ReadNullableDouble(reader, 19),
                ReadNullableDouble(reader, 20),
                ReadNullableDouble(reader, 21),
                ReadNullableDouble(reader, 22),
                ReadNullableDouble(reader, 23),
                ReadNullableDouble(reader, 24),
                ReadNullableDouble(reader, 25),
                 ReadNullableDouble(reader, 26),
                 percentages));
        }
        return result;
    }

    private static void AddPercentage(IDictionary<string, double> percentages, string key, double? value)
    {
        if (value is > 0) percentages[key] = value.Value;
    }

    public async Task<bool> UpdateAngebotETAsync(
        string eiid, int kalkulationId, int postenId, double angebotEt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE kw
            SET kw.AngebotET = @angebotEt
            FROM KalkulationWert kw
            INNER JOIN KalkulationPosten kp ON kp.KPID = kw.FKKPID
            INNER JOIN Kalkulation k ON k.KAID = kw.FKKAID
            WHERE kw.FKKAID = @kalkulationId
              AND kw.FKKPID = @postenId
              AND k.FKEIID = @eiid
              AND k.Status = 0
              AND kp.Input = 1
              AND kp.Oberkategorie <> 1
              AND kp.Kostenbestandteil NOT IN (N'Abschreibungen', N'Instandhaltung Gebäude');
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@angebotEt", angebotEt);
        command.Parameters.AddWithValue("@kalkulationId", kalkulationId);
        command.Parameters.AddWithValue("@postenId", postenId);
        command.Parameters.AddWithValue("@eiid", eiid);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<string?> CreateXmlAsync(string eiid, CancellationToken cancellationToken)
    {
        var page = await GetPageAsync(eiid, cancellationToken);
        if (page is null) return null;

        var document = new XDocument(
            new XElement("Kalkulation",
                new XAttribute("KAID", page.Id),
                new XAttribute("EIID", page.EIID),
                new XElement("Einrichtung",
                    new XElement("Kennzeichen", page.Einrichtungskennzeichen),
                    new XElement("Name", page.Einrichtungsname)),
                new XElement("Stammdaten",
                    new XElement("Bezeichnung", page.Bezeichnung),
                    new XElement("Status", page.Status),
                    new XElement("GueltigVon", page.GueltigVon),
                    new XElement("GueltigBis", page.GueltigBis)),
                new XElement("Posten", page.Posts.Select(post =>
                    new XElement("Posten",
                        new XAttribute("KPID", post.Id),
                        new XElement("Oberkategorie", post.Oberkategorie),
                        new XElement("Kostenart", post.Kostenart),
                        new XElement("Ordnungszahl", post.Ordnungszahl),
                        new XElement("Kostenbestandteil", post.Kostenbestandteil),
                        new XElement("AngebotET", post.AngebotET),
                        new XElement("AngebotKT", post.AngebotKT),
                        new XElement("Abschluss", post.Abschluss),
                        new XElement("Tagessatz", post.Tagessatz))))));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string ReadString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    private static int? ReadNullableInt(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    private static double? ReadNullableDouble(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToDouble(reader.GetValue(ordinal));
    private static DateTime? ReadNullableDate(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    private static bool ReadBoolean(SqlDataReader reader, int ordinal) => !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal));
}

public sealed record KalkulationPageData(
    int Id, string EIID, int? LayoutId, int Status, string Bezeichnung,
    DateTime? GueltigVon, DateTime? GueltigBis, double? Platzzahl, double? Auslastung,
    string Einrichtungsname, string Einrichtungskennzeichen, string LayoutBezeichnung,
    bool MitFahrten, bool HasPlatzzahl, bool HasAuslastung,
    IReadOnlyList<KalkulationNavigationItem> Posts,
    double TotalInputAngebotET = 0,
    IReadOnlyDictionary<string, double>? Summary = null);

public sealed record KalkulationNavigationItem(
    int Id, int Oberkategorie, string Kostenart, string Ordnungszahl,
    string Kostenbestandteil, bool Input, bool IsParentCategory,
    double? AngebotET, double? AngebotKT, double? Abschluss, double? Tagessatz,
    double? MPBetrag, double? MPGesamt, double? GPBetrag, double? GPGesamt,
    double? IBBetrag, double? IBGesamt, double? SKBetrag, double? SKGesamt,
    double? PKBetrag, double? PKGesamt, double? EPBetrag, double? EPGesamt,
    IReadOnlyDictionary<string, double>? Percentages = null);
