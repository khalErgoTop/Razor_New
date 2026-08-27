using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Legacy.Bridge;
using TOPqwWeb_Core.Services.Public;

namespace TOPqwWeb_Core.Services.Auth
{
    public sealed class QwStammdatenPageService
    {
        private const string AdminRechte = "ea0adb75-cea8-47a9-943c-f6a34f0a943e";
        private const string Stammdaten_Einrichtung_Bearbeiten = "48f0f47a-c9f8-42b8-8855-44b25dc54961";
        private const string Stammdaten_Einrichtung_Kontaktdaten_Bearbeiten = "9F5F4AC3-C052-4B59-9283-5E035401B9CF";
        private const string Stammdaten_Einrichtung_Attachments_Anzeigen = "bc728ced-26b2-4eed-bb99-d90d345804d7";
        private const string Stammdaten_Einrichtung_Attachments_Bearbeiten = "7dd57851-f102-43c1-99f2-58ccce67f08f";
        private const string Stammdaten_Einrichtung_Dokumente_Anzeigen = "F4264D50-A716-4714-8606-7ABD9BE31847";
        private const string Stammdaten_Einrichtung_Dokumente_Bearbeiten = "02E1FD33-BE9C-4EF6-9667-9B1AF37A3F10";
        private const string Stammdaten_Einrichtung_Eigene_Anzeigen = "07f98ddf-7bcb-4b12-aac4-1200aa1e071e";
        private const string Stammdaten_Einrichtung_Suche_Verbergen = "22427be0-1776-44f5-8c5c-e9b16d4e09d3";
        private const string Stammdaten_Einrichtung_Bilder_Anzeigen = "3D1FB251-53F7-40B6-A078-BEF077E4D953";
        private const string Stammdaten_Standort_Hinzufuegen = "42502EB1-0758-4F62-9059-93DB259D01D4";
        private const string Stammdaten_BesucherZaehler_anzeigen = "46BE18E8-AE12-4D95-9E28-C0CFEDC3DE0";

        private readonly StammdatenPageService _stammdatenPageService;
    private readonly PublicStammdatenAdapter _publicStammdatenAdapter;
        private readonly LegacyPermissionService _permissionService;
        private readonly CurrentUserAccessor _currentUserAccessor;
        private readonly ILogger<QwStammdatenPageService> _logger;

    public QwStammdatenPageService(StammdatenPageService stammdatenPageService, PublicStammdatenAdapter publicStammdatenAdapter, LegacyPermissionService permissionService, CurrentUserAccessor currentUserAccessor, ILogger<QwStammdatenPageService> logger)
        {
            _stammdatenPageService = stammdatenPageService;
        _publicStammdatenAdapter = publicStammdatenAdapter;
            _permissionService = permissionService;
            _currentUserAccessor = currentUserAccessor;
            _logger = logger;
        }

        public async Task<QwStammdatenResponse?> GetAsync(string eiid, string? stuid, CancellationToken cancellationToken)
        {
            return await GetAsync(eiid, stuid, false, cancellationToken);
        }

