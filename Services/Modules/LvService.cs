using Microsoft.Data.SqlClient;

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
