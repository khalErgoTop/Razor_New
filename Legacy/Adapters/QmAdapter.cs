using Microsoft.Data.SqlClient;
using System.Text;
using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Legacy.Adapters
{
    public sealed class QmAdapter
    {
        private readonly string _connectionString;
        private readonly CurrentUserAccessor _user;

        public QmAdapter(IConfiguration configuration, CurrentUserAccessor user)
        {
            _connectionString = configuration.GetConnectionString("TOPqwWeb")
                ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
            _user = user;
        }

        public async Task<IReadOnlyList<QmOberkategorieResponse>> GetQmBogenStrukturAsync(int QLID, int ETID, int QMBogenID, CancellationToken cancellationToken)
        {
            const string sql = "SELECT ok.OKID, ISNULL(ilo.Position, ok.Position) AS OKPosition, ok.Bezeichnung, ok.BezeichnungKurz, uk.UKID, ISNULL(iou.Position, uk.Position) AS UKPosition, uk.Bezeichnung, uk.BezeichnungKurz, f.QFID, iuf.Position, f.FrageKurz, f.FrageLang, f.Hilfetext, f.Format, iuf.Mussfeld, iuf.Eintragstyp FROM [dbo].[QMLV3Oberkategorie] ok INNER JOIN [dbo].[QMLV3IndexLayoutOberkategorie] ilo ON ilo.FKOKID = ok.OKID AND ilo.FKQLID = @QLID INNER JOIN [dbo].[QMLV3IndexOberUnterkategorie] iou ON iou.FKQLID = @QLID AND iou.FKOKID = ok.OKID INNER JOIN [dbo].[QMLV3Unterkategorie] uk ON uk.UKID = iou.FKUKID INNER JOIN [dbo].[QMLV3IndexEinrichtungstypUnterkategorie] ieu ON ieu.FKQLID = @QLID AND ieu.FKOKID = ok.OKID AND ieu.FKUKID = uk.UKID AND ieu.FKETID = @ETID INNER JOIN [dbo].[QMLV3IndexUnterkategorieFrage] iuf ON iuf.FKQLID = @QLID AND iuf.FKOKID = ok.OKID AND iuf.FKUKID = uk.UKID INNER JOIN [dbo].[QMLV3Frage] f ON f.QFID = iuf.FKQFID WHERE ISNULL(ok.isDeleted, 0) = 0 AND ISNULL(ilo.isDeleted, 0) = 0 AND ISNULL(iou.isDeleted, 0) = 0 AND ISNULL(uk.isDeleted, 0) = 0 AND ISNULL(ieu.isDeleted, 0) = 0 AND ISNULL(iuf.isDeleted, 0) = 0 AND ISNULL(f.isDeleted, 0) = 0 ORDER BY OKPosition, UKPosition, iuf.Position";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@QLID", QLID);
            command.Parameters.AddWithValue("@ETID", ETID);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var categories = new Dictionary<int, (int Position, string Bezeichnung, string? Kurz, Dictionary<int, (int Position, string Bezeichnung, string? Kurz, List<QmFrageResponse> Fragen)> Unterkategorien)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var okid = ReadInt(reader, 0);
                var ukid = ReadInt(reader, 4);
                if (!categories.TryGetValue(okid, out var oberkategorie))
                {
                    oberkategorie = (ReadInt(reader, 1), ReadString(reader, 2), ReadNullableString(reader, 3), new Dictionary<int, (int, string, string?, List<QmFrageResponse>)>());
                    categories.Add(okid, oberkategorie);
                }

                if (!oberkategorie.Unterkategorien.TryGetValue(ukid, out var unterkategorie))
                {
                    unterkategorie = (ReadInt(reader, 5), ReadString(reader, 6), ReadNullableString(reader, 7), new List<QmFrageResponse>());
                    oberkategorie.Unterkategorien.Add(ukid, unterkategorie);
                }

                unterkategorie.Fragen.Add(new QmFrageResponse(
                    ReadInt(reader, 8), ReadInt(reader, 9), ReadString(reader, 10), ReadNullableString(reader, 11),
                    ReadNullableString(reader, 12), ReadNullableString(reader, 13), ReadBool(reader, 14), ReadInt(reader, 15)));
            }

            var frageWerte = await GetFrageWerteAsync(QMBogenID, cancellationToken);
            return categories.OrderBy(x => x.Value.Position).Select(x => new QmOberkategorieResponse(
                x.Key, x.Value.Position, x.Value.Bezeichnung, x.Value.Kurz,
                x.Value.Unterkategorien.OrderBy(y => y.Value.Position).Select(y => new QmUnterkategorieResponse(
                    y.Key, y.Value.Position, y.Value.Bezeichnung, y.Value.Kurz,
                    y.Value.Fragen.OrderBy(f => f.Position).Select(f => frageWerte.TryGetValue(f.ID, out var wert)
                        ? f with { FrageWert = wert } : f).ToArray())).ToArray())).ToArray();
        }

        public async Task<int?> GetFacilityTypeAsync(string EIID, CancellationToken cancellationToken)
        {
            const string sql = "SELECT FKETID FROM [dbo].[Einrichtungen] WHERE EIID = @EIID";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@EIID", EIID);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null || value == DBNull.Value ? null : Convert.ToInt32(value);
        }

        public async Task<QmFrageWertResponse?> GetFrageWertAsync(int QMBogenID, int QIID, CancellationToken cancellationToken)
        {
            const string sql = "SELECT QWID, FKQBID, FKQIID, WertINT, WertREAL, WertBOOL, WertTXT, WertMEMO FROM [dbo].[QMWV3Wert] WHERE FKQBID = @QMBogenID AND FKQIID = @QIID";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@QMBogenID", QMBogenID);
            command.Parameters.AddWithValue("@QIID", QIID);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new QmFrageWertResponse(
                ReadInt(reader, 0), ReadInt(reader, 1), ReadInt(reader, 2), ReadInt(reader, 3),
                ReadDouble(reader, 4), ReadBool(reader, 5), ReadString(reader, 6), ReadString(reader, 7));
        }

        public async Task<IReadOnlyDictionary<int, QmFrageWertResponse>> GetFrageWerteAsync(int QMBogenID, CancellationToken cancellationToken)
        {
            const string sql = "SELECT QWID, FKQBID, FKQIID, WertINT, WertREAL, WertBOOL, WertTXT, WertMEMO FROM [dbo].[QMWV3Wert] WHERE FKQBID = @QMBogenID";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@QMBogenID", QMBogenID);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = new Dictionary<int, QmFrageWertResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var wert = new QmFrageWertResponse(
                    ReadInt(reader, 0), ReadInt(reader, 1), ReadInt(reader, 2), ReadInt(reader, 3),
                    ReadDouble(reader, 4), ReadBool(reader, 5), ReadString(reader, 6), ReadString(reader, 7));
                result[wert.FKQIID] = wert;
            }

            return result;
        }

        public async Task<IReadOnlyList<QmSelectableBogenResponse>> GetSelectableBoegenAsync(string EIID, int ETID, int iQMBogenstatus, CancellationToken cancellationToken)
        {
            const string sql = "SELECT b.QBID, b.FKEIID, b.FKQLID, l.Bezeichnung, l.Layoutjahr, b.Bezeichnung, b.Status, b.GeaendertAm FROM [dbo].[QMWV3Bogen] b INNER JOIN [dbo].[QMLV3Layout] l ON l.QLID = b.FKQLID INNER JOIN [dbo].[QMLV3IndexEinrichtungstypOberkategorie] i ON i.FKQLID = l.QLID WHERE b.FKEIID = @EIID AND i.FKETID = @ETID AND l.Aktiv = 1 AND ISNULL(l.IstTraegerBogen, 0) = 0 AND (@QMBogenstatus = -1 OR b.Status = @QMBogenstatus) ORDER BY l.Layoutjahr DESC, l.Bezeichnung, b.Bezeichnung";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@EIID", EIID);
            command.Parameters.AddWithValue("@ETID", ETID);
            command.Parameters.AddWithValue("@QMBogenstatus", iQMBogenstatus);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = new List<QmSelectableBogenResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new QmSelectableBogenResponse(
                    ReadInt(reader, 0), ReadString(reader, 1), ReadInt(reader, 2), ReadString(reader, 3),
                    ReadInt(reader, 4), ReadString(reader, 5), ReadInt(reader, 6), ReadNullableDate(reader, 7)));
            }

            return result;
        }

        public async Task<IReadOnlyList<QmFacilityResponse>> GetFacilitiesAvailableAsync(int iQMBogenstatus, CancellationToken cancellationToken)
        {
            if (_user.BEID is not int lBEID || lBEID <= 0) return [];

            var szSQL = new StringBuilder("DECLARE @LayoutJahr int, @KRIDS int; SELECT @LayoutJahr = [QMLV3Layout].[Layoutjahr] FROM [QMLV3Layout] WHERE [QMLV3Layout].[Aktiv] = 1; SELECT @KRIDS = COUNT(FKkRID) FROM IndexBenutzerKreise WHERE FKBEID = @BEID; SELECT DISTINCT Einrichtungsname,EIID,Einrichtungskennzeichen,Einrichtungen.Aktenzeichen, Einrichtungen.FKTRID, Einrichtungen.[Status], Einrichtungen.FKETID, Einrichtungen.FKKRID, Einrichtungen.GeschlossenAm, Einrichtungen.GeoeffnetSeit, Einrichtungen.GeaendertAM, Einrichtungen.GeaendertVon, Einrichtungen.SyncAm, Einrichtungen.Platzzahl, ElektronischerLeistungsnachweis, EinrichtungsBeschreibung FROM Einrichtungen ");
            if (!_user.IsAdmin)
            {
                switch (_user.UserType)
                {
                    case CurrentUserAccessor.LegacyUserTypes.TOPqw when _user.IsKosoz:
                    case CurrentUserAccessor.LegacyUserTypes.Jugendamt:
                        szSQL.Append("INNER JOIN IndexBenutzerEinrichtungstyp ON Einrichtungen.FKETID = IndexBenutzerEinrichtungstyp.FKETID INNER JOIN Benutzer ON IndexBenutzerEinrichtungstyp.FKBEID=Benutzer.BEID ");
                        break;
                    case CurrentUserAccessor.LegacyUserTypes.Traeger:
                        szSQL.Append("INNER JOIN Benutzer ON Einrichtungen.FKTRID = Benutzer.FKTRID ");
                        break;
                    case CurrentUserAccessor.LegacyUserTypes.Verband:
                        szSQL.Append("INNER JOIN Traeger ON Einrichtungen.FKTRID = Traeger.TRID INNER JOIN Benutzer ON Traeger.FKVEID = Benutzer.TOPqwID AND ISNULL(Traeger.VerbandszugriffErlauben,1) = 1 ");
                        break;
                    case CurrentUserAccessor.LegacyUserTypes.Einrichtungsverwalter:
                    case CurrentUserAccessor.LegacyUserTypes.Einrichtung:
                    case CurrentUserAccessor.LegacyUserTypes.VerbandUser:
                        szSQL.Append("INNER JOIN IndexBenutzerEinrichtung ON Einrichtungen.EIID=IndexBenutzerEinrichtung.FKEIID INNER JOIN Benutzer ON IndexBenutzerEinrichtung.FKBEID=Benutzer.BEID ");
                        break;
                }

                if (_user.UserType != CurrentUserAccessor.LegacyUserTypes.TOPqw)
                {
                    szSQL.Append(" AND BEID = @BEID ");
                }
            }
            else
            {
                szSQL.Append("LEFT JOIN IndexBenutzerEinrichtung ON Einrichtungen.EIID=IndexBenutzerEinrichtung.FKEIID LEFT JOIN Benutzer ON IndexBenutzerEinrichtung.FKBEID=Benutzer.BEID ");
            }

            szSQL.Append("LEFT JOIN [QMWV3Bogen] ON [Einrichtungen].[EIID] = [QMWV3Bogen].[FKEIID] LEFT JOIN [QMLV3Layout] ON [QLID] = [FKQLID] LEFT JOIN [Kreise] ON [Kreise].[KRID] = [Einrichtungen].[FKKRID] ");
            if (!_user.IsAdmin)
            {
                szSQL.Append("WHERE ((@KRIDS > 0 AND [Kreise].KRID IN (SELECT FKkRID FROM IndexBenutzerKreise WHERE FKBEID = @BEID) OR @KRIDS = 0)) ");
            }
            else
            {
                szSQL.Append("WHERE 1 = 1 ");
            }

            szSQL.Append("AND [Einrichtungen].[Status]<> 9 ");
            szSQL.Append("AND ISNULL(Year([Einrichtungen].[GeschlossenAm]),@LayoutJahr) >= @LayoutJahr ");
            szSQL.Append("AND ISNULL(Year([Einrichtungen].[GeoeffnetSeit]),@LayoutJahr) <= @LayoutJahr ");
            if (iQMBogenstatus > -1)
            {
                if (iQMBogenstatus == 1)
                {
                    szSQL.Append("AND ([QMLV3Layout].[Aktiv] = 1 OR [QMLV3Layout].[Aktiv] IS NULL) AND ([QMWV3Bogen].[Status] = @QMBogenstatus OR [QMWV3Bogen].[Status] IS NULL) ");
                }
                else
                {
                    szSQL.Append("AND [QMLV3Layout].[Aktiv] = 1 AND [QMWV3Bogen].[Status] = @QMBogenstatus ");
                }
            }

            szSQL.Append("ORDER BY Einrichtungskennzeichen");
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(szSQL.ToString(), connection);
            command.Parameters.AddWithValue("@BEID", lBEID);
            command.Parameters.AddWithValue("@QMBogenstatus", iQMBogenstatus);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = new List<QmFacilityResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new QmFacilityResponse(
                    ReadString(reader, 1), ReadString(reader, 0), ReadString(reader, 2), ReadNullableString(reader, 3),
                    ReadNullableString(reader, 4), ReadInt(reader, 5), ReadNullableInt(reader, 6), ReadNullableInt(reader, 7),
                    ReadNullableDate(reader, 8), ReadNullableDate(reader, 9), ReadNullableInt(reader, 13), ReadNullableString(reader, 15),
                    ReadNullableString(reader, 11), ReadNullableDate(reader, 10), ReadNullableDate(reader, 12), ReadBool(reader, 14)));
            }
            return result;
        }

        public async Task<QmBogenResponse?> GetQmBogenAsync(string EIID, int QLID, CancellationToken cancellationToken)
        {
            const string sql = "SELECT QBID, FKEIID, FKQLID, Bezeichnung, Status, GeaendertAm FROM [dbo].[QMWV3Bogen] WHERE FKEIID = @EIID AND FKQLID = @QLID";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@EIID", EIID);
            command.Parameters.AddWithValue("@QLID", QLID);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new QmBogenResponse(
                Convert.ToInt32(reader.GetValue(0)),
                Convert.ToString(reader.GetValue(1)) ?? EIID,
                Convert.ToInt32(reader.GetValue(2)),
                Convert.ToString(reader.GetValue(3)) ?? string.Empty,
                Convert.ToInt32(reader.GetValue(4)),
                reader.IsDBNull(5) ? null : Convert.ToDateTime(reader.GetValue(5)));
        }

        private static string ReadString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
        private static string? ReadNullableString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
        private static int ReadInt(SqlDataReader reader, int ordinal) => Convert.ToInt32(reader.GetValue(ordinal));
        private static double ReadDouble(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : Convert.ToDouble(reader.GetValue(ordinal));
        private static int? ReadNullableInt(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
        private static DateTime? ReadNullableDate(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
        private static bool ReadBool(SqlDataReader reader, int ordinal) => !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal));
    }
}
