using Microsoft.Data.SqlClient;

namespace TOPqwWeb_Core.Services.Administration;

public sealed class DbCheckService
{
    private const string LegacyMinimumVersion = "0.1.84";
    private readonly string _connectionString;
    private readonly IConfiguration _configuration;

    public DbCheckService(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("TOPqwWeb") is { Length: > 0 } connectionString
            ? connectionString
            : throw new InvalidOperationException("ConnectionStrings:TOPqwWeb ist leer. Konfigurieren Sie den Wert über User Secrets oder die Umgebungsvariable ConnectionStrings__TOPqwWeb.");
    }

    public async Task<DbCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        var minimumVersion = _configuration["Database:MinimumVersion"];
        if (string.IsNullOrWhiteSpace(minimumVersion)) minimumVersion = LegacyMinimumVersion;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT TOP (1) Version FROM Version;", connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        var currentVersion = value is null || value == DBNull.Value ? string.Empty : Convert.ToString(value) ?? string.Empty;
        var current = ParseVersion(currentVersion);
        var minimum = ParseVersion(minimumVersion);
        return new(minimumVersion, currentVersion, current is not null && minimum is not null && current >= minimum, null);
    }

    public static DbCheckResult Failure(string minimumVersion, Exception exception)
        => new(minimumVersion, string.Empty, false, "Die Datenbankversion konnte nicht gelesen werden.");

    private static Version? ParseVersion(string value)
    {
        var normalized = value.Trim().Replace("PRE", string.Empty, StringComparison.OrdinalIgnoreCase);
        return Version.TryParse(normalized, out var version) ? version : null;
    }
}

public sealed record DbCheckResult(string MinimumVersion, string CurrentVersion, bool IsCurrent, string? ErrorMessage);
