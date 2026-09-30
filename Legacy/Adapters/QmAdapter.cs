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
            const string sql = "SELECT ok.OKID, ISNULL(ilo.Position, ok.Position) AS OKPosition, ok.Bezeichnung, ok.BezeichnungKurz, uk.UKID, iou.Position AS UKPosition, uk.Bezeichnung, uk.BezeichnungKurz, f.QFID, iuf.Position AS FragePosition, f.FrageKurz, f.FrageLang, f.Hilfetext, f.Format, iuf.Mussfeld, iuf.Eintragstyp FROM [dbo].[QMLV3Oberkategorie] ok INNER JOIN [dbo].[QMLV3IndexLayoutOberkategorie] ilo ON ilo.FKOKID = ok.OKID AND ilo.FKQLID = @QLID INNER JOIN [dbo].[QMLV3IndexEinrichtungstypOberkategorie] ieo ON ieo.FKOKID = ok.OKID AND ieo.FKQLID = ilo.FKQLID AND ieo.FKETID = @ETID INNER JOIN [dbo].[QMLV3IndexOberUnterkategorie] iou ON iou.FKQLID = @QLID AND iou.FKOKID = ok.OKID INNER JOIN [dbo].[QMLV3Unterkategorie] uk ON uk.UKID = iou.FKUKID INNER JOIN [dbo].[QMLV3IndexEinrichtungstypUnterkategorie] ieu ON ieu.FKQLID = @QLID AND ieu.FKOKID = ok.OKID AND ieu.FKUKID = uk.UKID AND ieu.FKETID = @ETID INNER JOIN [dbo].[QMLV3IndexUnterkategorieFrage] iuf ON iuf.FKQLID = @QLID AND iuf.FKOKID = ok.OKID AND iuf.FKUKID = uk.UKID INNER JOIN [dbo].[QMLV3Frage] f ON f.QFID = iuf.FKQFID WHERE ISNULL(ok.isDeleted, 0) = 0 AND ISNULL(ilo.isDeleted, 0) = 0 AND ISNULL(iou.isDeleted, 0) = 0 AND ISNULL(iuf.isDeleted, 0) = 0 AND ISNULL(f.isDeleted, 0) = 0 ORDER BY OKPosition, UKPosition, FragePosition";
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

        public async Task<bool> SaveFrageWertAsync(int QMBogenID, int QIID, string format, string value, CancellationToken cancellationToken)
        {
            const string questionSql = "SELECT f.Format FROM QMLV3IndexUnterkategorieFrage iuf INNER JOIN QMLV3Frage f ON f.QFID = iuf.FKQFID WHERE iuf.QIID = @QIID AND EXISTS (SELECT 1 FROM QMWV3Bogen b WHERE b.QBID = @QMBogenID AND b.FKQLID = iuf.FKQLID) AND ISNULL(iuf.isDeleted, 0) = 0 AND ISNULL(f.isDeleted, 0) = 0";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var questionCommand = new SqlCommand(questionSql, connection);
            questionCommand.Parameters.AddWithValue("@QIID", QIID);
            questionCommand.Parameters.AddWithValue("@QMBogenID", QMBogenID);
            var storedFormat = Convert.ToString(await questionCommand.ExecuteScalarAsync(cancellationToken));
            if (string.IsNullOrWhiteSpace(storedFormat) || !string.Equals(storedFormat, format, StringComparison.OrdinalIgnoreCase)) return false;

            const string sql = "DECLARE @ID int; SELECT @ID = QWID FROM QMWV3Wert WHERE FKQBID = @QMBogenID AND FKQIID = @QIID; IF @ID IS NULL INSERT INTO QMWV3Wert (FKQBID, FKQIID, WertINT, WertREAL, WertBOOL, WertTXT, WertMEMO, GeaendertAm) VALUES (@QMBogenID, @QIID, @WertINT, @WertREAL, @WertBOOL, @WertTXT, @WertMEMO, GETDATE()); ELSE UPDATE QMWV3Wert SET WertINT = @WertINT, WertREAL = @WertREAL, WertBOOL = @WertBOOL, WertTXT = @WertTXT, WertMEMO = @WertMEMO, GeaendertAm = GETDATE() WHERE QWID = @ID;";
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@QMBogenID", QMBogenID);
            command.Parameters.AddWithValue("@QIID", QIID);
            command.Parameters.AddWithValue("@WertINT", ParseInt(format, value));
            command.Parameters.AddWithValue("@WertREAL", ParseDouble(format, value));
            command.Parameters.AddWithValue("@WertBOOL", ParseBool(format, value));
            command.Parameters.AddWithValue("@WertTXT", format.Equals("dbText", StringComparison.OrdinalIgnoreCase) ? value : string.Empty);
            command.Parameters.AddWithValue("@WertMEMO", format.Equals("dbMemo", StringComparison.OrdinalIgnoreCase) ? value : string.Empty);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }

        public async Task<bool> SetBogenInBearbeitungAsync(int QMBogenID, string EIID, CancellationToken cancellationToken)
        {
            const string sql = "UPDATE [dbo].[QMWV3Bogen] SET [Status] = @Status, [GeaendertAm] = GETDATE() WHERE [QBID] = @QMBogenID AND [FKEIID] = @EIID AND [Status] IN (1, 2, 5, 6)";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Status", 2);
            command.Parameters.AddWithValue("@QMBogenID", QMBogenID);
            command.Parameters.AddWithValue("@EIID", EIID);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }

        public async Task<bool> SetBogenVersandtAsync(int QMBogenID, string EIID, CancellationToken cancellationToken)
        {
            const string sql = "UPDATE [dbo].[QMWV3Bogen] SET [Status] = @Status, [GeaendertAm] = GETDATE() WHERE [QBID] = @QMBogenID AND [FKEIID] = @EIID AND [Status] IN (2, 6)";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Status", 3);
            command.Parameters.AddWithValue("@QMBogenID", QMBogenID);
            command.Parameters.AddWithValue("@EIID", EIID);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }

        private static int ParseInt(string format, string value) => format.Equals("dbLong", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var result) ? result : 0;
        private static double ParseDouble(string format, string value) => (format.Equals("dbReal", StringComparison.OrdinalIgnoreCase) || format.Equals("dbSingle", StringComparison.OrdinalIgnoreCase) || format.Equals("dbDouble", StringComparison.OrdinalIgnoreCase)) && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out var result) ? result : 0;
        private static bool ParseBool(string format, string value) => (format.Equals("dbBoolean", StringComparison.OrdinalIgnoreCase) || format.Equals("dbBool", StringComparison.OrdinalIgnoreCase)) && bool.TryParse(value, out var result) && result;

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

        public async Task<IReadOnlyList<QmArchiveFacilityResponse>> GetArchiveFacilitiesAsync(CancellationToken cancellationToken)
        {
            if (_user.BEID is not int lBEID || lBEID <= 0) return [];

            var sql = new StringBuilder("SELECT DISTINCT ei.EIID, ei.Einrichtungsname, ei.Einrichtungskennzeichen, qb.QBID, qb.FKQLID, qb.Bezeichnung, qb.Status, l.Layoutjahr, qb.GeaendertAm FROM Einrichtungen ei ");
            if (!_user.IsAdmin)
            {
                switch (_user.UserType)
                {
                    case CurrentUserAccessor.LegacyUserTypes.TOPqw when _user.IsKosoz:
                    case CurrentUserAccessor.LegacyUserTypes.Jugendamt:
                        sql.Append("INNER JOIN IndexBenutzerEinrichtungstyp ibet ON ei.FKETID = ibet.FKETID INNER JOIN Benutzer b ON ibet.FKBEID = b.BEID ");
                        break;
                    case CurrentUserAccessor.LegacyUserTypes.Traeger:
                        sql.Append("INNER JOIN Benutzer b ON ei.FKTRID = b.FKTRID ");
                        break;
                    case CurrentUserAccessor.LegacyUserTypes.Verband:
                        sql.Append("INNER JOIN Traeger tr ON ei.FKTRID = tr.TRID INNER JOIN Benutzer b ON tr.FKVEID = b.TOPqwID AND ISNULL(tr.VerbandszugriffErlauben, 1) = 1 ");
                        break;
                    default:
                        sql.Append("INNER JOIN IndexBenutzerEinrichtung ibe ON ei.EIID = ibe.FKEIID INNER JOIN Benutzer b ON ibe.FKBEID = b.BEID ");
                        break;
                }
            }
            sql.Append("INNER JOIN QMWV3Bogen qb ON ei.EIID = qb.FKEIID INNER JOIN QMLV3Layout l ON qb.FKQLID = l.QLID WHERE l.Aktiv <> 1 ");
            if (!_user.IsAdmin && _user.UserType != CurrentUserAccessor.LegacyUserTypes.TOPqw)
            {
                sql.Append("AND b.BEID = @BEID ");
            }
            sql.Append("ORDER BY ei.Einrichtungskennzeichen, l.Layoutjahr DESC, qb.Bezeichnung");

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql.ToString(), connection);
            command.Parameters.AddWithValue("@BEID", lBEID);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var facilities = new List<QmArchiveFacilityResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var eiid = ReadString(reader, 0);
                var facility = facilities.FirstOrDefault(item => item.EIID == eiid);
                var bogen = new QmArchiveBogenResponse(ReadInt(reader, 3), ReadInt(reader, 4), ReadString(reader, 5), ReadInt(reader, 6), ReadInt(reader, 7), ReadNullableDate(reader, 8));
                if (facility is null)
                {
                    facilities.Add(new QmArchiveFacilityResponse(eiid, ReadString(reader, 1), ReadString(reader, 2), [bogen]));
                }
                else
                {
                    var index = facilities.IndexOf(facility);
                    facilities[index] = facility with { Boegen = facility.Boegen.Append(bogen).ToArray() };
                }
            }
            return facilities;
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

        public async Task<QmXmlHeaderResponse?> GetQmXmlHeaderAsync(string EIID, int QLID, CancellationToken cancellationToken)
        {
            const string sql = "SELECT l.Layoutjahr, ISNULL(l.IstTraegerBogen, 0), ei.EIID, ei.Einrichtungsname, ei.Einrichtungskennzeichen, ei.Aktenzeichen, st.Strasse, st.Hausnummer, st.HausnummerZusatz, st.PLZ, st.Ort, tr.TRID, tr.Traegername, tr.Strasse, tr.PLZ, tr.Ort, kr.KRID, kr.Kreis FROM QMLV3Layout l INNER JOIN Einrichtungen ei ON ei.EIID = @EIID INNER JOIN Traeger tr ON tr.TRID = ei.FKTRID LEFT JOIN Kreise kr ON kr.KRID = ei.FKKRID OUTER APPLY (SELECT TOP 1 Strasse, Hausnummer, HausnummerZusatz, PLZ, Ort FROM Standorte WHERE FKEIID = ei.EIID ORDER BY Hauptstandort DESC, STUID) st WHERE l.QLID = @QLID";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@EIID", EIID);
            command.Parameters.AddWithValue("@QLID", QLID);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return new QmXmlHeaderResponse(
                ReadInt(reader, 0), ReadBool(reader, 1), ReadString(reader, 2), ReadString(reader, 3), ReadString(reader, 4),
                ReadNullableString(reader, 5), ReadNullableString(reader, 6), ReadNullableString(reader, 7), ReadNullableString(reader, 8),
                ReadNullableString(reader, 9), ReadNullableString(reader, 10), ReadNullableString(reader, 11), ReadNullableString(reader, 12),
                ReadNullableString(reader, 13), ReadNullableString(reader, 14), ReadNullableString(reader, 15),
                await GetQmXmlFacilityTypesAsync(EIID, cancellationToken), ReadNullableInt(reader, 16), ReadNullableString(reader, 17));
        }

        private async Task<IReadOnlyList<QmXmlFacilityTypeResponse>> GetQmXmlFacilityTypesAsync(string EIID, CancellationToken cancellationToken)
        {
            const string sql = "SELECT et.Typkennzeichnung, et.Einrichtungstyp FROM Einrichtungen ei INNER JOIN Einrichtungstypen et ON et.ETID = ei.FKETID WHERE ei.EIID = @EIID ORDER BY et.Reihenfolge, et.Kurzbezeichnung, et.Typkennzeichnung";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@EIID", EIID);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = new List<QmXmlFacilityTypeResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new QmXmlFacilityTypeResponse(ReadString(reader, 0), ReadString(reader, 1)));
            }
            return result;
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
