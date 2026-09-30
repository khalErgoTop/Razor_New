using Microsoft.Data.SqlClient;

namespace TOPqwWeb_Core.Services.Administration;

public sealed class AssistOptionenService
{
    private readonly string _connectionString;

    public AssistOptionenService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
    }

    public async Task<AssistOptionenSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Einstellung, WertBOOL, WertTXT
            FROM TOPqw_Einstellungen
            WHERE Einstellung IN (@visibleSetting, @nameSetting)
              AND ISNULL(fkUSID, 0) = 0;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@visibleSetting", "SuchAssistent_anzeigen");
        command.Parameters.AddWithValue("@nameSetting", "SuchAssistent_Name");

        var visible = false;
        var name = "Assistierte Suche";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var setting = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            if (setting.Equals("SuchAssistent_anzeigen", StringComparison.OrdinalIgnoreCase))
                visible = !reader.IsDBNull(1) && reader.GetBoolean(1);
            else if (setting.Equals("SuchAssistent_Name", StringComparison.OrdinalIgnoreCase) && !reader.IsDBNull(2))
                name = reader.GetString(2);
        }

        return new AssistOptionenSettings(visible, string.IsNullOrWhiteSpace(name) ? "Assistierte Suche" : name);
    }

    public async Task SaveSettingsAsync(bool visible, string? name, CancellationToken cancellationToken)
    {
        const string sql = """
            EXEC dbo.SaveByTokenList @SemikolonSeperatedTokenList=@visibleTokens, @tableName=N'TOPqw_Einstellungen', @idName=N'Einstellung', @id=N'SuchAssistent_anzeigen';
            EXEC dbo.SaveByTokenList @SemikolonSeperatedTokenList=@nameTokens, @tableName=N'TOPqw_Einstellungen', @idName=N'Einstellung', @id=N'SuchAssistent_Name';
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@visibleTokens", $"WertBOOL={(visible ? 1 : 0)}");
        command.Parameters.AddWithValue("@nameTokens", $"WertTXT={(string.IsNullOrWhiteSpace(name) ? "Assistierte Suche" : name.Trim())}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssistServiceItem>> GetServicesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT [Etid],
                   CASE WHEN LTRIM(RTRIM(ISNULL([BF_Name], ''))) = '' THEN [Einrichtungstyp] ELSE [BF_Name] END AS [DisplayName]
            FROM [Einrichtungstypen]
            ORDER BY [DisplayName];
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var services = new List<AssistServiceItem>();
        while (await reader.ReadAsync(cancellationToken))
            services.Add(new AssistServiceItem(reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
        return services;
    }

    public async Task<AssistServiceDetails?> GetServiceAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT [BF_Name], [BF_Kategorie], [BF_Beschreibung], [Einrichtungstyp]
            FROM [Einrichtungstypen]
            WHERE [Etid] = @ID;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ID", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var name = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
        var fallbackName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
        return new AssistServiceDetails(
            id,
            string.IsNullOrWhiteSpace(name) ? fallbackName : name,
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.IsDBNull(2) ? string.Empty : reader.GetString(2));
    }

    public async Task UpdateServiceAsync(int id, string? name, string? category, string? description, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE [Einrichtungstypen]
            SET [BF_Name] = @Name,
                [BF_Kategorie] = @Kategorie,
                [BF_Beschreibung] = @Deskription
            WHERE [Etid] = @ID;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", name?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@Kategorie", category?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@Deskription", description?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@ID", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

public sealed record AssistOptionenSettings(bool Visible, string Name);
public sealed record AssistServiceItem(int Id, string DisplayName);
public sealed record AssistServiceDetails(int Id, string Name, string Category, string Description);
