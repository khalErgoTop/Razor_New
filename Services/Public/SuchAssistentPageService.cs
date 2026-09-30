using Microsoft.Data.SqlClient;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Services.Public
{
    public class SuchAssistentPageService
    {
        private readonly string _connectionString;

        public SuchAssistentPageService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("TOPqwWeb")
                ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
        }

        public async Task<SuchAssistentResponse> GetCategoriesAsync(int? age, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT DISTINCT LTRIM(RTRIM(et.BF_Kategorie))
                FROM Einrichtungstypen et
                INNER JOIN Einrichtungen ei ON ei.FKETID = et.ETID
                WHERE ISNULL(et.BF_Kategorie, '') <> ''
                  AND (@age IS NULL OR et.AltersstrukturU18 = @age)
                  AND et.ETID IN (SELECT FKETID FROM HTML_EinrichtungstypenExport INNER JOIN HTML_Dokument ON FKHDID = HDID WHERE Exportieren = 1 AND Dokumenttitel = 'Einrichtung')
                  AND NOT ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 0)
                ORDER BY LTRIM(RTRIM(et.BF_Kategorie));
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@age", (object?)age ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var response = new SuchAssistentResponse();
            while (await reader.ReadAsync(cancellationToken))
            {
                response.Categories.Add(new SuchAssistentCategoryResponse
                {
                    Name = Convert.ToString(reader.GetValue(0)) ?? string.Empty
                });
            }

            return response;
        }

        public async Task<List<SuchAssistentLeistungResponse>> GetLeistungenAsync(
            string category,
            int? age,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT DISTINCT et.ETID, et.Einrichtungstyp
                FROM Einrichtungstypen et
                INNER JOIN Einrichtungen ei ON ei.FKETID = et.ETID
                    AND ei.FKETID IN (SELECT FKETID FROM HTML_EinrichtungstypenExport INNER JOIN HTML_Dokument ON FKHDID = HDID WHERE Exportieren = 1 AND Dokumenttitel = 'Einrichtung')
                    AND NOT ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 0)
                WHERE et.BF_Kategorie = @category
                  AND (@age IS NULL OR et.AltersstrukturU18 = @age)
                ;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@category", category);
            command.Parameters.AddWithValue("@age", (object?)age ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var leistungen = new List<SuchAssistentLeistungResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                leistungen.Add(new SuchAssistentLeistungResponse
                {
                    Id = reader.GetInt32(0),
                    Name = Convert.ToString(reader.GetValue(1)) ?? string.Empty
                });
            }

            return leistungen;
        }

        public async Task<string?> GetDescriptionForLeistungAsync(
            int leistungId,
            CancellationToken cancellationToken)
        {
            const string sql = "SELECT BF_Beschreibung FROM dbo.Einrichtungstypen WHERE ETID = @leistungId;";

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@leistungId", leistungId);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value == null || value == DBNull.Value ? null : Convert.ToString(value);
        }

        public async Task<List<string>> GetPostalCodesAsync(string term, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT DISTINCT plz
                FROM dbo.GEO_Locations
                WHERE plz LIKE @term;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@term", $"{term}%");
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var postalCodes = new List<string>();
            while (await reader.ReadAsync(cancellationToken))
            {
                postalCodes.Add(Convert.ToString(reader.GetValue(0))?.Trim() ?? string.Empty);
            }

            return postalCodes;
        }

        public async Task<List<SuchAssistentKreisResponse>> GetKreiseAsync(
            int leistungId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                DECLARE @now datetime = GETDATE();
                DECLARE @nowP1 datetime = GETDATE() + 0.001;
                SELECT kr.KRID, kr.Kreis
                FROM dbo.Kreise kr
                LEFT JOIN Einrichtungen ei ON kr.KRID = ei.FKKRID
                    AND ei.Status = 5
                    AND ISNULL(ei.GeschlossenAm, @nowP1) > @now
                LEFT JOIN Standorte std ON ei.EIID = std.FKEIID
                    AND ((ISNULL(std.FKKRID, -1) = 0 AND ei.FKKRID = kr.KRID)
                      OR (ISNULL(std.FKKRID, -1) <> 0 AND ISNULL(std.FKKRID, ei.FKKRID) = kr.KRID))
                    AND (ISNULL(std.GeschlossenAmStandort, @nowP1) > @now
                      AND ISNULL(std.StandortOhneLeistung, 0) = 0
                      OR ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 1))
                WHERE kr.Kreisschluessel <> ''
                  AND ei.FKETID = @leistungId
                  AND ei.EIID IS NOT NULL
                  AND std.STID IS NOT NULL
                GROUP BY kr.KRID, kr.Kreis, kr.Reihenfolge
                ORDER BY kr.Reihenfolge;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@leistungId", leistungId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var kreise = new List<SuchAssistentKreisResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                kreise.Add(new SuchAssistentKreisResponse
                {
                    Id = reader.GetInt32(0),
                    Name = Convert.ToString(reader.GetValue(1)) ?? string.Empty
                });
            }

            return kreise;
        }

        public async Task<List<SuchAssistentErgebnisResponse>> GetResultsByKreisAsync(
            int kreisId,
            int leistungId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                DECLARE @now datetime = GETDATE();
                DECLARE @nowP1 datetime = GETDATE() + 0.001;
                SELECT TOP (50) ei.EIID, std.STUID, ei.Einrichtungsname,
                       ei.Einrichtungskennzeichen, std.Standortname, std.Strasse,
                       std.Hausnummer, std.Hausnummerzusatz, std.PLZ, std.Ort,
                       std.Telefon, std.Email, std.Internet
                FROM dbo.Einrichtungen ei
                INNER JOIN dbo.Standorte std ON ei.EIID = std.FKEIID
                INNER JOIN dbo.Einrichtungstypen et ON ei.FKETID = et.ETID
                WHERE ((ISNULL(std.FKKRID, -1) = 0 AND ei.FKKRID = @kreisId)
                    OR (ISNULL(std.FKKRID, -1) <> 0 AND ISNULL(std.FKKRID, ei.FKKRID) = @kreisId))
                  AND et.ETID = @leistungId
                  AND ISNULL((SELECT t.Gesperrt FROM dbo.Traeger t WHERE t.TRID = ei.FKTRID), 0) = 0
                  AND ((ei.Status = 5
                    AND ISNULL(ei.GeschlossenAm, @nowP1) > @now
                    AND ISNULL(std.GeschlossenAmStandort, @nowP1) > @now
                    AND ISNULL(std.StandortOhneLeistung, 0) = 0)
                    OR ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 1))
                  AND ei.FKETID IN (SELECT FKETID FROM HTML_EinrichtungstypenExport INNER JOIN HTML_Dokument ON FKHDID = HDID WHERE Exportieren = 1 AND Dokumenttitel = 'Einrichtung')
                  AND NOT ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 0)
                ORDER BY ei.Einrichtungsname, std.Hauptstandort DESC, std.Standortname;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@kreisId", kreisId);
            command.Parameters.AddWithValue("@leistungId", leistungId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<SuchAssistentErgebnisResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SuchAssistentErgebnisResponse
                {
                    EIID = Convert.ToString(reader.GetValue(0)) ?? string.Empty,
                    STUID = GetNullableString(reader, 1),
                    Name = Convert.ToString(reader.GetValue(2)) ?? string.Empty,
                    Einrichtungskennzeichen = GetNullableString(reader, 3),
                    Standortname = GetNullableString(reader, 4),
                    Strasse = GetNullableString(reader, 5),
                    Hausnummer = GetNullableString(reader, 6),
                    Hausnummerzusatz = GetNullableString(reader, 7),
                    PLZ = GetNullableString(reader, 8),
                    Ort = GetNullableString(reader, 9),
                    Telefon = GetNullableString(reader, 10),
                    Email = GetNullableString(reader, 11),
                    Internet = GetNullableString(reader, 12)
                });
            }

            return results;
        }

        public async Task<List<SuchAssistentErgebnisResponse>> GetResultsByGeolocationAsync(
            decimal latitude,
            decimal longitude,
            int leistungId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                DECLARE @now datetime = GETDATE();
                DECLARE @nowP1 datetime = GETDATE() + 0.001;
                SELECT TOP (50) ei.EIID, std.STUID, ei.Einrichtungsname,
                       ei.Einrichtungskennzeichen, std.Standortname, std.Strasse,
                       std.Hausnummer, std.Hausnummerzusatz, std.PLZ, std.Ort,
                       std.Telefon, std.Email, std.Internet,
                       ISNULL(dbo.GetGeoDistance(@latitude, @longitude, std.GeoLat, std.GeoLon),
                              dbo.GetGeoDistanceByPlz2(@latitude, @longitude, std.PLZ)) AS Entfernung
                FROM dbo.Einrichtungen ei
                INNER JOIN dbo.Standorte std ON ei.EIID = std.FKEIID
                INNER JOIN dbo.Einrichtungstypen et ON ei.FKETID = et.ETID
                WHERE et.ETID = @leistungId
                  AND ISNULL((SELECT t.Gesperrt FROM dbo.Traeger t WHERE t.TRID = ei.FKTRID), 0) = 0
                  AND ((ei.Status = 5
                    AND ISNULL(ei.GeschlossenAm, @nowP1) > @now
                    AND ISNULL(std.GeschlossenAmStandort, @nowP1) > @now
                    AND ISNULL(std.StandortOhneLeistung, 0) = 0)
                    OR ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 1))
                  AND ei.FKETID IN (SELECT FKETID FROM HTML_EinrichtungstypenExport INNER JOIN HTML_Dokument ON FKHDID = HDID WHERE Exportieren = 1 AND Dokumenttitel = 'Einrichtung')
                  AND NOT ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 0)
                ORDER BY Entfernung, ei.Einrichtungsname, std.Hauptstandort DESC;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@latitude", latitude);
            command.Parameters.AddWithValue("@longitude", longitude);
            command.Parameters.AddWithValue("@leistungId", leistungId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<SuchAssistentErgebnisResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SuchAssistentErgebnisResponse
                {
                    EIID = Convert.ToString(reader.GetValue(0)) ?? string.Empty,
                    STUID = GetNullableString(reader, 1),
                    Name = Convert.ToString(reader.GetValue(2)) ?? string.Empty,
                    Einrichtungskennzeichen = GetNullableString(reader, 3),
                    Standortname = GetNullableString(reader, 4),
                    Strasse = GetNullableString(reader, 5),
                    Hausnummer = GetNullableString(reader, 6),
                    Hausnummerzusatz = GetNullableString(reader, 7),
                    PLZ = GetNullableString(reader, 8),
                    Ort = GetNullableString(reader, 9),
                    Telefon = GetNullableString(reader, 10),
                    Email = GetNullableString(reader, 11),
                    Internet = GetNullableString(reader, 12),
                    Entfernung = reader.IsDBNull(13) ? null : Convert.ToDecimal(reader.GetValue(13))
                });
            }

            return results;
        }

        public async Task<List<SuchAssistentErgebnisResponse>> GetResultsByPostalCodeAsync(
            string postalCode,
            int leistungId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                DECLARE @now datetime = GETDATE();
                DECLARE @nowP1 datetime = GETDATE() + 0.001;
                SELECT TOP (50) ei.EIID, std.STUID, ei.Einrichtungsname,
                       ei.Einrichtungskennzeichen, std.Standortname, std.Strasse,
                       std.Hausnummer, std.Hausnummerzusatz, std.PLZ, std.Ort,
                       std.Telefon, std.Email, std.Internet
                FROM dbo.Einrichtungen ei
                INNER JOIN dbo.Standorte std ON ei.EIID = std.FKEIID
                INNER JOIN dbo.Einrichtungstypen et ON ei.FKETID = et.ETID
                WHERE std.PLZ = @postalCode
                  AND et.ETID = @leistungId
                  AND ISNULL((SELECT t.Gesperrt FROM dbo.Traeger t WHERE t.TRID = ei.FKTRID), 0) = 0
                  AND ((ei.Status = 5
                    AND ISNULL(ei.GeschlossenAm, @nowP1) > @now
                    AND ISNULL(std.GeschlossenAmStandort, @nowP1) > @now
                    AND ISNULL(std.StandortOhneLeistung, 0) = 0)
                    OR ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 1))
                  AND ei.FKETID IN (SELECT FKETID FROM HTML_EinrichtungstypenExport INNER JOIN HTML_Dokument ON FKHDID = HDID WHERE Exportieren = 1 AND Dokumenttitel = 'Einrichtung')
                  AND NOT ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 0)
                ORDER BY ei.Einrichtungsname, std.Hauptstandort DESC, std.Standortname;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@postalCode", postalCode);
            command.Parameters.AddWithValue("@leistungId", leistungId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<SuchAssistentErgebnisResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadResult(reader));
            }

            return results;
        }

        private static SuchAssistentErgebnisResponse ReadResult(SqlDataReader reader)
        {
            return new SuchAssistentErgebnisResponse
            {
                EIID = Convert.ToString(reader.GetValue(0)) ?? string.Empty,
                STUID = GetNullableString(reader, 1),
                Name = Convert.ToString(reader.GetValue(2)) ?? string.Empty,
                Einrichtungskennzeichen = GetNullableString(reader, 3),
                Standortname = GetNullableString(reader, 4),
                Strasse = GetNullableString(reader, 5),
                Hausnummer = GetNullableString(reader, 6),
                Hausnummerzusatz = GetNullableString(reader, 7),
                PLZ = GetNullableString(reader, 8),
                Ort = GetNullableString(reader, 9),
                Telefon = GetNullableString(reader, 10),
                Email = GetNullableString(reader, 11),
                Internet = GetNullableString(reader, 12)
            };
        }

        private static string? GetNullableString(SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
        }
    }
}
