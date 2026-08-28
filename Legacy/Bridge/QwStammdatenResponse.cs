namespace TOPqwWeb_Core.Legacy.Bridge;

public sealed class QwStammdatenResponse
{
    public StammdatenResponse Facility { get; init; } = new();
    public bool CanEditFacility { get; init; }
    public bool CanEditContactData { get; init; }
    public bool CanViewAttachments { get; init; }
    public bool CanEditAttachments { get; init; }
    public bool CanViewDocuments { get; init; }
    public bool CanEditDocuments { get; init; }
    public bool CanEditFreePlaces { get; init; }
    public bool ShowFreePlaces { get; init; }
    public bool ShowFacilityDescription { get; init; }
    public bool CanRemoveFacility { get; init; }
    public bool CanCreateFacility { get; init; }
    public bool MultipleFacilityTypesEnabled { get; init; }
    public bool CanEditInstitutionContactData { get; init; }
    public InstitutionContactDataResponse? Institution { get; init; }
    public IReadOnlyList<FacilityRepresentativeResponse> Representatives { get; init; } = [];
    public IReadOnlyList<FacilityFeatureResponse> Features { get; init; } = [];
    public IReadOnlyList<FacilityFeatureResponse> AvailableFeatures { get; init; } = [];
    public bool CanEditFeatures { get; init; }
    public bool CanViewCosts { get; init; }
    public bool CanViewOwnFacilities { get; init; }
    public bool CanChangeFacilityVisibility { get; init; }
    public bool CanEditImage { get; init; }
    public bool CanAddLocation { get; init; }
    public bool CanEditLocations { get; init; }
    public bool FacilityVisibleInSearch { get; init; }
    public IReadOnlyList<StammdatenAttachmentResponse> Documents { get; init; } = [];
    public FreePlaceReportingResponse? FreePlaces { get; init; }
    public int? VisitorCount { get; init; }
    public IReadOnlyList<VisitorChartPoint> VisitorChart { get; init; } = [];
}

public sealed record FacilityRepresentativeResponse(string Name, string Email, string Username);

public sealed record FacilityFeatureResponse(int Id, string Label, string Description, string Category);

public sealed class InstitutionContactDataResponse
{
    public string Trid { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Internet { get; init; } = string.Empty;
    public bool AssociationAccessAllowed { get; init; }
}

public sealed class InstitutionContactDataInput
{
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Internet { get; set; } = string.Empty;
    public bool AssociationAccessAllowed { get; set; }
}

public sealed class FreePlaceReportingResponse
{
    public DateTime ReportingDate { get; init; }
    public int Availability { get; init; }
    public int LayoutId { get; init; }
    public IReadOnlyList<FreePlaceCategoryResponse> Categories { get; init; } = [];
}

public sealed class FreePlaceCategoryResponse
{
    public int Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public IReadOnlyList<FreePlaceQuestionResponse> Questions { get; init; } = [];
}

public sealed class FreePlaceQuestionResponse
{
    public int Id { get; init; }
    public int TypeId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string ShortLabel { get; init; } = string.Empty;
    public bool BooleanValue { get; init; }
    public int IntegerValue { get; init; }
}

public sealed class FreePlaceQuestionInput
{
    public int QuestionId { get; set; }
    public int TypeId { get; set; }
    public bool BooleanValue { get; set; }
    public int IntegerValue { get; set; }
}

public sealed class VisitorChartPoint
{
    public DateTime Date { get; init; }
    public int Count { get; init; }
}
