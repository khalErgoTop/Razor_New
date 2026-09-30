using Microsoft.Data.SqlClient;

namespace TOPqwWeb_Core.Services.Common;

public sealed class LeittextService
{
    private readonly string _connectionString;

    public LeittextService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
    }

    public async Task<IReadOnlyList<LeittextPage>> GetPagesAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT DISTINCT Seite FROM Leittexte WHERE Seite IS NOT NULL AND Seite <> '' ORDER BY Seite;";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var pages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var value = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(value))
                pages[value] = value;
        }

        foreach (var page in FixedPages)
            pages.TryAdd(page.Value, page.Text);

        return pages.OrderBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .Select(item => new LeittextPage(item.Key, item.Value))
            .ToList();
    }

    public async Task<LeittextEntry> GetAsync(string page, bool helpText, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Leittext, LeittextVisible, Hilfetext, HilfeTextVisible, Titel
            FROM Leittexte WHERE Seite = @page;
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@page", page);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new LeittextEntry(page, helpText, "", "", false);

        return helpText
            ? new LeittextEntry(page, true, GetString(reader, 2), GetString(reader, 4), GetBoolean(reader, 3))
            : new LeittextEntry(page, false, GetString(reader, 0), "", GetBoolean(reader, 1));
    }

    public async Task SaveAsync(LeittextEntry entry, CancellationToken cancellationToken)
    {
        const string sql = """
            IF EXISTS (SELECT 1 FROM Leittexte WHERE Seite = @page)
                UPDATE Leittexte SET Leittext = CASE WHEN @help = 0 THEN @text ELSE Leittext END,
                    LeittextVisible = CASE WHEN @help = 0 THEN @visible ELSE LeittextVisible END,
                    Hilfetext = CASE WHEN @help = 1 THEN @text ELSE Hilfetext END,
                    HilfeTextVisible = CASE WHEN @help = 1 THEN @visible ELSE HilfeTextVisible END,
                    Titel = CASE WHEN @help = 1 THEN @title ELSE Titel END WHERE Seite = @page;
            ELSE
                INSERT INTO Leittexte (Seite, Leittext, LeittextVisible, Hilfetext, HilfeTextVisible, Titel)
                VALUES (@page, CASE WHEN @help = 0 THEN @text ELSE '' END, CASE WHEN @help = 0 THEN @visible ELSE 0 END,
                    CASE WHEN @help = 1 THEN @text ELSE '' END, CASE WHEN @help = 1 THEN @visible ELSE 0 END,
                    CASE WHEN @help = 1 THEN @title ELSE '' END);
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@page", entry.Page);
        command.Parameters.AddWithValue("@help", entry.HelpText);
        command.Parameters.AddWithValue("@text", entry.Text ?? string.Empty);
        command.Parameters.AddWithValue("@visible", entry.Visible);
        command.Parameters.AddWithValue("@title", entry.Title ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string GetString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    private static bool GetBoolean(SqlDataReader reader, int ordinal) => !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);

    private static readonly LeittextPage[] FixedPages =
    [
        new("Impressum", "Impressum"), new("Datenschutz", "Datenschutz"), new("Summary", "Leichte Sprache"),
        new("Accessibility", "Barrierefreiheit"), new("TermsLV", "Rechtlicher Hinweis: LV"),
        new("TermsKalkulation", "Rechtlicher Hinweis: Kalkulation"), new("TermsQM", "Rechtlicher Hinweis: Qualitätsmanagment"),
        new("TermsBE", "Rechtlicher Hinweis: BE"), new("HinweisVerguetung", "Hinweis: Vergütung"),
        new("AttachmentDisclaimer", "Hinweis: Dateianhänge"), new("SearchAssistentWelcomeMsg", "Hinweis: SuchAssistent")
    ];
}

public sealed record LeittextPage(string Value, string Text);
public sealed record LeittextEntry(string Page, bool HelpText, string Text, string Title, bool Visible);
