using Microsoft.Data.SqlClient;

namespace TOPqwWeb_Core.Services.Administration;

public sealed class FehlermeldungenService
{
    private readonly string _connectionString;

    public FehlermeldungenService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
    }

    public async Task<IReadOnlyList<Fehlermeldung>> GetErrorsAsync(string? constraintType, CancellationToken cancellationToken)
    {
        var eventFilter = constraintType switch
        {
            "2" => "[Event] IN (90, 92)",
            "3" => "[Event] IN (1, 2, 3)",
            _ => "[Event] = 90"
        };

        var sql = $"""
            SELECT TOP 10 LogID, Datum, Username AS Benutzer, Info AS Beschreibung,
                   UserIp AS Rechner, [Event] AS Nummer
            FROM Errorlog
            WHERE {eventFilter}
            ORDER BY Datum DESC, LogID DESC;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var errors = new List<Fehlermeldung>();
        while (await reader.ReadAsync(cancellationToken))
        {
            errors.Add(new Fehlermeldung(
                reader.GetInt32(reader.GetOrdinal("LogID")),
                reader.IsDBNull(reader.GetOrdinal("Datum")) ? null : reader.GetDateTime(reader.GetOrdinal("Datum")),
                ReadString(reader, "Benutzer"), ReadString(reader, "Beschreibung"),
                ReadString(reader, "Rechner"), ReadString(reader, "Nummer")));
        }

        return errors;
    }

    private static string? ReadString(SqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column)) ? null : Convert.ToString(reader[column]);
}

public sealed record Fehlermeldung(
    int LogId, DateTime? Datum, string? Benutzer, string? Beschreibung,
    string? Rechner, string? Nummer);
