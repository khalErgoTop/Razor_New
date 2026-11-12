using Microsoft.Data.SqlClient;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Services.Modules;

public sealed class KalkulationService
{
    private const int KalkulationModule = 8;
    private readonly string _connectionString;
    private readonly CurrentUserAccessor _user;

    public KalkulationService(IConfiguration configuration, CurrentUserAccessor user)
    {
        _connectionString = configuration.GetConnectionString("TOPqm")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqm muss konfiguriert werden.");
        _user = user;
    }

    public async Task<bool> HasAccessAsync(CancellationToken cancellationToken)
    {
        if (_user.BEID is not int beid || beid <= 0) return false;
        const string sql = "SELECT COUNT(1) FROM BenutzerModule WHERE FKBEID = @beid AND FKModul = @module AND ISNULL(Aktiv, 1) <> 0;";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@beid", beid);
        command.Parameters.AddWithValue("@module", KalkulationModule);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<IReadOnlyList<KalkulationDistrict>> GetDistrictsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT Kreis, Kreisschluessel FROM Kreise ORDER BY Reihenfolge;";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<KalkulationDistrict> { new("Alle Kreise", "_00") };
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(ReadString(reader, 0), ReadString(reader, 1)));
        return result;
    }

    public async Task<IReadOnlyList<KalkulationFacility>> GetFacilitiesAsync(string? district, int? status, CancellationToken cancellationToken)
    {
        if (_user.BEID is not int beid || beid <= 0) return [];
        const string sql = """
            SELECT DISTINCT ei.EIID, ei.Einrichtungsname, ei.Einrichtungskennzeichen, ei.Status, k.Kreisschluessel
            FROM Einrichtungen ei
            INNER JOIN Benutzer b ON b.BEID = @beid
            LEFT JOIN Kreise k ON k.KRID = ei.FKKRID
            WHERE ei.Status <> 9
              AND EXISTS (SELECT 1 FROM IndexEinrichtungstypKalkulationLayout kl WHERE kl.FKETID = ei.FKETID)
              AND (@status IS NULL OR ei.Status = @status)
              AND (@district IS NULL OR @district = '_00' OR k.Kreisschluessel = @district)
              AND (
                  EXISTS (SELECT 1 FROM IndexBenutzerEinrichtung ibe WHERE ibe.FKBEID = @beid AND ibe.FKEIID = ei.EIID)
                  OR EXISTS (SELECT 1 FROM Einrichtungen e2 WHERE e2.EIID = ei.EIID AND e2.FKTRID IN (SELECT FKTRID FROM Benutzer WHERE BEID = @beid))
                  OR EXISTS (SELECT 1 FROM IndexBenutzerKreise ibk WHERE ibk.FKBEID = @beid AND ibk.FKkRID = ei.FKKRID)
              )
            ORDER BY ei.Einrichtungskennzeichen;
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@beid", beid);
        command.Parameters.AddWithValue("@district", string.IsNullOrWhiteSpace(district) ? "_00" : district);
        command.Parameters.AddWithValue("@status", status.HasValue ? status.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<KalkulationFacility>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(ReadString(reader, 0), ReadString(reader, 1), ReadString(reader, 2), reader.IsDBNull(3) ? null : reader.GetInt32(3), ReadString(reader, 4)));
        return result;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string ReadString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
}

public sealed record KalkulationDistrict(string Name, string Key);
public sealed record KalkulationFacility(string EIID, string Name, string Indicator, int? Status, string DistrictKey);