        private async Task<QwStammdatenResponse?> GetAsync(string eiid, string? stuid, bool isAdmin, CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            _logger.LogInformation("qwStammdaten access: Authenticated={Authenticated}, BEID={BEID}, EIID={EIID}, STUID={STUID}", _currentUserAccessor.IsAuthenticated, beid, eiid, stuid);
            if (!beid.HasValue)
            {
                _logger.LogWarning("qwStammdaten denied because BEID is missing.");
                return null;
            }
            var permissions = await _permissionService.GetPermissionsAsync(beid.Value,
                [Stammdaten_Einrichtung_Eigene_Anzeigen, Stammdaten_Einrichtung_Bearbeiten, Stammdaten_Einrichtung_Kontaktdaten_Bearbeiten, Stammdaten_Einrichtung_Attachments_Anzeigen, Stammdaten_Einrichtung_Attachments_Bearbeiten, Stammdaten_Einrichtung_Dokumente_Anzeigen, Stammdaten_Einrichtung_Dokumente_Bearbeiten, Stammdaten_Einrichtung_Suche_Verbergen, Stammdaten_Einrichtung_Bilder_Anzeigen, Stammdaten_Standort_Hinzufuegen, Stammdaten_BesucherZaehler_anzeigen], cancellationToken);
            _logger.LogInformation("qwStammdaten permissions: BEID={BEID}, OwnFacilities={OwnFacilities}, All={All}", beid, permissions[Stammdaten_Einrichtung_Eigene_Anzeigen], string.Join(",", permissions.Select(pair => $"{pair.Key}={pair.Value}")));
            if (!permissions[Stammdaten_Einrichtung_Eigene_Anzeigen])
            {
                _logger.LogWarning("qwStammdaten denied because own-facility permission is false. BEID={BEID}", beid);
                return null;
            }
        var facility = await _publicStammdatenAdapter.GetForUserAsync(eiid, stuid, beid.Value, isAdmin, cancellationToken);
            if (facility is null)
            {
                _logger.LogWarning("qwStammdaten denied because facility was not found or is not assigned. BEID={BEID}, EIID={EIID}", beid, eiid);
                return null;
            }
            var documents = permissions[Stammdaten_Einrichtung_Dokumente_Anzeigen]
                ? await _publicStammdatenAdapter.GetDocumentsAsync(eiid, cancellationToken)
                : [];
            var visitorData = permissions.ContainsKey(Stammdaten_BesucherZaehler_anzeigen) && permissions[Stammdaten_BesucherZaehler_anzeigen]
                ? await _publicStammdatenAdapter.GetVisitorDataAsync(eiid, cancellationToken)
                : (0, new List<VisitorChartPoint>());
            return new QwStammdatenResponse
            {
                Facility = facility,
                CanEditFacility = permissions[Stammdaten_Einrichtung_Bearbeiten],
                CanEditContactData = permissions[Stammdaten_Einrichtung_Kontaktdaten_Bearbeiten],
                CanViewAttachments = permissions[Stammdaten_Einrichtung_Attachments_Anzeigen],
                CanEditAttachments = permissions[Stammdaten_Einrichtung_Attachments_Bearbeiten],
                CanViewDocuments = permissions[Stammdaten_Einrichtung_Dokumente_Anzeigen],
                CanEditDocuments = permissions[Stammdaten_Einrichtung_Dokumente_Bearbeiten],
                CanViewOwnFacilities = permissions[Stammdaten_Einrichtung_Eigene_Anzeigen]
                ,CanChangeFacilityVisibility = permissions[Stammdaten_Einrichtung_Suche_Verbergen]
                ,CanEditImage = permissions[Stammdaten_Einrichtung_Bearbeiten] && permissions[Stammdaten_Einrichtung_Bilder_Anzeigen]
                ,CanAddLocation = permissions[Stammdaten_Standort_Hinzufuegen]
                ,CanEditLocations = permissions[Stammdaten_Einrichtung_Bearbeiten]
                ,FacilityVisibleInSearch = true
                ,Documents = documents
                ,VisitorCount = visitorData.Item1
                ,VisitorChart = visitorData.Item2
                ,FreePlaces = await _publicStammdatenAdapter.GetFreePlacesAsync(eiid, cancellationToken)
            };
        }

        public async Task<QwStammdatenResponse?> GetFirstForUserAsync(CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            if (!beid.HasValue)
            {
                return null;
            }

            var permissions = await _permissionService.GetPermissionsAsync(
                beid.Value,
                [Stammdaten_Einrichtung_Eigene_Anzeigen],
                cancellationToken);
            if (!permissions[Stammdaten_Einrichtung_Eigene_Anzeigen])
            {
                return null;
            }

            var isAdmin = await _permissionService.HasPermissionAsync(beid.Value, AdminRechte, cancellationToken);
            var facility = await _publicStammdatenAdapter.GetFirstForUserAsync(beid.Value, isAdmin, cancellationToken);
            if (facility is null)
            {
                return null;
            }

            return await GetAsync(facility.EIID, facility.Locations.FirstOrDefault()?.STUID, isAdmin, cancellationToken);
        }

