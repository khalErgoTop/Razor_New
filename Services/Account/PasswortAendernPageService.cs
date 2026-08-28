using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Services.Account;

public sealed class PasswortAendernPageService
{
    private readonly AuthAdapter _adapter;
    private readonly IConfiguration _configuration;
    private readonly TOPqwWeb_Core.Infrastructure.Security.LegacyPermissionService _permissions;

    public PasswortAendernPageService(
        AuthAdapter adapter,
        IConfiguration configuration,
        TOPqwWeb_Core.Infrastructure.Security.LegacyPermissionService permissions)
    {
        _adapter = adapter;
        _configuration = configuration;
        _permissions = permissions;
    }

    public Task<bool> HasUserManagementAdminRightsAsync(int userId, CancellationToken cancellationToken) =>
        _permissions.HasPermissionAsync(userId, "2fe8d121-0b70-47bf-87f7-437e6462a9f2", cancellationToken);

    public async Task<string?> GetUsernameAsync(int userId, CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new Microsoft.Data.SqlClient.SqlCommand(
            "SELECT Benutzername FROM Benutzer WHERE BEID = @beid", connection);
        command.Parameters.AddWithValue("@beid", userId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    public Task<PasswordPolicy> GetPasswordPolicyAsync(CancellationToken cancellationToken) =>
        _adapter.GetPasswordPolicyAsync(cancellationToken);

    public async Task<PasswordChangeResult> ChangeOwnPasswordAsync(
        int userId, string oldPassword, string newPassword, string confirmation, CancellationToken cancellationToken)
    {
        var policy = await _adapter.GetPasswordPolicyAsync(cancellationToken);
        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
            return new PasswordChangeResult(false, "Die Passwörter stimmen nicht überein.", policy);
        if (!policy.IsValid(newPassword))
            return new PasswordChangeResult(false, "Das neue Passwort entspricht nicht der Passwortvorgabe.", policy);
        if (!await _adapter.VerifyCurrentPasswordAsync(userId, oldPassword, cancellationToken))
            return new PasswordChangeResult(false, "Die Eingabe Ihres alten Passwortes stimmt nicht mit dem hinterlegten überein.", policy);

        await _adapter.SaveNewPasswordAsync(userId, newPassword, cancellationToken);
        return new PasswordChangeResult(true, "Ihr Passwort wurde geändert.", policy);
    }

    public async Task<PasswordChangeResult> ChangeUserPasswordAsync(
        int userId, string newPassword, string confirmation, CancellationToken cancellationToken)
    {
        var policy = await _adapter.GetPasswordPolicyAsync(cancellationToken);
        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
            return new PasswordChangeResult(false, "Die Passwörter stimmen nicht überein.", policy);
        if (!policy.IsValid(newPassword))
            return new PasswordChangeResult(false, "Das neue Passwort entspricht nicht der Passwortvorgabe.", policy);

        await _adapter.SaveNewPasswordAsync(userId, newPassword, cancellationToken);
        return new PasswordChangeResult(true, "Das Passwort wurde geändert.", policy);
    }
}

public sealed record PasswordChangeResult(bool Succeeded, string Message, PasswordPolicy Policy);
