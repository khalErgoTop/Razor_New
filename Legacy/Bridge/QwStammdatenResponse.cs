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
