using Microsoft.Data.SqlClient;

namespace TOPqwWeb_Core.Services.Administration;

public sealed class SystemNewsService
{
    private readonly string _connectionString;

    public SystemNewsService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
    }

    public async Task<bool> NewsExistsAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(1) FROM InformationNews WHERE INID = @id;";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<IReadOnlyList<SystemNewsItem>> GetNewsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.INID, n.InfoLabel, n.InfoText, COUNT(i.FKBEID) AS UserCount
            FROM InformationNews n LEFT JOIN IndexBenutzerInfoNews i ON n.INID = i.FKINID
            GROUP BY n.INID, n.InfoLabel, n.InfoText ORDER BY n.InfoLabel;
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<SystemNewsItem>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetInt32(0), ReadString(reader, 1), ReadString(reader, 2), reader.GetInt32(3)));
        return result;
    }

    public async Task<int> SaveNewsAsync(int id, string? label, string? text, CancellationToken cancellationToken)
    {
        const string sql = """
            IF @id > 0
            BEGIN
                UPDATE InformationNews SET InfoLabel = @label, InfoText = @text WHERE INID = @id;
                SELECT @id;
            END
            ELSE
            BEGIN
                INSERT INTO InformationNews (InfoLabel, InfoText) VALUES (@label, @text);
                SELECT CONVERT(int, SCOPE_IDENTITY());
            END
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@label", label?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@text", text ?? string.Empty);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task DeleteNewsAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM IndexBenutzerInfoNews WHERE FKINID = @id; DELETE FROM InformationNews WHERE INID = @id;";
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task<IReadOnlyList<SystemNewsPerson>> GetSelectedPersonsAsync(int newsId, CancellationToken cancellationToken)
        => GetPersonsAsync("INNER JOIN IndexBenutzerInfoNews i ON b.BEID = i.FKBEID WHERE i.FKINID = @newsId", newsId, cancellationToken);

    public Task<IReadOnlyList<SystemNewsPerson>> GetAvailablePersonsAsync(int newsId, CancellationToken cancellationToken)
        => GetPersonsAsync("WHERE NOT EXISTS (SELECT 1 FROM IndexBenutzerInfoNews i WHERE i.FKBEID = b.BEID AND i.FKINID = @newsId)", newsId, cancellationToken);

    public Task AddPersonsAsync(int newsId, IEnumerable<int> personIds, CancellationToken cancellationToken)
        => ChangePersonsAsync(newsId, personIds, false, cancellationToken);

    public Task RemovePersonsAsync(int newsId, IEnumerable<int> personIds, CancellationToken cancellationToken)
        => ChangePersonsAsync(newsId, personIds, true, cancellationToken);

    private async Task<IReadOnlyList<SystemNewsPerson>> GetPersonsAsync(string clause, int newsId, CancellationToken cancellationToken)
    {
        var sql = $"SELECT b.BEID, b.Benutzername, b.Benutzertyp FROM Benutzer b {clause} ORDER BY b.Benutzername;";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@newsId", newsId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<SystemNewsPerson>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetInt32(0), ReadString(reader, 1), ReadString(reader, 2)));
        return result;
    }

    private async Task ChangePersonsAsync(int newsId, IEnumerable<int> personIds, bool remove, CancellationToken cancellationToken)
    {
        var ids = personIds.Distinct().ToArray();
        if (ids.Length == 0) return;
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var validateNews = new SqlCommand("SELECT COUNT(1) FROM InformationNews WHERE INID = @newsId", connection, transaction))
            {
                validateNews.Parameters.AddWithValue("@newsId", newsId);
                if (Convert.ToInt32(await validateNews.ExecuteScalarAsync(cancellationToken)) == 0)
                {
                    throw new KeyNotFoundException("Die ausgewählte Nachricht wurde nicht gefunden.");
                }
            }

            foreach (var personId in ids)
            {
                await using var command = new SqlCommand(remove
                    ? "DELETE FROM IndexBenutzerInfoNews WHERE FKINID = @newsId AND FKBEID = @personId"
                    : "IF EXISTS (SELECT 1 FROM Benutzer WHERE BEID = @personId) AND NOT EXISTS (SELECT 1 FROM IndexBenutzerInfoNews WHERE FKINID = @newsId AND FKBEID = @personId) INSERT INTO IndexBenutzerInfoNews (FKBEID, FKINID) VALUES (@personId, @newsId)", connection, transaction);
                command.Parameters.AddWithValue("@newsId", newsId);
                command.Parameters.AddWithValue("@personId", personId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string ReadString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
}

public sealed record SystemNewsItem(int Id, string Label, string Text, int UserCount);
public sealed record SystemNewsPerson(int Id, string Username, string UserType);
