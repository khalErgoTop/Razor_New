using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Services.Administration;

public sealed class BenutzerManagementService
{
    private const string PageRights = "9ecf8e04-838f-4f5e-bab8-245d0b0c2b73";
    private const string AdminRights = "2fe8d121-0b70-47bf-87f7-437e6462a9f2";
    private readonly string _connectionString;
    private readonly LegacyPermissionService _permissions;
    private readonly CurrentUserAccessor _currentUser;

    public BenutzerManagementService(IConfiguration configuration, LegacyPermissionService permissions, CurrentUserAccessor currentUser)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
        _permissions = permissions;
        _currentUser = currentUser;
    }

    public async Task<bool> CanAccessAsync(CancellationToken cancellationToken)
        => _currentUser.BEID is int beid &&
           await _permissions.HasPermissionAsync(beid, PageRights, cancellationToken);

    public async Task<bool> HasAdminRightsAsync(CancellationToken cancellationToken)
        => _currentUser.BEID is int beid &&
           await _permissions.HasPermissionAsync(beid, AdminRights, cancellationToken);

    public async Task<bool> CanCreateUsersAsync(CancellationToken cancellationToken)
    {
        var context = await GetCurrentUserContextAsync(cancellationToken);
        return context.UserType is 1 or 7 or 13;
    }

    public async Task<CreateUserResult> CreateUserAsync(NewUserInput input, CancellationToken cancellationToken)
    {
        var context = await GetCurrentUserContextAsync(cancellationToken);
        if (context.UserType is not (1 or 7 or 13))
            return new CreateUserResult(false, "Sie sind nicht berechtigt, neue Benutzer anzulegen.", null);

        var username = input.Username.Trim();
        if (string.IsNullOrWhiteSpace(username) || !Regex.IsMatch(username, "^[\\d\\w.\\-]+$"))
            return new CreateUserResult(false, "Der Benutzername enthält ungültige Zeichen.", null);

        var allowedTypes = context.UserTypes.Select(type => type.Value).ToHashSet();
        if (!allowedTypes.Contains(input.UserType))
            return new CreateUserResult(false, "Der ausgewählte Benutzertyp ist nicht zulässig.", null);

        await using var connection = await OpenAsync(cancellationToken);
        const string existsSql = "SELECT COUNT(BEID) FROM Benutzer WHERE Benutzername = @username";
        await using (var existsCommand = new SqlCommand(existsSql, connection))
        {
            existsCommand.Parameters.AddWithValue("@username", username);
            if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync(cancellationToken)) > 0)
                return new CreateUserResult(false, $"Der Benutzername '{username}' ist bereits vergeben.", null);
        }

        const string sql = """
            INSERT INTO Benutzer
                (Benutzername, Nachname, Vorname, Passwort, Email, Benutzertyp, TOPqwID, FKTRID,
                 Admin, Logincount, IsDeactivated, Kosoz)
            OUTPUT INSERTED.BEID
            VALUES
                (@username, @lastname, @firstname, @password, @email, @userType, @topqwId, @trid,
                 0, 0, 0, 0);
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@lastname", input.Lastname?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@firstname", input.Firstname?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@password", string.Empty);
        command.Parameters.AddWithValue("@email", input.Email?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@userType", input.UserType);
        command.Parameters.AddWithValue("@topqwId", DBNull.Value);
        command.Parameters.AddWithValue("@trid", context.UserType is 1 or 13 ? await GetCurrentTridAsync(connection, context.Beid, cancellationToken) : DBNull.Value);
        var newUserId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return new CreateUserResult(true, $"Der Benutzer '{username}' wurde angelegt.", newUserId);
    }

    public async Task<DeleteUserResult> DeleteUserAsync(int userId, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.BEID;
        if (currentUserId is null || userId <= 0 || userId == currentUserId.Value)
            return new DeleteUserResult(false, "Der aktuell angemeldete Benutzer kann nicht gelöscht werden.");

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var table in new[] { "IndexBenutzerEinrichtung", "IndexBenutzerKreise", "IndexBenutzerEinrichtungsTyp", "IndexBenutzerModule", "IndexBenutzerGruppe" })
            {
                await using var relationCommand = new SqlCommand($"DELETE FROM [{table}] WHERE FKBEID = @beid", connection, transaction);
                relationCommand.Parameters.AddWithValue("@beid", userId);
                await relationCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var userCommand = new SqlCommand("DELETE FROM [Benutzer] WHERE BEID = @beid", connection, transaction);
            userCommand.Parameters.AddWithValue("@beid", userId);
            if (await userCommand.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new DeleteUserResult(false, "Der Benutzer wurde nicht gefunden.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new DeleteUserResult(true, "Der Benutzer wurde gelöscht.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<object> GetCurrentTridAsync(SqlConnection connection, int beid, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT FKTRID FROM Benutzer WHERE BEID = @beid", connection);
        command.Parameters.AddWithValue("@beid", beid);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value ? DBNull.Value : value;
    }

    public async Task<UserContext> GetCurrentUserContextAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.BEID is not int beid) return new UserContext(0, 0, false, false, []);
        const string sql = "SELECT Benutzertyp, ISNULL(Admin, 0), ISNULL(Kosoz, 0) FROM Benutzer WHERE BEID = @beid";
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@beid", beid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new UserContext(beid, 0, false, false, []);
        var userType = Convert.ToInt32(reader.GetValue(0));
        var isAdmin = Convert.ToBoolean(reader.GetValue(1));
        var isKosoz = Convert.ToBoolean(reader.GetValue(2));
        return new UserContext(beid, userType, isAdmin, isKosoz, GetUserTypeOptions(userType));
    }

    public async Task<IReadOnlyList<UserListItem>> GetUsersAsync(int selectedUserType, string? constraint, CancellationToken cancellationToken)
    {
        var context = await GetCurrentUserContextAsync(cancellationToken);
        const string sql = """
            SELECT BEID, Benutzername, Nachname
            FROM Benutzer
            WHERE Benutzertyp = @userType
              AND (@constraint = N'' OR Benutzername LIKE @constraint)
              AND (@isAdmin = 1 OR Benutzertyp <> 7 OR BEID = @beid)
            ORDER BY Benutzername;
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@userType", selectedUserType);
        command.Parameters.AddWithValue("@constraint", string.IsNullOrWhiteSpace(constraint) ? string.Empty : $"%{constraint.Trim()}%");
        command.Parameters.AddWithValue("@isAdmin", context.IsAdmin);
        command.Parameters.AddWithValue("@beid", context.Beid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<UserListItem>();
        while (await reader.ReadAsync(cancellationToken))
            users.Add(new UserListItem(reader.GetInt32(0), Convert.ToString(reader.GetValue(1)) ?? string.Empty, reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2)) ?? string.Empty));
        return users;
    }

    public async Task<UserDetails?> GetUserDetailsAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT BEID, Benutzername, Vorname, Nachname, Email, Benutzertyp,
                   ISNULL(Admin, 0), ISNULL(Kosoz, 0), ISNULL(IsDeactivated, 0)
            FROM Benutzer WHERE BEID = @beid;
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@beid", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new UserDetails(
            reader.GetInt32(0), Convert.ToString(reader.GetValue(1)) ?? string.Empty,
            reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2)) ?? string.Empty,
            reader.IsDBNull(3) ? string.Empty : Convert.ToString(reader.GetValue(3)) ?? string.Empty,
            reader.IsDBNull(4) ? string.Empty : Convert.ToString(reader.GetValue(4)) ?? string.Empty,
            Convert.ToInt32(reader.GetValue(5)), Convert.ToBoolean(reader.GetValue(6)),
            Convert.ToBoolean(reader.GetValue(7)), Convert.ToBoolean(reader.GetValue(8)));
    }

    public async Task<IReadOnlyList<ModuleOption>> GetModulesAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT m.MID, m.Bezeichnung, CASE WHEN ibm.FKBEID IS NULL THEN 0 ELSE 1 END
            FROM Module m
            LEFT JOIN IndexBenutzerModule ibm ON ibm.ModuleID = m.MID AND ibm.FKBEID = @beid
            WHERE ISNULL(m.Sichtbar, 0) = 1
            ORDER BY m.Bezeichnung;
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@beid", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var modules = new List<ModuleOption>();
        while (await reader.ReadAsync(cancellationToken))
            modules.Add(new ModuleOption(reader.GetInt32(0), Convert.ToString(reader.GetValue(1)) ?? string.Empty, Convert.ToBoolean(reader.GetValue(2))));
        return modules;
    }

    public async Task<UserAssignments> GetAssignmentsAsync(UserDetails user, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var facilities = new List<AssignmentOption>();
        var districts = new List<AssignmentOption>();
        var facilityTypes = new List<AssignmentOption>();

        if (user.UserType is 1 or 2 or 3 or 14)
        {
            const string facilitiesSql = """
                SELECT DISTINCT e.EIID,
                       ISNULL(e.Einrichtungsname, '') + CASE WHEN ISNULL(e.Einrichtungskennzeichen, '') <> '' THEN ' - ' + e.Einrichtungskennzeichen ELSE '' END
                FROM Einrichtungen e
                WHERE e.FKTRID IN (SELECT FKTRID FROM Benutzer WHERE BEID = @beid)
                ORDER BY 2;
                """;
            await using var command = new SqlCommand(facilitiesSql, connection);
            command.Parameters.AddWithValue("@beid", user.Beid);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                facilities.Add(new AssignmentOption(Convert.ToString(reader.GetValue(0)) ?? string.Empty, Convert.ToString(reader.GetValue(1)) ?? string.Empty));
            await reader.CloseAsync();

            const string selectedFacilitiesSql = "SELECT FKEIID FROM IndexBenutzerEinrichtung WHERE FKBEID = @beid";
            await using var selectedCommand = new SqlCommand(selectedFacilitiesSql, connection);
            selectedCommand.Parameters.AddWithValue("@beid", user.Beid);
            await using var selectedReader = await selectedCommand.ExecuteReaderAsync(cancellationToken);
            var selectedFacilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await selectedReader.ReadAsync(cancellationToken))
                selectedFacilities.Add(Convert.ToString(selectedReader.GetValue(0)) ?? string.Empty);
            facilities = facilities.Select(item => item with { IsSelected = selectedFacilities.Contains(item.Value) }).ToList();
        }

        if (user.UserType is 7 or 22)
        {
            const string districtsSql = "SELECT KRID, Kreis FROM Kreise ORDER BY Kreis";
            await using var command = new SqlCommand(districtsSql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                districts.Add(new AssignmentOption(Convert.ToString(reader.GetValue(0)) ?? string.Empty, Convert.ToString(reader.GetValue(1)) ?? string.Empty));
            await reader.CloseAsync();

            const string selectedDistrictsSql = "SELECT FKKRID FROM IndexBenutzerKreise WHERE FKBEID = @beid";
            await using var selectedCommand = new SqlCommand(selectedDistrictsSql, connection);
            selectedCommand.Parameters.AddWithValue("@beid", user.Beid);
            await using var selectedReader = await selectedCommand.ExecuteReaderAsync(cancellationToken);
            var selectedDistricts = new HashSet<string>();
            while (await selectedReader.ReadAsync(cancellationToken))
                selectedDistricts.Add(Convert.ToString(selectedReader.GetValue(0)) ?? string.Empty);
            await selectedReader.CloseAsync();
            districts = districts.Select(item => item with { IsSelected = selectedDistricts.Contains(item.Value) }).ToList();

            const string facilityTypesSql = """
                SELECT ETID, Typkennzeichnung + ' - ' + Einrichtungstyp
                FROM Einrichtungstypen
                WHERE ISNULL(Typkennzeichnung, '') <> ''
                ORDER BY Typkennzeichnung;
                """;
            await using var typesCommand = new SqlCommand(facilityTypesSql, connection);
            await using var typesReader = await typesCommand.ExecuteReaderAsync(cancellationToken);
            while (await typesReader.ReadAsync(cancellationToken))
                facilityTypes.Add(new AssignmentOption(Convert.ToString(typesReader.GetValue(0)) ?? string.Empty, Convert.ToString(typesReader.GetValue(1)) ?? string.Empty));
            await typesReader.CloseAsync();

            const string selectedTypesSql = "SELECT FKETID FROM IndexBenutzerEinrichtungsTyp WHERE FKBEID = @beid";
            await using var selectedTypesCommand = new SqlCommand(selectedTypesSql, connection);
            selectedTypesCommand.Parameters.AddWithValue("@beid", user.Beid);
            await using var selectedTypesReader = await selectedTypesCommand.ExecuteReaderAsync(cancellationToken);
            var selectedTypes = new HashSet<string>();
            while (await selectedTypesReader.ReadAsync(cancellationToken))
                selectedTypes.Add(Convert.ToString(selectedTypesReader.GetValue(0)) ?? string.Empty);
            facilityTypes = facilityTypes.Select(item => item with { IsSelected = selectedTypes.Contains(item.Value) }).ToList();
        }

        return new UserAssignments(facilities, districts, facilityTypes,
            CanEditFacilities(user.UserType), user.UserType is 7 or 22);
    }

    public async Task<bool> SaveAssignmentsAsync(int userId, IEnumerable<string> facilityIds, IEnumerable<int> districtIds, IEnumerable<int> facilityTypeIds, bool canEditFacilities, bool canEditDistrictsAndTypes, CancellationToken cancellationToken)
    {
        var context = await GetCurrentUserContextAsync(cancellationToken);
        var target = await GetUserDetailsAsync(userId, cancellationToken);
        if (target is null || !CanEditTarget(context, target.UserType)) return false;

        canEditFacilities = CanEditFacilities(context, target.UserType);
        canEditDistrictsAndTypes = target.UserType is 7 or 22 && (context.IsAdmin || context.UserType == 7);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (canEditFacilities)
                await ReplaceAssignmentsAsync(connection, transaction, "IndexBenutzerEinrichtung", "FKEIID", userId, facilityIds, cancellationToken);
            if (canEditDistrictsAndTypes)
            {
                await ReplaceAssignmentsAsync(connection, transaction, "IndexBenutzerKreise", "FKKRID", userId, districtIds.Select(id => id.ToString()), cancellationToken);
                await ReplaceAssignmentsAsync(connection, transaction, "IndexBenutzerEinrichtungsTyp", "FKETID", userId, facilityTypeIds.Select(id => id.ToString()), cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SaveModulesAsync(int userId, IEnumerable<int> moduleIds, CancellationToken cancellationToken)
    {
        var context = await GetCurrentUserContextAsync(cancellationToken);
        var target = await GetUserDetailsAsync(userId, cancellationToken);
        if (target is null || !CanEditModules(context, target.UserType)) return;

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var delete = new SqlCommand("DELETE FROM IndexBenutzerModule WHERE FKBEID = @beid", connection, transaction))
            {
                delete.Parameters.AddWithValue("@beid", userId);
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }

            var allowedModuleIds = await GetAllowedModuleIdsAsync(connection, transaction, context, cancellationToken);
            foreach (var moduleId in moduleIds.Distinct().Where(allowedModuleIds.Contains))
            {
                await using var insert = new SqlCommand("INSERT INTO IndexBenutzerModule (FKBEID, ModuleID) VALUES (@beid, @moduleId)", connection, transaction);
                insert.Parameters.AddWithValue("@beid", userId);
                insert.Parameters.AddWithValue("@moduleId", moduleId);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task ReplaceAssignmentsAsync(SqlConnection connection, SqlTransaction transaction, string table, string valueColumn, int userId, IEnumerable<string> values, CancellationToken cancellationToken)
    {
        var allowedTables = new HashSet<string>(StringComparer.Ordinal) { "IndexBenutzerEinrichtung", "IndexBenutzerKreise", "IndexBenutzerEinrichtungsTyp" };
        if (!allowedTables.Contains(table)) throw new ArgumentException("Ungültige Zuordnungstabelle.", nameof(table));
        await using (var delete = new SqlCommand($"DELETE FROM [{table}] WHERE FKBEID = @beid", connection, transaction))
        {
            delete.Parameters.AddWithValue("@beid", userId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        var index = 0;
        foreach (var value in values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            await using var insert = new SqlCommand($"INSERT INTO [{table}] (FKBEID, [{valueColumn}]) VALUES (@beid, @value)", connection, transaction);
            insert.Parameters.AddWithValue("@beid", userId);
            insert.Parameters.AddWithValue("@value", valueColumn == "FKEIID" ? Guid.Parse(value) : int.Parse(value));
            await insert.ExecuteNonQueryAsync(cancellationToken);
            index++;
        }
    }

    private static bool CanEditFacilities(UserContext context, int targetUserType)
        => context.IsAdmin || context.UserType == 7 || context.UserType is 1 or 22 && targetUserType is 2 or 3;

    private static bool CanEditTarget(UserContext context, int targetUserType)
        => context.IsAdmin || context.UserType == 7 ||
           (context.UserType is 1 or 22 && targetUserType is 2 or 3) ||
           (context.UserType == 13 && targetUserType == 14) ||
           context.UserType == targetUserType;

    private async Task<HashSet<int>> GetAllowedModuleIdsAsync(SqlConnection connection, SqlTransaction transaction, UserContext context, CancellationToken cancellationToken)
    {
        const string sql = "SELECT MID FROM Module WHERE ISNULL(Sichtbar, 0) = 1";
        await using var command = new SqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ids = new HashSet<int>();
        while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetInt32(0));
        await reader.CloseAsync();
        return ids;
    }

    private static bool CanEditFacilities(int targetUserType)
        => targetUserType is 1 or 2 or 3 or 14;

    private static bool CanEditModules(UserContext context, int targetUserType)
        => context.IsAdmin || context.UserType == 7 ||
           ((context.UserType is 1 or 22) && (targetUserType is 2 or 3));

    public async Task<bool> SaveUserAsync(UserDetails user, CancellationToken cancellationToken)
    {
        var context = await GetCurrentUserContextAsync(cancellationToken);
        if (!CanEditTarget(context, user.UserType)) return false;

        var isSelf = context.Beid == user.Beid;
        var isAdmin = isSelf ? context.IsAdmin && user.IsAdmin : context.IsAdmin && user.IsAdmin;
        var isKosoz = isSelf ? context.IsKosoz && user.IsKosoz : context.IsAdmin && user.IsKosoz;
        const string sql = """
            UPDATE Benutzer SET Benutzername=@username, Vorname=@firstname, Nachname=@lastname,
                Email=@email, Admin=@admin, Kosoz=@kosoz, IsDeactivated=@deactivated
            WHERE BEID=@beid;
            """;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", user.Username);
        command.Parameters.AddWithValue("@firstname", user.Firstname);
        command.Parameters.AddWithValue("@lastname", user.Lastname);
        command.Parameters.AddWithValue("@email", user.Email);
        command.Parameters.AddWithValue("@admin", isAdmin);
        command.Parameters.AddWithValue("@kosoz", isKosoz);
        command.Parameters.AddWithValue("@deactivated", user.IsDeactivated);
        command.Parameters.AddWithValue("@beid", user.Beid);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static IReadOnlyList<UserTypeOption> GetUserTypeOptions(int currentType) => currentType switch
    {
        2 => [new("Einrichtung", 2)],
        7 => [new("Einrichtung", 2), new("Träger", 1), new("TOPQW", 7), new("Verbände", 13), new("Verbandsbenutzer", 14), new("Jugendamt", 22), new("Gruppe", 55)],
        1 => [new("Einrichtung", 2), new("Träger", 1)],
        13 => [new("Verbände", 13), new("Verbandsbenutzer", 14)],
        14 => [new("Verbandsbenutzer", 7)],
        _ => []
    };

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

public sealed record UserContext(int Beid, int UserType, bool IsAdmin, bool IsKosoz, IReadOnlyList<UserTypeOption> UserTypes);
public sealed record UserTypeOption(string Text, int Value);
public sealed record UserListItem(int Beid, string Username, string Lastname);
public sealed record UserDetails(int Beid, string Username, string Firstname, string Lastname, string Email, int UserType, bool IsAdmin, bool IsKosoz, bool IsDeactivated);
public sealed record ModuleOption(int Id, string Name, bool IsSelected);
public sealed record NewUserInput(string Username, string Firstname, string Lastname, string Email, int UserType);
public sealed record CreateUserResult(bool Success, string Message, int? UserId);
public sealed record DeleteUserResult(bool Success, string Message);
public sealed record AssignmentOption(string Value, string Text, bool IsSelected = false);
public sealed record UserAssignments(
    IReadOnlyList<AssignmentOption> Facilities,
    IReadOnlyList<AssignmentOption> Districts,
    IReadOnlyList<AssignmentOption> FacilityTypes,
    bool CanEditFacilities,
    bool CanEditDistrictsAndTypes);
