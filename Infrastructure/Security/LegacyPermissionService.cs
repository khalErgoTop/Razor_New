using Microsoft.Data.SqlClient;

namespace TOPqwWeb_Core.Infrastructure.Security;

public sealed class LegacyPermissionService
{
    private const string AdminRechte = "ea0adb75-cea8-47a9-943c-f6a34f0a943e";
    private readonly string _connectionString;
    private readonly ILogger<LegacyPermissionService> _logger;

    public LegacyPermissionService(IConfiguration configuration, ILogger<LegacyPermissionService> logger)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(int beid, string permission, CancellationToken cancellationToken)
    {
        if (beid <= 0 || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("GetUserPermissions", connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@userId", beid);
        command.Parameters.AddWithValue("@withGroups", true);
        command.Parameters.AddWithValue("@uid", beid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var allowed = false;
        var denied = false;
        var adminAllowed = false;
        var rowCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rowCount++;
            var permissionId = Convert.ToString(reader["BRID"]);
            var designation = Convert.ToString(reader["Bezeichnung"]);
            var isAllowed = !reader.IsDBNull(reader.GetOrdinal("Erlaubt")) && Convert.ToBoolean(reader["Erlaubt"]);
            var isDenied = !reader.IsDBNull(reader.GetOrdinal("Verweigert")) && Convert.ToBoolean(reader["Verweigert"]);
            if (string.Equals(permissionId, AdminRechte, StringComparison.OrdinalIgnoreCase) && isAllowed)
            {
                adminAllowed = true;
            }

            if (string.Equals(permissionId, permission, StringComparison.OrdinalIgnoreCase))
            {
                allowed |= isAllowed;
                denied |= isDenied;
            }
        }

        var result = !denied && (allowed || adminAllowed);
        _logger.LogInformation("Legacy permission check: BEID={BEID}, Permission={Permission}, Rows={Rows}, Allowed={Allowed}, Denied={Denied}, AdminAllowed={AdminAllowed}, Result={Result}", beid, permission, rowCount, allowed, denied, adminAllowed, result);
        return result;
    }

    public async Task<IReadOnlyDictionary<string, bool>> GetPermissionsAsync(int beid, IEnumerable<string> permissions, CancellationToken cancellationToken)
    {
        var requested = permissions.Where(permission => !string.IsNullOrWhiteSpace(permission)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var result = requested.ToDictionary(permission => permission, _ => false, StringComparer.OrdinalIgnoreCase);
        if (beid <= 0 || requested.Length == 0)
        {
            return result;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("GetUserPermissions", connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@userId", beid);
        command.Parameters.AddWithValue("@withGroups", true);
        command.Parameters.AddWithValue("@uid", beid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var denied = requested.ToDictionary(permission => permission, _ => false, StringComparer.OrdinalIgnoreCase);
        var adminAllowed = false;
        var rowCount = 0;
        var seenDesignations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            rowCount++;
            var permissionId = Convert.ToString(reader["BRID"]);
            var designation = Convert.ToString(reader["Bezeichnung"]);
            if (!string.IsNullOrWhiteSpace(designation)) seenDesignations.Add($"{permissionId}:{designation}");
            if (permissionId is null || (!result.ContainsKey(permissionId) && !string.Equals(permissionId, AdminRechte, StringComparison.OrdinalIgnoreCase))) continue;
            var isAllowed = !reader.IsDBNull(reader.GetOrdinal("Erlaubt")) && Convert.ToBoolean(reader["Erlaubt"]);
            var isDenied = !reader.IsDBNull(reader.GetOrdinal("Verweigert")) && Convert.ToBoolean(reader["Verweigert"]);
            if (string.Equals(permissionId, AdminRechte, StringComparison.OrdinalIgnoreCase) && isAllowed)
            {
                adminAllowed = true;
                continue;
            }

            if (result.ContainsKey(permissionId))
            {
                result[permissionId] |= isAllowed;
                denied[permissionId] |= isDenied;
            }
        }

        foreach (var permission in requested)
        {
            result[permission] = !denied[permission] && (result[permission] || adminAllowed);
        }

        _logger.LogInformation("Legacy permission batch: BEID={BEID}, Rows={Rows}, Requested={Requested}, Seen={Seen}, Results={Results}", beid, rowCount, string.Join(",", requested), string.Join(",", seenDesignations), string.Join(",", result.Select(pair => $"{pair.Key}={pair.Value}")));

        return result;
    }
}
