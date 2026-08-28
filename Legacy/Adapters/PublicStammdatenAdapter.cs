using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Legacy.Adapters
{
    public class PublicStammdatenAdapter
    {
        private readonly string _connectionString;
        private readonly string _facilityImagesPath;
    private readonly ILogger<PublicStammdatenAdapter> _logger;

        public PublicStammdatenAdapter(IConfiguration configuration, IWebHostEnvironment environment, ILogger<PublicStammdatenAdapter> logger)
        {
            _connectionString = configuration.GetConnectionString("TOPqwWeb")
                ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
            var configuredPath = configuration["FacilityImages:Path"] ?? "Images/FacilityImages";
            _facilityImagesPath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath);
            _logger = logger;
        }

        public async Task<InstitutionContactDataResponse?> GetInstitutionContactDataAsync(string eiid, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT tr.TRID, tr.Traegername, ISNULL(tr.Telefon, ''), ISNULL(tr.Email, ''),
                       ISNULL(tr.Internet, ''), ISNULL(tr.VerbandszugriffErlauben, 1)
                FROM Einrichtungen ei
                INNER JOIN Traeger tr ON tr.TRID = ei.FKTRID
                WHERE ei.EIID = @eiid;
                """;
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return new InstitutionContactDataResponse
            {
                Trid = Convert.ToString(reader.GetValue(0)) ?? string.Empty,
                Name = Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                Phone = Convert.ToString(reader.GetValue(2)) ?? string.Empty,
                Email = Convert.ToString(reader.GetValue(3)) ?? string.Empty,
                Internet = Convert.ToString(reader.GetValue(4)) ?? string.Empty,
                AssociationAccessAllowed = Convert.ToBoolean(reader.GetValue(5))
            };
        }

        public async Task<bool> GetBooleanOptionAsync(string optionName, CancellationToken cancellationToken)
        {
            const string sql = "SELECT TOP 1 WertBOOL FROM TOPqw_Einstellungen WHERE Einstellung = @optionName AND ISNULL(fkUSID, 0) = 0";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@optionName", optionName);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is not null && value != DBNull.Value && Convert.ToBoolean(value);
        }

        public async Task<IReadOnlyList<string>> GetStreetNameSuggestionsAsync(string prefix, CancellationToken cancellationToken)
        {
            const string sql = "SELECT DISTINCT TOP 10 Strassenname FROM Strassenverzeichnis WHERE Strassenname LIKE @prefix + '%'";
            return await ReadStringListAsync(sql, [new("@prefix", prefix)], "Strassenname", cancellationToken);
        }

        public async Task<IReadOnlyList<string>> GetHouseNumberSuggestionsAsync(string streetName, string prefix, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(streetName) || streetName.Length <= 2) return [];
            const string sql = "SELECT DISTINCT TOP 10 CASE WHEN Alphazusatz IS NOT NULL THEN Hausnummer + '' + ISNULL(Alphazusatz, '') ELSE Hausnummer END AS FullHausnummer FROM Strassenverzeichnis WHERE Strassenname = @streetName AND (Hausnummer LIKE @prefix + '%' OR Alphazusatz LIKE @prefix + '%')";
            return await ReadStringListAsync(sql, [new("@streetName", streetName), new("@prefix", prefix)], "FullHausnummer", cancellationToken);
        }

        public async Task<StreetDirectoryDetails?> GetStreetDetailsAsync(string streetName, string houseNumber, CancellationToken cancellationToken)
        {
            const string sql = "SELECT TOP 1 Alphazusatz, PLZ, Ort, Stadtteil, Bezirksname FROM Strassenverzeichnis WHERE Strassenname = @streetName AND Hausnummer + ISNULL(Alphazusatz, '') = @houseNumber";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@streetName", streetName);
            command.Parameters.AddWithValue("@houseNumber", houseNumber);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            string? GetValue(int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal).ToString();
            return new StreetDirectoryDetails(GetValue(0), GetValue(1), GetValue(2), GetValue(3), GetValue(4));
        }

        public async Task<bool> StreetAddressExistsAsync(string streetName, string houseNumber, string? addition, string? postalCode, string? city, CancellationToken cancellationToken)
        {
            const string sql = "SELECT COUNT(Id) FROM Strassenverzeichnis WHERE Strassenname = @streetName AND Hausnummer = @houseNumber AND Alphazusatz = @addition AND PLZ = @postalCode AND Ort = @city";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@streetName", streetName);
            command.Parameters.AddWithValue("@houseNumber", houseNumber);
            command.Parameters.AddWithValue("@addition", (object?)addition ?? DBNull.Value);
            command.Parameters.AddWithValue("@postalCode", (object?)postalCode ?? DBNull.Value);
            command.Parameters.AddWithValue("@city", (object?)city ?? DBNull.Value);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        private async Task<IReadOnlyList<string>> ReadStringListAsync(string sql, IEnumerable<SqlParameter> parameters, string column, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddRange(parameters.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var values = new List<string>();
            while (await reader.ReadAsync(cancellationToken))
                values.Add(Convert.ToString(reader[column]) ?? string.Empty);
            return values;
        }

        public async Task<bool> SaveInstitutionContactDataAsync(string eiid, InstitutionContactDataInput input, CancellationToken cancellationToken)
        {
            const string sql = """
                UPDATE tr
                SET Telefon=@phone, Email=@email, Internet=@internet,
                    VerbandszugriffErlauben=@associationAccessAllowed
                FROM Traeger tr
                INNER JOIN Einrichtungen ei ON ei.FKTRID = tr.TRID
                WHERE ei.EIID = @eiid;
                """;
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            command.Parameters.AddWithValue("@phone", input.Phone?.Trim() ?? string.Empty);
            command.Parameters.AddWithValue("@email", input.Email?.Trim() ?? string.Empty);
            command.Parameters.AddWithValue("@internet", input.Internet?.Trim() ?? string.Empty);
            command.Parameters.AddWithValue("@associationAccessAllowed", input.AssociationAccessAllowed);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<IReadOnlyList<FacilityRepresentativeResponse>> GetFacilityRepresentativesAsync(string eiid, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT TOP 1 ISNULL(b.Vorname + ' ', '') + ISNULL(b.Nachname, ''),
                       ISNULL(b.Email, ''), ISNULL(b.Benutzername, '')
                FROM Benutzer b
                INNER JOIN IndexBenutzerEinrichtung ibe ON ibe.FKBEID = b.BEID
                WHERE ibe.FKEIID = @eiid
                ORDER BY b.Nachname, b.Vorname;
                """;
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var representatives = new List<FacilityRepresentativeResponse>();
            while (await reader.ReadAsync(cancellationToken))
                representatives.Add(new FacilityRepresentativeResponse(Convert.ToString(reader.GetValue(0)) ?? string.Empty, Convert.ToString(reader.GetValue(1)) ?? string.Empty, Convert.ToString(reader.GetValue(2)) ?? string.Empty));
            return representatives;
        }

        public async Task<IReadOnlyList<FacilityFeatureResponse>> GetFacilityFeaturesAsync(string eiid, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT lm.LMID, lm.Typbezeichnung, lm.Beschreibung, ISNULL(lm.Kategorie, '')
                FROM Leistungsmerkmal lm
                INNER JOIN EinrichtungsMerkmale em ON em.FKLMID = lm.LMID
                WHERE em.FKEIID = @eiid
                ORDER BY lm.Kategorie, lm.Reihenfolge, lm.Typbezeichnung, lm.Beschreibung;
                """;
            return await ReadFeatureListAsync(sql, eiid, cancellationToken);
        }

        public async Task<IReadOnlyList<FacilityFeatureResponse>> GetAvailableFacilityFeaturesAsync(CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT LMID, Typbezeichnung, Beschreibung, ISNULL(Kategorie, '')
                FROM Leistungsmerkmal
                ORDER BY Kategorie, Reihenfolge, Typbezeichnung, Beschreibung;
                """;
            return await ReadFeatureListAsync(sql, null, cancellationToken);
        }

        private async Task<IReadOnlyList<FacilityFeatureResponse>> ReadFeatureListAsync(string sql, string? eiid, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            if (eiid is not null) command.Parameters.AddWithValue("@eiid", eiid);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var features = new List<FacilityFeatureResponse>();
            while (await reader.ReadAsync(cancellationToken))
                features.Add(new FacilityFeatureResponse(Convert.ToInt32(reader.GetValue(0)), Convert.ToString(reader.GetValue(1)) ?? string.Empty, Convert.ToString(reader.GetValue(2)) ?? string.Empty, Convert.ToString(reader.GetValue(3)) ?? string.Empty));
            return features;
        }

        public async Task<bool> SaveFacilityFeaturesAsync(string eiid, IReadOnlyCollection<int> featureIds, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await using (var deleteCommand = new SqlCommand("DELETE FROM EinrichtungsMerkmale WHERE FKEIID=@eiid;", connection, transaction))
                {
                    deleteCommand.Parameters.AddWithValue("@eiid", eiid);
                    await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
                }
                foreach (var featureId in featureIds.Distinct())
                {
                    await using var insertCommand = new SqlCommand("INSERT INTO EinrichtungsMerkmale (FKEIID, FKLMID) SELECT @eiid, @lmid WHERE EXISTS (SELECT 1 FROM Leistungsmerkmal WHERE LMID=@lmid);", connection, transaction);
                    insertCommand.Parameters.AddWithValue("@eiid", eiid);
                    insertCommand.Parameters.AddWithValue("@lmid", featureId);
                    await insertCommand.ExecuteNonQueryAsync(cancellationToken);
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

        public async Task<(int Count, List<VisitorChartPoint> Chart)> GetVisitorDataAsync(string eiid, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var countCommand = new SqlCommand("dbo.GetFacilityViewCount", connection) { CommandType = System.Data.CommandType.StoredProcedure };
            countCommand.Parameters.AddWithValue("@EIID", eiid);
            var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
            await using var chartCommand = new SqlCommand("GetFacilityViewsByTimeFrame", connection) { CommandType = System.Data.CommandType.StoredProcedure };
            chartCommand.Parameters.AddWithValue("@EIID", eiid);
            chartCommand.Parameters.AddWithValue("@date1", new DateTime(1753, 1, 1));
            chartCommand.Parameters.AddWithValue("@date2", DateTime.UtcNow);
            chartCommand.Parameters.AddWithValue("@viewMode", "month");
            await using var reader = await chartCommand.ExecuteReaderAsync(cancellationToken);
            var chart = new List<VisitorChartPoint>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var dateOrdinal = reader.GetOrdinal("DateDay");
                var countOrdinal = reader.GetOrdinal("ViewsPerDay");
                var dateValue = reader.GetValue(dateOrdinal);
                var date = dateValue is DateTime dateTime
                    ? dateTime
                    : Convert.ToDateTime(dateValue, System.Globalization.CultureInfo.InvariantCulture);
                chart.Add(new VisitorChartPoint { Date = date, Count = Convert.ToInt32(reader.GetValue(countOrdinal)) });
            }
            return (count, chart);
        }

        public async Task<FreePlaceReportingResponse?> GetFreePlacesAsync(string eiid, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT TOP (1) pm.ID, pm.GemeldetAm, pm.FKPMLID,
                    ISNULL(dbo.CalculatePlatzmeldungIndex(pm.FKEIID, NULL), 0) AS Availability
                FROM Platzmeldungen pm
                WHERE pm.FKEIID=@eiid
                ORDER BY pm.GemeldetAm DESC;
                """;
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            var reportId = reader.GetInt32(0);
            var report = new FreePlaceReportingResponse
            {
                ReportingDate = reader.GetDateTime(1),
                LayoutId = reader.GetInt32(2),
                Availability = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Categories = await ReadFreePlaceCategoriesAsync(connection, reportId, reader.GetInt32(2), eiid, cancellationToken)
            };
            return report;
        }

        public async Task<bool> SaveFreePlacesAsync(string eiid, IReadOnlyList<FreePlaceQuestionInput> questions, CancellationToken cancellationToken)
        {
            const string insertReport = "INSERT INTO Platzmeldungen (FKEIID, GemeldetAm, FKPMLID) OUTPUT INSERTED.ID VALUES (@eiid, GETDATE(), (SELECT TOP 1 FKPMLID FROM PlatzmeldungOberbegriffe ORDER BY FKPMLID));";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await using var reportCommand = new SqlCommand(insertReport, connection, transaction);
                reportCommand.Parameters.AddWithValue("@eiid", eiid);
                var reportId = Convert.ToInt32(await reportCommand.ExecuteScalarAsync(cancellationToken));
                foreach (var question in questions.Where(q => q.TypeId is 0 or 1))
                {
                    var column = question.TypeId == 0 ? "WertBool" : "WertInt";
                    var value = question.TypeId == 0 ? (object)question.BooleanValue : question.IntegerValue;
                    var sql = $"INSERT INTO PlatzmeldungWerte (FKPMID, FKPOFID, {column}) VALUES (@reportId, @questionId, @value);";
                    await using var valueCommand = new SqlCommand(sql, connection, transaction);
                    valueCommand.Parameters.AddWithValue("@reportId", reportId);
                    valueCommand.Parameters.AddWithValue("@questionId", question.QuestionId);
                    valueCommand.Parameters.AddWithValue("@value", value);
                    await valueCommand.ExecuteNonQueryAsync(cancellationToken);
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

        private static async Task<List<FreePlaceCategoryResponse>> ReadFreePlaceCategoriesAsync(SqlConnection connection, int reportId, int layoutId, string eiid, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT PMOID, FKPMLID, PMO_Bezeichnung, POFID, WertTyp, Bezeichnung, Kurzbezeichnung,
                       WertBool, WertInt
                FROM dbo.GetPlatzmeldungByEIIDAndID(@eiid, @reportId)
                ORDER BY PMO_Reihenfolge, Reihenfolge;
                """;
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            command.Parameters.AddWithValue("@reportId", reportId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var categories = new Dictionary<int, FreePlaceCategoryResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var categoryId = reader.GetInt32(0);
                if (!categories.TryGetValue(categoryId, out var category))
                {
                    category = new FreePlaceCategoryResponse { Id = categoryId, Label = reader.GetString(2), Questions = new List<FreePlaceQuestionResponse>() };
                    categories.Add(categoryId, category);
                }
                ((List<FreePlaceQuestionResponse>)category.Questions).Add(new FreePlaceQuestionResponse
                {
                    Id = reader.GetInt32(3), TypeId = reader.GetInt32(4), Label = reader.GetString(5), ShortLabel = reader.GetString(6),
                    BooleanValue = !reader.IsDBNull(7) && reader.GetBoolean(7), IntegerValue = reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
                });
            }
            return categories.Values.ToList();
        }

        public async Task<List<StammdatenAttachmentResponse>> GetDocumentsAsync(string eiid, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT ID, DateinameOriginal, AnhangTyp, Status
                FROM DateiAnhaengeStammdaten
                WHERE FKID=@eiid AND AnhangTyp IN (0,1,2,22)
                ORDER BY GeaendertAm, DateinameOriginal;
                """;
            return await ReadAttachmentsAsync(sql, eiid, cancellationToken);
        }

        private async Task<List<StammdatenAttachmentResponse>> ReadAttachmentsAsync(string sql, string eiid, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var attachments = new List<StammdatenAttachmentResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                attachments.Add(new StammdatenAttachmentResponse
                {
                    Id = Convert.ToString(reader.GetValue(0)) ?? string.Empty,
                    Filename = Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                    Type = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                    Status = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3))
                });
            }
            return attachments;
        }

        public async Task<bool> SaveAttachmentAsync(string eiid, IFormFile file, int attachmentType, int maxFiles, CancellationToken cancellationToken)
        {
            if (file.Length <= 0 || file.Length > 2_000_000 || !IsAllowedFile(file.FileName)) return false;
            var content = await ReadFileAsync(file, cancellationToken);
            var md5 = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(content));
            var id = Guid.NewGuid().ToString();
            const string sql = """
                DECLARE @count int = (SELECT COUNT(*) FROM DateiAnhaengeStammdaten WHERE FKID=@eiid AND AnhangTyp=@type);
                IF @count >= @maxFiles THROW 51000, 'Maximale Dateianzahl erreicht.', 1;
                INSERT INTO DateiAnhaengeStammdaten (ID, FKID, DateinameOriginal, DateinameEncrypt, MD5, ErstelltAm, GeaendertAm, Status, AnhangTyp)
                VALUES (@id, @eiid, @filename, @encrypted, @md5, GETDATE(), GETDATE(), 0, @type);
                EXEC dbo.SaveDateiAnhaengeStammdatenFile @ID=@id, @FKID=@eiid, @DateiInhalt=@content;
                """;
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@eiid", eiid);
            command.Parameters.AddWithValue("@filename", file.FileName);
            command.Parameters.AddWithValue("@encrypted", $"{DateTime.Now:yyyyMMddHHmmss}_{eiid}_{md5}");
            command.Parameters.AddWithValue("@md5", md5);
            command.Parameters.AddWithValue("@type", attachmentType);
            command.Parameters.AddWithValue("@maxFiles", maxFiles);
            command.Parameters.Add("@content", System.Data.SqlDbType.VarBinary, -1).Value = content;
            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteAttachmentAsync(string eiid, string id, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM DateiAnhaengeStammdaten WHERE ID=@id AND FKID=@eiid;";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@eiid", eiid);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> SaveFacilityDescriptionAsync(string eiid, string description, CancellationToken cancellationToken)
        {
            const string sql = "UPDATE Einrichtungen SET Einrichtungsbeschreibung=@description WHERE EIID=@eiid;";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            command.Parameters.AddWithValue("@description", description ?? string.Empty);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> AddLocationAsync(string eiid, StammdatenLocationInput location, CancellationToken cancellationToken)
        {
            var stuid = Guid.NewGuid().ToString();
            const string sql = """
                INSERT INTO StandortAenderungen (FKSTUID, FKEIID, Status, Standortname, Strasse, Hausnummer, Hausnummerzusatz, PLZ, Ort, GeaendertAm, MitStrassenVerzeichnisValidiert)
                VALUES (@stuid, @eiid, 4, @name, @street, @number, @addition, @postalCode, @city, GETDATE(), @validated);
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@stuid", stuid);
            command.Parameters.AddWithValue("@eiid", eiid);
            command.Parameters.AddWithValue("@name", (object?)location.Name ?? DBNull.Value);
            command.Parameters.AddWithValue("@street", (object?)location.Street ?? DBNull.Value);
            command.Parameters.AddWithValue("@number", (object?)location.HouseNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("@addition", (object?)location.HouseNumberAddition ?? DBNull.Value);
            command.Parameters.AddWithValue("@postalCode", (object?)location.PostalCode ?? DBNull.Value);
            command.Parameters.AddWithValue("@city", (object?)location.City ?? DBNull.Value);
            command.Parameters.AddWithValue("@validated", location.StreetDirectoryValidated);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> SaveLocationAsync(string eiid, string stuid, StammdatenLocationInput location, CancellationToken cancellationToken)
        {
            const string sql = """
                BEGIN TRANSACTION;
                UPDATE Standorte
                SET Platzzahl=@capacity, AdresseImWebAnzeigen=@showAddress
                WHERE STUID=@stuid AND FKEIID=@eiid;
                IF @@ROWCOUNT = 0 THROW 51001, 'Standort nicht gefunden.', 1;
                IF NOT EXISTS (SELECT 1 FROM StandortAenderungen WHERE FKSTUID=@stuid)
                BEGIN
                    INSERT INTO StandortAenderungen (FKSTUID, FKEIID, Status, GeaendertAm, Standortname, Strasse, Hausnummer, Hausnummerzusatz, PLZ, Ort, MitStrassenVerzeichnisValidiert)
                    VALUES (@stuid, @eiid, 1, GETDATE(), @name, @street, @number, @addition, @postalCode, @city, @validated);
                END
                ELSE
                BEGIN
                    UPDATE StandortAenderungen
                    SET Status=1, GeaendertAm=GETDATE(), Standortname=@name, Strasse=@street, Hausnummer=@number,
                        Hausnummerzusatz=@addition, PLZ=@postalCode, Ort=@city
                    WHERE FKSTUID=@stuid;
                END;
                COMMIT TRANSACTION;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            command.Parameters.AddWithValue("@stuid", stuid);
            command.Parameters.AddWithValue("@name", (object?)location.Name ?? DBNull.Value);
            command.Parameters.AddWithValue("@street", (object?)location.Street ?? DBNull.Value);
            command.Parameters.AddWithValue("@number", (object?)location.HouseNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("@addition", (object?)location.HouseNumberAddition ?? DBNull.Value);
            command.Parameters.AddWithValue("@postalCode", (object?)location.PostalCode ?? DBNull.Value);
            command.Parameters.AddWithValue("@city", (object?)location.City ?? DBNull.Value);
            command.Parameters.AddWithValue("@capacity", (object?)location.Capacity ?? DBNull.Value);
            command.Parameters.AddWithValue("@showAddress", location.ShowAddressInWeb);
            command.Parameters.AddWithValue("@validated", location.StreetDirectoryValidated);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        private static bool IsAllowedFile(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return new[] { ".doc", ".docx", ".xls", ".xlsx", ".xml", ".pdf", ".jpg" }
                .Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<byte[]> ReadFileAsync(IFormFile file, CancellationToken cancellationToken)
        {
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            return stream.ToArray();
        }

        public async Task<bool> ToggleFacilityVisibilityAsync(string eiid, CancellationToken cancellationToken)
        {
            const string sql = """
                DECLARE @standardVisibility int = (
                    SELECT COUNT(EIID) FROM Einrichtungen
                    WHERE (([Status] = 5 AND ISNULL(GeschlossenAm, GETDATE() + 1) > GETDATE())
                           OR EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 1))
                      AND FKETID IN (SELECT FKETID FROM HTML_EinrichtungstypenExport INNER JOIN HTML_Dokument ON FKHDID = HDID WHERE Exportieren = 1 AND Dokumenttitel = 'Einrichtung')
                      AND EIID = @eiid
                      AND NOT EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 0 AND FKEIID = @eiid));
                DECLARE @override int = (SELECT TOP 1 ShowInSearch FROM SearchFacilityOverride WHERE FKEIID = @eiid);
                IF @standardVisibility > 0 AND @override IS NULL
                BEGIN
                    INSERT INTO SearchFacilityOverride (FKEIID, ShowInSearch) VALUES (@eiid, 0);
                    SELECT CAST(0 AS bit); RETURN;
                END;
                IF @standardVisibility <= 0 AND ISNULL(@override, -1) <= 0
                BEGIN
                    IF @override IS NULL INSERT INTO SearchFacilityOverride (FKEIID, ShowInSearch) VALUES (@eiid, 1);
                    ELSE UPDATE SearchFacilityOverride SET ShowInSearch = 1 WHERE FKEIID = @eiid;
                    SELECT CAST(1 AS bit); RETURN;
                END;
                DELETE FROM SearchFacilityOverride WHERE FKEIID = @eiid;
                SELECT CONVERT(bit, CASE WHEN @standardVisibility > 0 THEN 1 ELSE 0 END);
                """;
            await ExecuteVisibilityCommandAsync(sql, eiid, cancellationToken);
            return true;
        }

        public async Task<bool> GetFacilityVisibleInSearchAsync(string eiid, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT CASE WHEN
                    ((e.Status = 5 AND ISNULL(e.GeschlossenAm, GETDATE() + 1) > GETDATE())
                     OR EXISTS (SELECT 1 FROM SearchFacilityOverride o WHERE o.FKEIID = e.EIID AND o.ShowInSearch = 1))
                    AND e.FKETID IN (SELECT FKETID FROM HTML_EinrichtungstypenExport INNER JOIN HTML_Dokument ON FKHDID = HDID WHERE Exportieren = 1 AND Dokumenttitel = 'Einrichtung')
                    AND NOT EXISTS (SELECT 1 FROM SearchFacilityOverride o WHERE o.FKEIID = e.EIID AND o.ShowInSearch = 0)
                THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
                FROM Einrichtungen e
                WHERE e.EIID = @eiid;
                """;
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is not null && value != DBNull.Value && Convert.ToBoolean(value);
        }

        public async Task<bool> ToggleLocationVisibilityAsync(int stid, bool currentlyVisible, CancellationToken cancellationToken)
        {
            var tokenList = $"AdresseImWebAnzeigen={(!currentlyVisible).ToString().ToLowerInvariant()}";
            return await SaveTokenListAsync("Standorte", "STID", stid, tokenList, cancellationToken);
        }

        public async Task<bool> SaveFacilityImageAsync(string eiid, IFormFile image, CancellationToken cancellationToken)
        {
            if (!image.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || image.Length <= 0 || image.Length > 1_000_000) return false;
            Directory.CreateDirectory(_facilityImagesPath);
            foreach (var existing in Directory.EnumerateFiles(_facilityImagesPath, eiid + "*.??g")) File.Delete(existing);
            await using var stream = File.Create(Path.Combine(_facilityImagesPath, eiid + ".jpg"));
            await image.CopyToAsync(stream, cancellationToken);
            return true;
        }

        public Task<bool> DeleteFacilityImageAsync(string eiid, CancellationToken cancellationToken)
        {
            foreach (var existing in Directory.Exists(_facilityImagesPath) ? Directory.EnumerateFiles(_facilityImagesPath, eiid + "*.??g") : []) File.Delete(existing);
            return Task.FromResult(true);
        }

        private async Task<bool> ExecuteVisibilityCommandAsync(string sql, string eiid, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null && result != DBNull.Value && Convert.ToBoolean(result);
        }

        private async Task<bool> SaveTokenListAsync(string tableName, string idName, int id, string tokenList, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("dbo.SaveByTokenList", connection) { CommandType = System.Data.CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@SemikolonSeperatedTokenList", tokenList);
            command.Parameters.AddWithValue("@tableName", tableName);
            command.Parameters.AddWithValue("@idName", idName);
            command.Parameters.AddWithValue("@id", id);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null && result != DBNull.Value && Convert.ToInt32(result) > 0;
        }

        public async Task<StammdatenResponse?> GetAsync(string eiid, string? stuid, CancellationToken cancellationToken)
        {
            return await GetAsync(eiid, stuid, null, cancellationToken);
        }

        public async Task<StammdatenResponse?> GetForUserAsync(string eiid, string? stuid, int beid, CancellationToken cancellationToken)
        {
            return await GetAsync(eiid, stuid, beid, cancellationToken);
        }

        public async Task<StammdatenResponse?> GetForUserAsync(string eiid, string? stuid, int beid, bool isAdmin, CancellationToken cancellationToken)
        {
            return await GetAsync(eiid, stuid, beid, isAdmin, cancellationToken);
        }

        public async Task<StammdatenResponse?> GetFirstForUserAsync(int beid, bool isAdmin, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT TOP (1) ei.EIID
                FROM Einrichtungen ei
                INNER JOIN Traeger tr ON tr.TRID = ei.FKTRID
                INNER JOIN Einrichtungstypen et ON et.ETID = ei.FKETID
                INNER JOIN Standorte st ON st.FKEIID = ei.EIID
                WHERE @isAdmin = 1
                OR EXISTS (
                    SELECT 1
                    FROM IndexBenutzerEinrichtung ibe
                    WHERE ibe.FKEIID = ei.EIID AND ibe.FKBEID = @beid
                )
                OR EXISTS (
                    SELECT 1
                    FROM IndexBenutzerGruppe ibg
                    INNER JOIN IndexBenutzerEinrichtung ibe ON ibe.FKBEID = ibg.FKG
                    WHERE ibg.FKBEID = @beid AND ibe.FKEIID = ei.EIID
                )
                ORDER BY ei.Einrichtungsname, ei.EIID;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@beid", beid);
            command.Parameters.AddWithValue("@isAdmin", isAdmin);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            var eiid = value is null || value == DBNull.Value ? null : Convert.ToString(value);
            _logger.LogInformation("qwStammdaten default lookup: BEID={BEID}, IsAdmin={IsAdmin}, SelectedEIID={EIID}", beid, isAdmin, eiid);
            return string.IsNullOrWhiteSpace(eiid)
                ? null
                : await GetForUserWithLoggingAsync(eiid, beid, isAdmin, cancellationToken);
        }

        public async Task<IReadOnlyList<StammdatenFacilityChoice>> GetFacilitiesForUserAsync(int beid, bool isAdmin, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT DISTINCT ei.EIID, ei.Einrichtungsname, ei.Einrichtungskennzeichen
                FROM Einrichtungen ei
                INNER JOIN Traeger tr ON tr.TRID = ei.FKTRID
                WHERE @isAdmin = 1
                   OR EXISTS (SELECT 1 FROM IndexBenutzerEinrichtung ibe WHERE ibe.FKEIID = ei.EIID AND ibe.FKBEID = @beid)
                   OR EXISTS (
                       SELECT 1
                       FROM IndexBenutzerGruppe ibg
                       INNER JOIN IndexBenutzerEinrichtung ibe ON ibe.FKBEID = ibg.FKG AND ibe.FKEIID = ei.EIID
                       WHERE ibg.FKBEID = @beid)
                ORDER BY ei.Einrichtungsname, ei.EIID;
                """;
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@beid", beid);
            command.Parameters.AddWithValue("@isAdmin", isAdmin);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var choices = new List<StammdatenFacilityChoice>();
            while (await reader.ReadAsync(cancellationToken))
            {
                choices.Add(new StammdatenFacilityChoice
                {
                    EIID = Convert.ToString(reader.GetValue(0)) ?? string.Empty,
                    Name = Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                    FacilityIndicator = reader.IsDBNull(2) ? null : Convert.ToString(reader.GetValue(2))
                });
            }
            return choices;
        }

        public async Task<StammdatenFacilityCreateOptions> GetFacilityCreateOptionsAsync(CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var institutions = new List<StammdatenInstitutionOption>();
            await using (var command = new SqlCommand("SELECT CONVERT(varchar(36), TRID), Traegername FROM Traeger WHERE ISNULL(Gesperrt, 0) = 0 ORDER BY Traegername;", connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    institutions.Add(new StammdatenInstitutionOption(Convert.ToString(reader.GetValue(0)) ?? string.Empty, Convert.ToString(reader.GetValue(1)) ?? string.Empty));
            }

            var facilityTypes = new List<StammdatenFacilityTypeResponse>();
            await using (var command = new SqlCommand("SELECT ETID, Typkennzeichnung FROM Einrichtungstypen WHERE ISNULL(Typkennzeichnung, '') <> '' ORDER BY Reihenfolge, Typkennzeichnung;", connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    facilityTypes.Add(new StammdatenFacilityTypeResponse(Convert.ToInt32(reader.GetValue(0)), Convert.ToString(reader.GetValue(1)) ?? string.Empty));
            }

            var districts = new List<StammdatenDistrictResponse>();
            await using (var command = new SqlCommand("SELECT KRID, Kreis FROM Kreise ORDER BY Reihenfolge, Kreis;", connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    districts.Add(new StammdatenDistrictResponse(Convert.ToInt32(reader.GetValue(0)), Convert.ToString(reader.GetValue(1)) ?? string.Empty));
            }

            return new StammdatenFacilityCreateOptions { Institutions = institutions, FacilityTypes = facilityTypes, Districts = districts };
        }

        public async Task<string> CreateFacilityAsync(StammdatenFacilityCreateInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.City) || input.Capacity < 0 ||
                !Guid.TryParse(input.TraegerId, out var traegerId) || input.FacilityTypeIds.Count == 0 || input.FacilityTypeIds.Any(id => id <= 0) || input.DistrictId <= 0)
                throw new ArgumentException("Die Angaben zur Einrichtung sind unvollständig oder ungültig.");

            var eiid = Guid.NewGuid();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                const string referenceSql = """
                    SELECT
                        CASE WHEN EXISTS (SELECT 1 FROM Traeger WHERE TRID=@trid) THEN 1 ELSE 0 END,
                        CASE WHEN NOT EXISTS (SELECT 1 FROM Einrichtungstypen WHERE ETID IN (SELECT value FROM STRING_SPLIT(@etids, ','))) THEN 0 ELSE 1 END,
                        CASE WHEN EXISTS (SELECT 1 FROM Kreise WHERE KRID=@krid) THEN 1 ELSE 0 END;
                    """;
                await using (var referenceCommand = new SqlCommand(referenceSql, connection, transaction))
                {
                    referenceCommand.Parameters.AddWithValue("@trid", traegerId);
                    referenceCommand.Parameters.AddWithValue("@etids", string.Join(',', input.FacilityTypeIds));
                    referenceCommand.Parameters.AddWithValue("@krid", input.DistrictId);
                    await using var referenceReader = await referenceCommand.ExecuteReaderAsync(cancellationToken);
                    if (!await referenceReader.ReadAsync(cancellationToken) || referenceReader.GetInt32(0) != 1 || referenceReader.GetInt32(1) != 1 || referenceReader.GetInt32(2) != 1)
                        throw new ArgumentException("Eine ausgewählte Referenz ist nicht vorhanden.");
                }

                await using (var facilityCommand = new SqlCommand("dbo.SaveFacility", connection, transaction) { CommandType = System.Data.CommandType.StoredProcedure })
                {
                    facilityCommand.Parameters.AddWithValue("@Einrichtungsname", input.Name.Trim());
                    facilityCommand.Parameters.AddWithValue("@Kurzname", input.Name.Trim().Length > 100 ? input.Name.Trim()[..100] : input.Name.Trim());
                    facilityCommand.Parameters.AddWithValue("@FKKRID", input.DistrictId);
                    facilityCommand.Parameters.AddWithValue("@FKETID", input.FacilityTypeIds[0]);
                    facilityCommand.Parameters.AddWithValue("@Aktenzeichen", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@Einrichtungskennzeichen", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@Platzzahl", input.Capacity);
                    facilityCommand.Parameters.AddWithValue("@VertretenDurch", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@GeoeffnetSeit", DBNull.Value);
                    facilityCommand.Parameters.AddWithValue("@GeschlossenAm", DBNull.Value);
                    facilityCommand.Parameters.AddWithValue("@Anmerkung", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@GeaendertAm", DateTime.Now);
                    facilityCommand.Parameters.AddWithValue("@GeaendertVon", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@erstelltAm", DateTime.Now);
                    facilityCommand.Parameters.AddWithValue("@ErstelltVon", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@Aktenstandort", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@RueckspracheUSID", 0);
                    facilityCommand.Parameters.AddWithValue("@RueckspracheText", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@unchecked", false);
                    facilityCommand.Parameters.AddWithValue("@ParentEIID", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@Traegernummer", 0);
                    facilityCommand.Parameters.AddWithValue("@FKTRID", traegerId);
                    facilityCommand.Parameters.AddWithValue("@UebergeordneterTRID", DBNull.Value);
                    facilityCommand.Parameters.AddWithValue("@Institutskennzahl", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@Koordinierungsmittel", false);
                    facilityCommand.Parameters.AddWithValue("@EIKomplexitaet", 0m);
                    facilityCommand.Parameters.AddWithValue("@PflegeFinderParameter", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@Ueberbelegung", 0);
                    facilityCommand.Parameters.AddWithValue("@Schliessungsgrund", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@EinrichtungsnameZusatz", string.Empty);
                    facilityCommand.Parameters.AddWithValue("@ElektronischerLeistungsnachweis", false);
                    facilityCommand.Parameters.AddWithValue("@Status", 0);
                    facilityCommand.Parameters.AddWithValue("@PlatzzahlGeschlossen", 0);
                    facilityCommand.Parameters.AddWithValue("@EinrichtngsBeschreibung", string.Empty);
                    var eiidParameter = facilityCommand.Parameters.Add("@EIID", System.Data.SqlDbType.UniqueIdentifier);
                    eiidParameter.Direction = System.Data.ParameterDirection.InputOutput;
                    eiidParameter.Value = eiid;
                    await facilityCommand.ExecuteScalarAsync(cancellationToken);
                }

                await using (var typeCommand = new SqlCommand("dbo.SaveFacilityTypes", connection, transaction) { CommandType = System.Data.CommandType.StoredProcedure })
                {
                    typeCommand.Parameters.AddWithValue("@etids", string.Join(',', input.FacilityTypeIds));
                    typeCommand.Parameters.AddWithValue("@eiid", eiid.ToString());
                    await typeCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                const string locationSql = """
                    INSERT INTO Standorte (FKEIID, Strasse, Hausnummer, Hausnummerzusatz, PLZ, Ort, Platzzahl, PlatzzahlMax,
                        Hauptstandort, GeaendertAm, GeaendertVon, erstelltAm, ErstelltVon, AdresseImWebAnzeigen, FKTRID, FKKRID)
                    VALUES (@eiid, @street, @houseNumber, '', @postalCode, @city, @capacity, @capacity,
                        1, @changedAt, '', @createdAt, '', 1, @trid, @krid);
                    """;
                await using (var locationCommand = new SqlCommand(locationSql, connection, transaction))
                {
                    locationCommand.Parameters.AddWithValue("@eiid", eiid);
                    locationCommand.Parameters.AddWithValue("@street", (object?)input.Street?.Trim() ?? DBNull.Value);
                    locationCommand.Parameters.AddWithValue("@houseNumber", (object?)input.HouseNumber?.Trim() ?? DBNull.Value);
                    locationCommand.Parameters.AddWithValue("@postalCode", (object?)input.PostalCode?.Trim() ?? DBNull.Value);
                    locationCommand.Parameters.AddWithValue("@city", input.City.Trim());
                    locationCommand.Parameters.AddWithValue("@capacity", input.Capacity);
                    locationCommand.Parameters.AddWithValue("@changedAt", DateTime.Now);
                    locationCommand.Parameters.AddWithValue("@createdAt", DateTime.Now);
                    locationCommand.Parameters.AddWithValue("@trid", traegerId);
                    locationCommand.Parameters.AddWithValue("@krid", input.DistrictId);
                    await locationCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return eiid.ToString();
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<bool> RemoveFacilityAsync(string eiid, CancellationToken cancellationToken)
        {
            const string sql = "UPDATE Einrichtungen SET Status = 11 WHERE EIID = @eiid;";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        private async Task<StammdatenResponse?> GetForUserWithLoggingAsync(string eiid, int beid, bool isAdmin, CancellationToken cancellationToken)
        {
            var result = await GetForUserAsync(eiid, null, beid, isAdmin, cancellationToken);
            _logger.LogInformation("qwStammdaten facility lookup: BEID={BEID}, IsAdmin={IsAdmin}, EIID={EIID}, Found={Found}, Locations={Locations}", beid, isAdmin, eiid, result is not null, result?.Locations.Count ?? 0);
            return result;
        }

        public async Task<bool> SaveMainLocationContactDataAsync(
            int stid,
            string phone,
            string fax,
            string internet,
            string email,
            string mobile,
            CancellationToken cancellationToken)
        {
            const string sql = "dbo.SaveByTokenList";
            var tokenList = string.Join("/;\\", new[]
            {
                $"Telefon={phone}",
                $"Telefax={fax}",
                $"Internet={internet}",
                $"Email={email}",
                $"Mobil={mobile}"
            });

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@SemikolonSeperatedTokenList", tokenList);
            command.Parameters.AddWithValue("@tableName", "Standorte");
            command.Parameters.AddWithValue("@idName", "STID");
            command.Parameters.AddWithValue("@id", stid);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null && result != DBNull.Value && Convert.ToInt32(result) > 0;
        }

        private async Task<StammdatenResponse?> GetAsync(string eiid, string? stuid, int? beid, CancellationToken cancellationToken)
        {
            return await GetAsync(eiid, stuid, beid, false, cancellationToken);
        }

        private async Task<StammdatenResponse?> GetAsync(string eiid, string? stuid, int? beid, bool isAdmin, CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(eiid, out _)) return null;

            const string sql = """
                SELECT ei.EIID AS FacilityEIID, ei.Einrichtungsname AS FacilityName,
                       ei.Einrichtungskennzeichen AS FacilityIndicator,
                       ei.Status AS FacilityStatus,
                       ei.Einrichtungsbeschreibung AS FacilityDescription,
                       st.STUID AS LocationSTUID, st.Standortname AS LocationName,
                       st.Strasse AS LocationStreet, st.Hausnummer AS LocationHouseNumber,
                       st.HausnummerZusatz AS LocationHouseNumberAddition, st.PLZ AS LocationPostalCode,
                       st.Ort AS LocationCity, st.Platzzahl AS LocationCapacity, st.AdresseImWebAnzeigen AS ShowAddressInWeb,
                       st.Hauptstandort AS IsMainLocation, st.Email AS LocationEmail,
                       st.Internet AS LocationInternet, st.Telefon AS LocationPhone,
                       st.Mobil AS LocationMobile, st.Telefax AS LocationFax, st.STID AS LocationSTID
                FROM Einrichtungen ei
                INNER JOIN Traeger tr ON tr.TRID = ei.FKTRID
                INNER JOIN Einrichtungstypen et ON et.ETID = ei.FKETID
                INNER JOIN Standorte st ON st.FKEIID = ei.EIID
                WHERE ei.EIID = @eiid
                  AND (
                    (
                      ISNULL(tr.Gesperrt, 0) = 0
                      AND ISNULL(tr.ImBuergerPortalSichtbar, 0) = 1
                      AND ei.FKETID IN (SELECT FKETID FROM HTML_EinrichtungstypenExport INNER JOIN HTML_Dokument ON FKHDID = HDID WHERE Exportieren = 1 AND Dokumenttitel = 'Einrichtung')
                      AND NOT ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 0)
                    )
                     OR (
                       @isAdmin = 1
                       OR
                      @beid IS NOT NULL
                      AND EXISTS (
                        SELECT 1 FROM IndexBenutzerEinrichtung ibe
                        WHERE ibe.FKEIID = ei.EIID AND ibe.FKBEID = @beid
                      )
                      OR @beid IS NOT NULL
                      AND EXISTS (
                        SELECT 1 FROM IndexBenutzerGruppe ibg
                        INNER JOIN IndexBenutzerEinrichtung ibe ON ibe.FKBEID = ibg.FKG AND ibe.FKEIID = ei.EIID
                        WHERE ibg.FKBEID = @beid
                      )
                    )
                  )
                ORDER BY CASE WHEN @stuid IS NOT NULL AND st.STUID = @stuid THEN 0 ELSE 1 END,
                         st.Hauptstandort DESC, st.Standortname;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            command.Parameters.AddWithValue("@stuid", (object?)stuid ?? DBNull.Value);
            command.Parameters.AddWithValue("@beid", (object?)beid ?? DBNull.Value);
            command.Parameters.AddWithValue("@isAdmin", isAdmin);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            var response = new StammdatenResponse
            {
                EIID = GetString(reader, "FacilityEIID") ?? string.Empty,
                Name = GetString(reader, "FacilityName") ?? string.Empty,
                FacilityIndicator = GetString(reader, "FacilityIndicator"),
                Status = GetInt(reader, "FacilityStatus") ?? 0,
                Description = GetString(reader, "FacilityDescription"),
                Locations = []
            };

            do
            {
                var location = ReadLocation(reader);
                response.Locations.Add(location);
                if (response.Locations.Count == 1)
                {
                    response.Address = location.Address;
                    response.Contacts = location.Contacts;
                }
            }
            while (await reader.ReadAsync(cancellationToken));

            await reader.DisposeAsync();

            response.Attachments = await GetAttachmentsAsync(response.EIID, connection, cancellationToken);
            response.ImageFileName = GetImageFileName(response.EIID);
            return response;
        }

        public async Task<(string FileName, byte[] Content, string ContentType)?> GetImageAsync(
            string eiid,
            CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(eiid, out _)) return null;

            var fileName = GetImageFileName(eiid);
            if (fileName is null) return null;

            var path = Path.Combine(_facilityImagesPath, fileName);
            if (!File.Exists(path)) return null;

            return (fileName, await File.ReadAllBytesAsync(path, cancellationToken), GetContentType(fileName));
        }

        public async Task<(string FileName, byte[] Content)?> GetAttachmentAsync(
            string eiid,
            string attachmentId,
            CancellationToken cancellationToken)
        {
            return await GetAttachmentAsync(eiid, attachmentId, 102, cancellationToken);
        }

        public async Task<(string FileName, byte[] Content)?> GetAttachmentAsync(
            string eiid,
            string attachmentId,
            int attachmentType,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT TOP 1 DateinameOriginal, DateiInhalt
                FROM DateiAnhaengeStammdaten
                WHERE ID = @id AND FKID = @eiid AND AnhangTyp = @type;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", attachmentId);
            command.Parameters.AddWithValue("@eiid", eiid);
            command.Parameters.AddWithValue("@type", attachmentType);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(1)) return null;

            return (Convert.ToString(reader.GetValue(0)) ?? "download", (byte[])reader.GetValue(1));
        }

        private async Task<List<StammdatenAttachmentResponse>> GetAttachmentsAsync(
            string eiid,
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT ID, DateinameOriginal
                FROM DateiAnhaengeStammdaten
                WHERE FKID = @eiid AND AnhangTyp = 102
                ORDER BY GeaendertAm, DateinameOriginal;
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@eiid", eiid);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var attachments = new List<StammdatenAttachmentResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                attachments.Add(new StammdatenAttachmentResponse
                {
                    Id = Convert.ToString(reader.GetValue(0)) ?? string.Empty,
                    Filename = Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                    Type = reader.FieldCount > 2 && !reader.IsDBNull(2) ? Convert.ToInt32(reader.GetValue(2)) : 102,
                    Status = reader.FieldCount > 3 && !reader.IsDBNull(3) ? Convert.ToInt32(reader.GetValue(3)) : 0
                });
            }

            return attachments;
        }

        private string? GetImageFileName(string eiid)
        {
            if (!Guid.TryParse(eiid, out _)) return null;

            var directory = Path.GetFullPath(_facilityImagesPath);
            if (!Directory.Exists(directory)) return null;

            return Directory.EnumerateFiles(directory, eiid + "*.??g")
                .Select(Path.GetFileName)
                .Where(fileName => fileName is not null)
                .Select(fileName => fileName!)
                .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static string GetContentType(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }


        private static StammdatenLocationResponse ReadLocation(SqlDataReader reader)
        {
            var contacts = new List<StammdatenContactResponse>();
            AddContact(contacts, "E-Mail", reader, "LocationEmail");
            AddContact(contacts, "Internet", reader, "LocationInternet");
            AddContact(contacts, "Telefon", reader, "LocationPhone");
            AddContact(contacts, "Mobil", reader, "LocationMobile");
            AddContact(contacts, "Telefax", reader, "LocationFax");

            var address = !GetBool(reader, "ShowAddressInWeb")
                ? null
                : string.Join(" ", new[] { "LocationStreet", "LocationHouseNumber", "LocationHouseNumberAddition", "LocationPostalCode", "LocationCity" }
                    .Select(columnName => GetString(reader, columnName))
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

            return new StammdatenLocationResponse
            {
                STID = GetInt(reader, "LocationSTID"),
                STUID = GetString(reader, "LocationSTUID"),
                Name = GetString(reader, "LocationName") ?? string.Empty,
                Description = null,
                Address = address,
                Street = GetString(reader, "LocationStreet"),
                HouseNumber = GetString(reader, "LocationHouseNumber"),
                HouseNumberAddition = GetString(reader, "LocationHouseNumberAddition"),
                PostalCode = GetString(reader, "LocationPostalCode"),
                City = GetString(reader, "LocationCity"),
                Capacity = GetInt(reader, "LocationCapacity"),
                IsMainLocation = GetBool(reader, "IsMainLocation"),
                ShowAddressInWeb = GetBool(reader, "ShowAddressInWeb"),
                Phone = GetString(reader, "LocationPhone"),
                Mobile = GetString(reader, "LocationMobile"),
                Fax = GetString(reader, "LocationFax"),
                Email = GetString(reader, "LocationEmail"),
                Internet = GetString(reader, "LocationInternet"),
                Contacts = contacts
            };
        }

        private static string? GetString(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
        }

        private static int? GetInt(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static bool GetBool(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal));
        }

        private static void AddContact(List<StammdatenContactResponse> contacts, string label, SqlDataReader reader, string columnName)
        {
            var value = GetString(reader, columnName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                contacts.Add(new StammdatenContactResponse { Label = label, Value = value });
            }
        }
    }
}
