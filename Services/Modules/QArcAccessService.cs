using Microsoft.Data.SqlClient;
using TOPqwWeb_Core.Infrastructure.Security;

namespace TOPqwWeb_Core.Services.Modules;

public sealed class QArcAccessService
{
    private const string QarcAnzeige = "2542e895-8495-4ec6-94e0-f9c5c44c6351";
    private const string QarcBogenerstellen = "edf6b1a9-960f-4cad-88b1-e64779769352";

    private readonly string _connectionString;
    private readonly CurrentUserAccessor _user;
    private readonly LegacyPermissionService _permissions;

    public QArcAccessService(IConfiguration configuration, CurrentUserAccessor user, LegacyPermissionService permissions)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
        _user = user;
        _permissions = permissions;
    }

    public async Task<QArcAccess> GetAccessAsync(string eiid, string tablePrefix, int? status, CancellationToken cancellationToken)
    {
        if (_user.BEID is not int beid || beid <= 0 || string.IsNullOrWhiteSpace(eiid))
            return QArcAccess.Denied;

        var permissions = await _permissions.GetPermissionsAsync(beid, [QarcAnzeige, QarcBogenerstellen], cancellationToken);
        if (!permissions[QarcAnzeige]) return QArcAccess.Denied;

        if (!await IsAssignedAsync(beid, eiid, cancellationToken)) return QArcAccess.Denied;

        var canEdit = permissions[QarcBogenerstellen] && IsEditableStatus(status);
        return new(true, canEdit, tablePrefix.Equals("BE", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEditableStatus(int? status) => status is null or 0 or 1 or 2 or 5;

    private async Task<bool> IsAssignedAsync(int beid, string eiid, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM Einrichtungen ei
            WHERE ei.EIID = @eiid
              AND (
                   @isAdmin = 1
                   OR EXISTS (SELECT 1 FROM IndexBenutzerEinrichtung ibe WHERE ibe.FKBEID = @beid AND ibe.FKEIID = ei.EIID)
                   OR EXISTS (SELECT 1 FROM Benutzer b WHERE b.BEID = @beid AND b.FKTRID = ei.FKTRID)
                   OR EXISTS (SELECT 1 FROM IndexBenutzerKreise ibk WHERE ibk.FKBEID = @beid AND ibk.FKKRID = ei.FKKRID)
              );
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eiid", eiid);
        command.Parameters.AddWithValue("@beid", beid);
        command.Parameters.AddWithValue("@isAdmin", _user.IsAdmin ? 1 : 0);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }
}

public sealed record QArcAccess(bool CanView, bool CanEdit, bool IsBusinessPermit)
{
    public static QArcAccess Denied { get; } = new(false, false, false);
}
