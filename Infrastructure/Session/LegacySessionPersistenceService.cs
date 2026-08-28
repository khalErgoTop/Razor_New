using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace TOPqwWeb_Core.Infrastructure.Session;

public sealed class LegacySessionPersistenceService
{
    public const string SessionLoadedKey = "TOPqwWeb.SessionLoaded";
    public const string SkipSaveKey = "TOPqwWeb.SkipSessionSave";

    private readonly string _connectionString;

    public LegacySessionPersistenceService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb") is { Length: > 0 } connectionString
            ? connectionString
            : throw new InvalidOperationException("Die ConnectionStrings:TOPqwWeb-Konfiguration fehlt.");
    }

    public async Task LoadAsync(ISession session, CancellationToken cancellationToken)
    {
        if (session.TryGetValue(SessionLoadedKey, out _))
        {
            return;
        }

        await session.LoadAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT [Session] FROM Sessions WHERE SessID = @sessionId";
        command.Parameters.Add("@sessionId", System.Data.SqlDbType.VarChar, 100).Value = session.Id;

        var payload = await command.ExecuteScalarAsync(cancellationToken);
        if (payload is string json && !string.IsNullOrWhiteSpace(json))
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (values is not null)
            {
                foreach (var value in values)
                {
                    try
                    {
                        session.Set(value.Key, Convert.FromBase64String(value.Value));
                    }
                    catch (FormatException)
                    {
                        session.SetString(value.Key, value.Value);
                    }
                }
            }
        }

        session.SetString(SessionLoadedKey, bool.TrueString);
    }

    public async Task SaveAsync(ISession session, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in session.Keys)
        {
            if (key is SessionLoadedKey or SkipSaveKey)
            {
                continue;
            }

            if (session.TryGetValue(key, out var value))
            {
                values[key] = Convert.ToBase64String(value);
            }
        }

        var json = JsonSerializer.Serialize(values);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Sessions SET [Session] = @session
            WHERE SessID = @sessionId;
            IF @@ROWCOUNT = 0
                INSERT INTO Sessions (SessID, [Session]) VALUES (@sessionId, @session);
            """;
        command.Parameters.Add("@sessionId", System.Data.SqlDbType.VarChar, 100).Value = session.Id;
        command.Parameters.Add("@session", System.Data.SqlDbType.NVarChar, -1).Value = json;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(ISession session, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Sessions WHERE SessID = @sessionId";
        command.Parameters.Add("@sessionId", System.Data.SqlDbType.VarChar, 100).Value = session.Id;
        await command.ExecuteNonQueryAsync(cancellationToken);
        session.SetString(SkipSaveKey, bool.TrueString);
    }
}