        public async Task<bool> SaveAttachmentAsync(string eiid, IFormFile file, bool document, int documentType, CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            var permission = document ? Stammdaten_Einrichtung_Dokumente_Bearbeiten : Stammdaten_Einrichtung_Attachments_Bearbeiten;
            if (!beid.HasValue || !await _permissionService.HasPermissionAsync(beid.Value, permission, cancellationToken)) return false;
            if (!await HasFacilityAccessAsync(eiid, beid.Value, cancellationToken)) return false;
            var type = document ? documentType : 102;
            if (document && type is not (0 or 1 or 2 or 22)) return false;
            return await _publicStammdatenAdapter.SaveAttachmentAsync(eiid, file, type, 5, cancellationToken);
        }

        public async Task<bool> DeleteAttachmentAsync(string eiid, string attachmentId, bool document, CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            var permission = document ? Stammdaten_Einrichtung_Dokumente_Bearbeiten : Stammdaten_Einrichtung_Attachments_Bearbeiten;
            if (!beid.HasValue || !await _permissionService.HasPermissionAsync(beid.Value, permission, cancellationToken)) return false;
            if (!await HasFacilityAccessAsync(eiid, beid.Value, cancellationToken)) return false;
            return await _publicStammdatenAdapter.DeleteAttachmentAsync(eiid, attachmentId, cancellationToken);
        }

        public async Task<bool> AddLocationAsync(string eiid, StammdatenLocationInput location, CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            if (!beid.HasValue || !await _permissionService.HasPermissionAsync(beid.Value, Stammdaten_Standort_Hinzufuegen, cancellationToken)) return false;
            if (!await HasFacilityAccessAsync(eiid, beid.Value, cancellationToken)) return false;
            if (string.IsNullOrWhiteSpace(location.City)) return false;
            return await _publicStammdatenAdapter.AddLocationAsync(eiid, location, cancellationToken);
        }

        public async Task<bool> SaveLocationAsync(string eiid, string stuid, StammdatenLocationInput location, CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            if (!beid.HasValue || !await _permissionService.HasPermissionAsync(beid.Value, Stammdaten_Einrichtung_Bearbeiten, cancellationToken)) return false;
            if (!Guid.TryParse(stuid, out _)) return false;
            if (!await HasLocationAccessAsync(eiid, stuid, beid.Value, cancellationToken)) return false;
            return await _publicStammdatenAdapter.SaveLocationAsync(eiid, stuid, location, cancellationToken);
        }

        public async Task<bool> SaveFacilityDescriptionAsync(string eiid, string description, CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            if (!beid.HasValue || !await _permissionService.HasPermissionAsync(beid.Value, Stammdaten_Einrichtung_Bearbeiten, cancellationToken)) return false;
            if (!await HasFacilityAccessAsync(eiid, beid.Value, cancellationToken)) return false;
            return await _publicStammdatenAdapter.SaveFacilityDescriptionAsync(eiid, description, cancellationToken);
        }

        public async Task<bool> SaveFreePlacesAsync(string eiid, IReadOnlyList<FreePlaceQuestionInput> questions, CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            if (!beid.HasValue || !await _permissionService.HasPermissionAsync(beid.Value, Stammdaten_Einrichtung_Bearbeiten, cancellationToken)) return false;
            if (!await HasFacilityAccessAsync(eiid, beid.Value, cancellationToken)) return false;
            return await _publicStammdatenAdapter.SaveFreePlacesAsync(eiid, questions, cancellationToken);
        }

