using Microsoft.Data.SqlClient;
using System.Data;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Legacy.Adapters;

public sealed class StammdatenKostenAdapter
{
    private readonly string _connectionString;

    public StammdatenKostenAdapter(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TOPqm")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqm muss konfiguriert werden.");
    }

    public async Task<int?> GetFacilityTypeAsync(string eiid, CancellationToken cancellationToken)
    {
        const string sql = "SELECT FKETID FROM Einrichtungen WHERE EIID = @EIID;";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EIID", eiid);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value ? null : Convert.ToInt32(value);
    }

    public async Task<IReadOnlyList<StammdatenKostenTimeFrame>> GetTimeFramesAsync(string eiid, CancellationToken cancellationToken)
    {
        const string sql = "SELECT HBGWZID, GueltigAb, GueltigBis, CONVERT(varchar(MAX), GueltigAb, 104) + ' - ' + CONVERT(varchar(MAX), GueltigBis, 104) AS VonBis FROM HilfebedarfsWerteZeitraumWeb WHERE FKEIID = @EIID ORDER BY GueltigAb, HBGWZID;";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EIID", eiid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<StammdatenKostenTimeFrame>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new(
                reader.GetInt32(0),
                ReadDate(reader, 1),
                ReadDate(reader, 2),
                ReadString(reader, 3)));
        }
        return result;
    }

    public async Task<IReadOnlyList<StammdatenKostenRow>> GetRowsAsync(string eiid, int hbgwzid, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT hbg.HBGID, hbg.Bezeichnung, hbgw.HBGWWID, hbgw.MP, hbgw.GP, hbgw.IB, hbgw.FB,
                   hbgw.VereinbartAm, hbgw.UnterschriebenAm
            FROM dbo.Hilfebedarfsgruppe hbg
            INNER JOIN HilfebedarfsgruppeZeitraum hbgz ON hbgz.HBGZID = hbg.FKHBGZID
            INNER JOIN Einrichtungen ei ON ei.FKETID = hbg.FKETID AND ei.EIID = @EIID
            LEFT JOIN HilfebedarfsWerteWeb hbgw ON hbgw.FKHBGID = hbg.HBGID AND hbgw.FKHBGWZID = @HBGWZID
            WHERE ISNULL(hbgz.GueltigBis, CONVERT(date, GETDATE() + 1)) > GETDATE()
            ORDER BY hbg.HBGID;
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EIID", eiid);
        command.Parameters.AddWithValue("@HBGWZID", hbgwzid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<StammdatenKostenRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new(
                reader.GetInt32(0),
                ReadString(reader, 1),
                ReadNullableInt(reader, 2),
                ReadNullableDecimal(reader, 3),
                ReadNullableDecimal(reader, 4),
                ReadNullableDecimal(reader, 5),
                ReadNullableDecimal(reader, 6),
                ReadDate(reader, 7),
                ReadDate(reader, 8)));
        }
        return result;
    }

    public async Task<bool> TimeFrameBelongsToFacilityAsync(string eiid, int hbgwzid, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(1) FROM HilfebedarfsWerteZeitraumWeb WHERE HBGWZID = @HBGWZID AND FKEIID = @EIID;";
        return await ExecuteCountAsync(sql, eiid, hbgwzid, cancellationToken) > 0;
    }

    public async Task<int> SaveTimeFrameAsync(string eiid, int etid, int hbgwzid, DateTime dateFrom, DateTime dateTo, string username, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SaveTimeFrameStammdatenKosten", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@HBGWZID", hbgwzid);
        command.Parameters.AddWithValue("@DateFrom", dateFrom);
        command.Parameters.AddWithValue("@DateTo", dateTo);
        command.Parameters.AddWithValue("@EIID", eiid);
        command.Parameters.AddWithValue("@ETID", etid);
        command.Parameters.AddWithValue("@GeaendertVon", username);
        command.Parameters.AddWithValue("@ErstelltVon", username);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<int> DeleteTimeFrameAsync(string eiid, int hbgwzid, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        const string deleteRows = "DELETE FROM HilfebedarfsWerteWeb WHERE FKHBGWZID = @HBGWZID AND EXISTS (SELECT 1 FROM HilfebedarfsWerteZeitraumWeb WHERE HBGWZID = @HBGWZID AND FKEIID = @EIID);";
        await using (var rows = new SqlCommand(deleteRows, connection, transaction))
        {
            rows.Parameters.AddWithValue("@HBGWZID", hbgwzid);
            rows.Parameters.AddWithValue("@EIID", eiid);
            await rows.ExecuteNonQueryAsync(cancellationToken);
        }
        const string deleteFrame = "DELETE FROM HilfebedarfsWerteZeitraumWeb WHERE HBGWZID = @HBGWZID AND FKEIID = @EIID;";
        await using var frame = new SqlCommand(deleteFrame, connection, transaction);
        frame.Parameters.AddWithValue("@HBGWZID", hbgwzid);
        frame.Parameters.AddWithValue("@EIID", eiid);
        var affected = await frame.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return affected;
    }

    public async Task<bool> HbgBelongsToFacilityTypeAsync(string eiid, int hbgid, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(1) FROM Hilfebedarfsgruppe hbg INNER JOIN Einrichtungen ei ON ei.FKETID = hbg.FKETID WHERE ei.EIID = @EIID AND hbg.HBGID = @HBGID;";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EIID", eiid);
        command.Parameters.AddWithValue("@HBGID", hbgid);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<bool> SaveRowAsync(string eiid, int hbgwzid, StammdatenKostenRow row, string username, CancellationToken cancellationToken)
    {
        if (row.MP is null && row.GP is null && row.IB is null && row.FB is null)
        {
            return false;
        }

        const string sql = """
            IF (ISNULL(@HBGWZID, 0) <> 0)
            BEGIN
                IF (ISNULL(@HBGWWID, 0) = 0)
                BEGIN
                    INSERT INTO HilfebedarfsWerteWeb (FKHBGWZID, FKHBGID, MP, GP, IB, FB, ErstelltAm, ErstelltVon, VereinbartAm, UnterschriebenAm)
                    SELECT @HBGWZID, @HBGID, @MP, @GP, @IB, @FB, GETDATE(), @ErstelltVon, @VereinbartAm, @UnterschriebenAm
                    WHERE EXISTS (SELECT 1 FROM HilfebedarfsWerteZeitraumWeb WHERE HBGWZID = @HBGWZID AND FKEIID = @EIID);
                    SELECT CAST(SCOPE_IDENTITY() AS int);
                END
                ELSE
                BEGIN
                    UPDATE w SET FKHBGWZID=@HBGWZID, FKHBGID=@HBGID, MP=@MP, GP=@GP, IB=@IB, FB=@FB,
                        GeaendertAm=GETDATE(), GeaendertVon=@GeaendertVon, UnterschriebenAm=@UnterschriebenAm, VereinbartAm=@VereinbartAm
                    FROM HilfebedarfsWerteWeb w
                    WHERE HBGWWID=@HBGWWID AND FKHBGWZID=@HBGWZID
                      AND EXISTS (SELECT 1 FROM HilfebedarfsWerteZeitraumWeb WHERE HBGWZID=@HBGWZID AND FKEIID=@EIID);
                    SELECT CASE WHEN @@ROWCOUNT > 0 THEN @HBGWWID ELSE NULL END;
                END
            END
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EIID", eiid);
        command.Parameters.AddWithValue("@HBGWZID", hbgwzid);
        command.Parameters.AddWithValue("@HBGWWID", (object?)row.HBGWWID ?? DBNull.Value);
        command.Parameters.AddWithValue("@HBGID", row.HBGID);
        AddDecimal(command, "@MP", row.MP); AddDecimal(command, "@GP", row.GP); AddDecimal(command, "@IB", row.IB); AddDecimal(command, "@FB", row.FB);
        command.Parameters.AddWithValue("@VereinbartAm", (object?)row.VereinbartAm ?? DBNull.Value);
        command.Parameters.AddWithValue("@UnterschriebenAm", (object?)row.UnterschriebenAm ?? DBNull.Value);
        command.Parameters.AddWithValue("@ErstelltVon", username); command.Parameters.AddWithValue("@GeaendertVon", username);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && result != DBNull.Value;
    }//balla3

    private async Task<int> ExecuteCountAsync(string sql, string eiid, int hbgwzid, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EIID", eiid); command.Parameters.AddWithValue("@HBGWZID", hbgwzid);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddDecimal(SqlCommand command, string name, decimal? value) => command.Parameters.AddWithValue(name, (object?)value ?? DBNull.Value);
    private static string ReadString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    private static int? ReadNullableInt(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    private static decimal? ReadNullableDecimal(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    private static DateTime? ReadDate(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
}
