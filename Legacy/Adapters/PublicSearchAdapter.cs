using Microsoft.Data.SqlClient;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Legacy.Adapters
{
    public class PublicSearchAdapter
    {
        private const string Query = """
            SELECT ei.EIID, st.STUID, ei.Einrichtungsname, st.Standortname, ei.Einrichtungskennzeichen,
                   et.Einrichtungstyp, et.Oberkategorie, et.Typkennzeichnung, kr.Kreis,
                   ISNULL(st.Hauptstandort, 0) AS Hauptstandort, st.Strasse,
                   st.Hausnummer + ' ' + st.HausnummerZusatz AS Hausnummer, st.PLZ, st.Ort, st.AdresseImWebAnzeigen
            FROM Einrichtungen ei
            INNER JOIN Traeger tr ON tr.TRID = ei.FKTRID
            INNER JOIN Einrichtungstypen et ON et.ETID = ei.FKETID
            INNER JOIN Standorte st ON st.FKEIID = ei.EIID
            LEFT JOIN Kreise kr ON kr.KRID = ISNULL(st.FKKRID, ei.FKKRID)
            WHERE (ei.Status = 5 AND ISNULL(st.StandortOhneLeistung, 0) = 0
                   AND ISNULL(ei.GeschlossenAm, GETDATE() + 1) > GETDATE()
                   AND ISNULL(st.GeschlossenAmStandort, GETDATE() + 1) > GETDATE()
                   OR ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 1))
              AND ISNULL(tr.Gesperrt, 0) = 0 AND ISNULL(tr.ImBuergerPortalSichtbar, 0) = 1
              AND ei.FKETID IN (SELECT FKETID FROM HTML_EinrichtungstypenExport INNER JOIN HTML_Dokument ON FKHDID = HDID WHERE Exportieren = 1 AND Dokumenttitel = 'Einrichtung')
              AND NOT ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 0)
              AND (@typId IS NULL OR et.Typkennzeichnung = @typId)
              AND (@kreisId IS NULL OR kr.Kurzname = @kreisId)
            ORDER BY ei.Einrichtungsname, ei.EIID, st.Hauptstandort DESC;
            """;
        private readonly string _connectionString;

        public PublicSearchAdapter(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("TOPqwWeb") is { Length: > 0 } connectionString
                ? connectionString
                : throw new InvalidOperationException("ConnectionStrings:TOPqwWeb ist leer. Konfigurieren Sie den Wert über User Secrets oder die Umgebungsvariable ConnectionStrings__TOPqwWeb.");
        }

        public async Task<AuflistungResponse> GetAuflistungAsync(string type, string? typeId, string? districtId, CancellationToken cancellationToken)
        {
            var rows = new List<FacilityRow>();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(Query, connection);
            command.Parameters.AddWithValue("@typId", (object?)typeId ?? DBNull.Value);
            command.Parameters.AddWithValue("@kreisId", (object?)districtId ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) rows.Add(FacilityRow.Create(reader));

            var mainFacilities = rows.Where(row => row.IsMainLocation).ToList();
            var categories = mainFacilities.GroupBy(row => type == "Typ" ? row.Category : row.District)
                .Select(category => new AuflistungCategoryResponse
                {
                    Name = category.Key,
                    Groups = category.GroupBy(row => type == "Typ" ? row.FacilityType : "Einrichtungen")
                        .Select(group => new AuflistungGroupResponse { Name = group.Key, Facilities = group.Select(row => row.ToResponse(rows)).ToList() }).ToList()
                }).ToList();
            return new AuflistungResponse { Title = type == "Typ" ? "Leistungsangebote nach Art der Leistung" : "Leistungsangebote nach Kreis", Categories = categories };
        }

        public async Task<PublicSearchResponse> SearchAsync(PublicSearchRequest request, CancellationToken cancellationToken)
        {
            const string searchSql = """
                SELECT TOP 51 ei.EIID, st.STUID, ei.Einrichtungsname, et.Einrichtungstyp,
                       ei.Einrichtungskennzeichen, st.Standortname, st.Strasse,
                       st.Hausnummer + ' ' + st.HausnummerZusatz AS Hausnummer,
                       st.PLZ, st.Ort, st.AdresseImWebAnzeigen, ISNULL(st.Hauptstandort, 0) AS Hauptstandort,
                       dbo.GetGeoDistance(origin.lat, origin.lon, ISNULL(st.GeoLat, geo.lat), ISNULL(st.GeoLon, geo.lon)) AS Distanz
                FROM Einrichtungen ei
                INNER JOIN Traeger tr ON tr.TRID = ei.FKTRID
                INNER JOIN Einrichtungstypen et ON et.ETID = ei.FKETID
                INNER JOIN Standorte st ON st.FKEIID = ei.EIID
                LEFT JOIN GEO_Locations geo ON geo.PLZ = st.PLZ
                OUTER APPLY dbo.GetCurrPlatzmeldungOrEmpty(ei.EIID) cpm
                CROSS APPLY (SELECT lat = (SELECT lat FROM GEO_Locations WHERE PLZ = @postalCode),
                                    lon = (SELECT lon FROM GEO_Locations WHERE PLZ = @postalCode)) origin
                WHERE (ei.Status = 5 AND ISNULL(st.StandortOhneLeistung, 0) = 0
                       AND ISNULL(ei.GeschlossenAm, GETDATE() + 1) > GETDATE()
                       AND ISNULL(st.GeschlossenAmStandort, GETDATE() + 1) > GETDATE()
                       OR ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 1))
                  AND ISNULL(tr.Gesperrt, 0) = 0
                  AND ISNULL(tr.ImBuergerPortalSichtbar, 0) = 1
                  AND ei.FKETID IN (SELECT FKETID FROM HTML_EinrichtungstypenExport INNER JOIN HTML_Dokument ON FKHDID = HDID WHERE Exportieren = 1 AND Dokumenttitel = 'Einrichtung')
                  AND NOT ei.EIID IN (SELECT FKEIID FROM SearchFacilityOverride WHERE ShowInSearch = 0)
                  AND (@facilityType = 0 OR ei.FKETID = @facilityType)
                  AND (@withFreePlaces = 0 OR ISNULL(cpm.PMID, 0) <> 0)
                  AND (@featureIds = ''
                       OR EXISTS (SELECT 1 FROM EinrichtungsMerkmale em INNER JOIN dbo.SplitString(@featureIds, ',') f ON TRY_CONVERT(int, f.s) = em.FKLMID WHERE em.FKEIID = ei.EIID))
                  AND (@postalCode = '' OR (origin.lat IS NOT NULL AND dbo.GetGeoDistance(origin.lat, origin.lon, ISNULL(st.GeoLat, geo.lat), ISNULL(st.GeoLon, geo.lon)) <= @distanceKm))
                  AND (@searchValue = '' OR ei.Einrichtungsname LIKE '%' + @searchValue + '%'
                       OR ISNULL(ei.Kurzname, '') LIKE '%' + @searchValue + '%'
                       OR ISNULL(ei.Einrichtungskennzeichen, '') LIKE '%' + @searchValue + '%'
                       OR ISNULL(ei.Aktenzeichen, '') LIKE '%' + @searchValue + '%'
                       OR ISNULL(st.Standortname, '') LIKE '%' + @searchValue + '%'
                       OR ISNULL(st.Bezeichnung, '') LIKE '%' + @searchValue + '%')
                ORDER BY CASE WHEN @postalCode = '' THEN 0 ELSE dbo.GetGeoDistance(origin.lat, origin.lon, ISNULL(st.GeoLat, geo.lat), ISNULL(st.GeoLon, geo.lon)) END,
                         ei.Einrichtungsname, ei.EIID, st.Hauptstandort DESC;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(searchSql, connection);
            command.Parameters.AddWithValue("@searchValue", request.SearchValue.Trim());
            command.Parameters.AddWithValue("@facilityType", request.FacilityType);
            command.Parameters.AddWithValue("@postalCode", request.PostalCode.Trim());
            command.Parameters.AddWithValue("@distanceKm", request.DistanceKm > 0 ? request.DistanceKm : int.MaxValue);
            command.Parameters.AddWithValue("@withFreePlaces", request.WithFreePlaces);
            command.Parameters.AddWithValue("@featureIds", string.Join(',', request.FacilityFeatures.Distinct()));
            var results = new List<PublicSearchResultResponse>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var address = reader.IsDBNull(10) || !Convert.ToBoolean(reader.GetValue(10))
                        ? null
                        : string.Join(" ", Enumerable.Range(6, 4)
                            .Select(index => reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index)))
                            .Where(value => !string.IsNullOrWhiteSpace(value)));
                    results.Add(new PublicSearchResultResponse
                    {
                        EIID = Convert.ToString(reader.GetValue(0)) ?? string.Empty,
                        STUID = reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1)),
                        Name = reader.IsDBNull(5) || string.IsNullOrWhiteSpace(Convert.ToString(reader.GetValue(5)))
                            ? Convert.ToString(reader.GetValue(2)) ?? string.Empty
                            : $"{Convert.ToString(reader.GetValue(2))} - {Convert.ToString(reader.GetValue(5))}",
                        FacilityType = reader.IsDBNull(3) ? null : Convert.ToString(reader.GetValue(3)),
                        FacilityIndicator = reader.IsDBNull(4) ? null : Convert.ToString(reader.GetValue(4)),
                        Address = address,
                        IsMainLocation = !reader.IsDBNull(11) && Convert.ToBoolean(reader.GetValue(11)),
                        DistanceKm = reader.IsDBNull(12) ? null : Convert.ToDecimal(reader.GetValue(12))
                    });
                }
            }

            var facilityTypes = new List<PublicSearchTypeResponse>();
            await using (var typeCommand = new SqlCommand("""
                SELECT DISTINCT a.ETID, ISNULL(a.Kurzbezeichnung, ISNULL(a.Typkennzeichnung, '')) + ' : ' + ISNULL(a.Einrichtungstyp, '') AS TypName, a.Oberkategorie
                FROM Einrichtungstypen a
                INNER JOIN HTML_EinrichtungstypenExport ex ON a.ETID = ex.FKETID
                INNER JOIN HTML_Dokument doc ON doc.HDID = ex.FKHDID
                WHERE ex.Exportieren = 1 AND doc.DokumentTitel = 'Einrichtung'
                  AND EXISTS (SELECT 1 FROM Einrichtungen e WHERE e.FKETID = a.ETID AND ISNULL(e.GeschlossenAm, GETDATE() + 1) > GETDATE())
                ORDER BY TypName;
                """, connection))
            await using (var typeReader = await typeCommand.ExecuteReaderAsync(cancellationToken))
            {
                while (await typeReader.ReadAsync(cancellationToken))
                {
                    facilityTypes.Add(new PublicSearchTypeResponse
                    {
                        Id = Convert.ToInt32(typeReader.GetValue(0)),
                        Name = Convert.ToString(typeReader.GetValue(1)) ?? string.Empty,
                        Category = typeReader.IsDBNull(2) ? null : Convert.ToString(typeReader.GetValue(2))
                    });
                }
            }

            var facilityFeatures = new List<PublicSearchFeatureResponse>();
            await using (var featureCommand = new SqlCommand("""
                SELECT LMID, Beschreibung
                FROM Leistungsmerkmal
                WHERE ISNULL(Beschreibung, '') <> ''
                  AND LOWER(Beschreibung) <> 'kein merkmal'
                ORDER BY Beschreibung;
                """, connection))
            await using (var featureReader = await featureCommand.ExecuteReaderAsync(cancellationToken))
            {
                while (await featureReader.ReadAsync(cancellationToken))
                {
                    facilityFeatures.Add(new PublicSearchFeatureResponse
                    {
                        Id = Convert.ToInt32(featureReader.GetValue(0)),
                        Name = Convert.ToString(featureReader.GetValue(1)) ?? string.Empty
                    });
                }
            }

            return new PublicSearchResponse
            {
                Results = results.Take(50).ToList(),
                HasMoreResults = results.Count > 50,
                FacilityTypes = facilityTypes,
                Distances = [1, 2, 5, 10, 15, 25, 50],
                FacilityFeatures = facilityFeatures
            };
        }

        private sealed record FacilityRow(string EIID, string? STUID, string Name, string FacilityType, string Category, string District, bool IsMainLocation, string? Indicator, string? Address)
        {
            private static string? GetNullableString(SqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetValue(index).ToString();

            public static FacilityRow Create(SqlDataReader reader)
            {
                var eiid = GetNullableString(reader, 0) ?? string.Empty;
                var stuid = GetNullableString(reader, 1);
                var name = GetNullableString(reader, 2) ?? string.Empty;
                var facilityType = GetNullableString(reader, 5) ?? string.Empty;
                var category = GetNullableString(reader, 6) ?? string.Empty;
                var district = reader.IsDBNull(8) ? "Kreis" : GetNullableString(reader, 8) ?? "Kreis";
                static bool GetBooleanLike(SqlDataReader r, int idx)
                {
                    if (r.IsDBNull(idx)) return false;
                    var val = r.GetValue(idx);
                    return val switch
                    {
                        bool b => b,
                        byte bt => bt == 1,
                        short s => s == 1,
                        int i => i == 1,
                        long l => l == 1L,
                        string str => str == "1" || string.Equals(str, "true", StringComparison.OrdinalIgnoreCase),
                        _ => false
                    };
                }

                var isMain = GetBooleanLike(reader, 9);
                var indicator = GetNullableString(reader, 4);
                var address = (reader.IsDBNull(14) || !reader.GetBoolean(14))
                    ? null
                    : string.Join(" ", Enumerable.Range(10, 4).Select(index => GetNullableString(reader, index)).Where(value => !string.IsNullOrWhiteSpace(value)));

                return new FacilityRow(eiid, stuid, name, facilityType, category, district, isMain, indicator, address);
            }

            public AuflistungFacilityResponse ToResponse(IEnumerable<FacilityRow> rows) => new() { EIID = EIID, Name = Name, FacilityIndicator = Indicator, Address = Address, AdditionalLocations = rows.Where(row => row.EIID == EIID && !row.IsMainLocation).Select(row => new AuflistungFacilityResponse { EIID = row.EIID, STUID = row.STUID, Name = row.Name, FacilityIndicator = row.Indicator, Address = row.Address }).ToList() };
        }
    }
}