        public async Task<bool> ToggleFacilityVisibilityAsync(string eiid, CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            if (!beid.HasValue || !await _permissionService.HasPermissionAsync(beid.Value, Stammdaten_Einrichtung_Suche_Verbergen, cancellationToken)) return false;
            if (!await HasFacilityAccessAsync(eiid, beid.Value, cancellationToken)) return false;
            return await _publicStammdatenAdapter.ToggleFacilityVisibilityAsync(eiid, cancellationToken);
        }

        public async Task<bool> ToggleLocationVisibilityAsync(string eiid, int stid, bool currentlyVisible, CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            if (!beid.HasValue || !await _permissionService.HasPermissionAsync(beid.Value, Stammdaten_Einrichtung_Bearbeiten, cancellationToken)) return false;
            if (!await HasLocationAccessAsync(eiid, stid, beid.Value, cancellationToken)) return false;
            return await _publicStammdatenAdapter.ToggleLocationVisibilityAsync(stid, currentlyVisible, cancellationToken);
        }

        public async Task<bool> SaveFacilityImageAsync(string eiid, IFormFile image, CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            if (!beid.HasValue || !await _permissionService.HasPermissionAsync(beid.Value, Stammdaten_Einrichtung_Bearbeiten, cancellationToken)) return false;
            if (!await HasFacilityAccessAsync(eiid, beid.Value, cancellationToken)) return false;
            return await _publicStammdatenAdapter.SaveFacilityImageAsync(eiid, image, cancellationToken);
        }

        public async Task<bool> DeleteFacilityImageAsync(string eiid, CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            if (!beid.HasValue || !await _permissionService.HasPermissionAsync(beid.Value, Stammdaten_Einrichtung_Bearbeiten, cancellationToken)) return false;
            if (!await HasFacilityAccessAsync(eiid, beid.Value, cancellationToken)) return false;
            return await _publicStammdatenAdapter.DeleteFacilityImageAsync(eiid, cancellationToken);
        }

        public async Task<bool> SaveMainLocationContactDataAsync(
            string eiid,
            string? stuid,
            string phone,
            string fax,
            string internet,
            string email,
            string mobile,
            CancellationToken cancellationToken)
        {
            var beid = _currentUserAccessor.BEID;
            if (!beid.HasValue) return false;

            var allowed = await _permissionService.HasPermissionAsync(
                beid.Value,
                Stammdaten_Einrichtung_Kontaktdaten_Bearbeiten,
                cancellationToken);
            if (!allowed) return false;

            var facility = await _publicStammdatenAdapter.GetForUserAsync(eiid, stuid, beid.Value, cancellationToken);
            var mainLocation = facility?.Locations.FirstOrDefault(location => location.IsMainLocation)
                ?? facility?.Locations.FirstOrDefault();
            if (mainLocation?.STID is not int stid) return false;

            return await _publicStammdatenAdapter.SaveMainLocationContactDataAsync(
                stid, phone, fax, internet, email, mobile, cancellationToken);
        }

        private async Task<bool> HasFacilityAccessAsync(string eiid, int beid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid)) return false;
            var facility = await _publicStammdatenAdapter.GetForUserAsync(eiid, null, beid, _currentUserAccessor.IsAdmin, cancellationToken);
            return facility is not null;
        }

        private async Task<bool> HasLocationAccessAsync(string? eiid, string stuid, int beid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(eiid) || string.IsNullOrWhiteSpace(stuid)) return false;
            var facility = await _publicStammdatenAdapter.GetForUserAsync(eiid, stuid, beid, _currentUserAccessor.IsAdmin, cancellationToken);
            return facility?.Locations.Any(location => string.Equals(location.STUID, stuid, StringComparison.OrdinalIgnoreCase)) == true;
        }

        private async Task<bool> HasLocationAccessAsync(string? eiid, int stid, int beid, CancellationToken cancellationToken)
        {
            if (stid <= 0) return false;
            var facility = await _publicStammdatenAdapter.GetForUserAsync(eiid ?? string.Empty, null, beid, _currentUserAccessor.IsAdmin, cancellationToken);
            return facility?.Locations.Any(location => location.STID == stid) == true;
        }
    }
}
