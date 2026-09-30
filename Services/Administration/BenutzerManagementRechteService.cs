using Microsoft.Data.SqlClient;
using System.Data;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Services.Administration;

public sealed class BenutzerManagementRechteService
{
    private const string AdminRights = "2fe8d121-0b70-47bf-87f7-437e6462a9f2";
    private readonly string _connectionString;
    private readonly LegacyPermissionService _permissions;
    private readonly CurrentUserAccessor _currentUser;

    public BenutzerManagementRechteService(IConfiguration configuration, LegacyPermissionService permissions, CurrentUserAccessor currentUser)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb") ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
        _permissions = permissions;
        _currentUser = currentUser;
    }

    public async Task<bool> HasAdminRightsAsync(CancellationToken cancellationToken)
        => _currentUser.BEID is int beid && await _permissions.HasPermissionAsync(beid, AdminRights, cancellationToken);

    public async Task<IReadOnlyList<UserGroupItem>> GetAvailableGroupsAsync(CancellationToken cancellationToken)
        => await ReadGroupsAsync("SELECT BEID, Benutzername FROM Benutzer WHERE Benutzertyp = @userType ORDER BY Benutzername", null, cancellationToken);

    public async Task<IReadOnlyList<UserGroupItem>> GetGroupsByUserAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT BEID, Benutzername FROM Benutzer WHERE BEID = @userId UNION SELECT BEID, Benutzername FROM Benutzer INNER JOIN IndexBenutzerGruppe ON BEID = FKG WHERE FKBEID = @userId ORDER BY Benutzername";
        return await ReadGroupsAsync(sql, userId, cancellationToken);
    }

    public async Task<IReadOnlyList<UserRightItem>> GetRightsByUserAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT br.BRID, br.Bezeichnung, br.Beschreibung, ISNULL(ibr.Erlaubt, 0), ISNULL(ibr.Verweigert, 0) FROM BenutzerRechte AS br LEFT JOIN IndexBenutzerRechteBenutzer AS ibr ON br.BRID = ibr.FKBRID AND ibr.FKBEID = @userId ORDER BY br.Bezeichnung";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@userId", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rights = new List<UserRightItem>();
        while (await reader.ReadAsync(cancellationToken))
            rights.Add(new UserRightItem(reader.GetGuid(0), Convert.ToString(reader.GetValue(1)) ?? string.Empty, reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2)) ?? string.Empty, Convert.ToBoolean(reader.GetValue(3)), Convert.ToBoolean(reader.GetValue(4)), true));
        return rights;
    }

    public async Task<bool> SaveUserRightAsync(int userId, Guid rightId, bool allowed, bool denied, CancellationToken cancellationToken)
    {
        if (userId <= 0 || rightId == Guid.Empty || !await HasAdminRightsAsync(cancellationToken)) return false;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand("[dbo].[SaveUserRight]", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.AddWithValue("@BRID", rightId);
        command.Parameters.AddWithValue("@BEID", userId);
        command.Parameters.AddWithValue("@Erlaubt", allowed);
        command.Parameters.AddWithValue("@Verweigert", denied);
        command.Parameters.AddWithValue("@uid", userId);
        await command.ExecuteScalarAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AddUserGroupAsync(int userId, int groupId, CancellationToken cancellationToken)
    {
        if (!await CanChangeGroupAsync(userId, groupId, cancellationToken)) return false;
        await ExecuteGroupProcedureAsync("[dbo].[SaveUserGroup]", userId, groupId, cancellationToken);
        return true;
    }

    public async Task<bool> RemoveUserGroupAsync(int userId, int groupId, CancellationToken cancellationToken)
    {
        if (!await CanChangeGroupAsync(userId, groupId, cancellationToken) || userId == groupId) return false;
        await ExecuteGroupProcedureAsync("[dbo].[DeleteUserGroup]", userId, groupId, cancellationToken);
        return true;
    }

    private async Task<bool> CanChangeGroupAsync(int userId, int groupId, CancellationToken cancellationToken)
    {
        if (userId <= 0 || groupId <= 0 || !await HasAdminRightsAsync(cancellationToken)) return false;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT COUNT(*) FROM Benutzer WHERE BEID = @userId OR (BEID = @groupId AND Benutzertyp = @groupType)", connection);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@groupId", groupId);
        command.Parameters.AddWithValue("@groupType", CurrentUserAccessor.LegacyUserTypes.Gruppe);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 2;
    }

    private async Task ExecuteGroupProcedureAsync(string procedure, int userId, int groupId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(procedure, connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@groupId", groupId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<UserGroupItem>> ReadGroupsAsync(string sql, int? userId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue(userId.HasValue ? "@userId" : "@userType", userId ?? CurrentUserAccessor.LegacyUserTypes.Gruppe);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var groups = new List<UserGroupItem>();
        while (await reader.ReadAsync(cancellationToken)) groups.Add(new UserGroupItem(reader.GetInt32(0), Convert.ToString(reader.GetValue(1)) ?? string.Empty));
        return groups;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

public sealed record UserGroupItem(int Beid, string Username);
public sealed record UserRightItem(Guid Brid, string Bezeichnung, string Beschreibung, bool Erlaubt, bool Verweigert, bool RechtAendern);
