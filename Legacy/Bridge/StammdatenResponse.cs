namespace TOPqwWeb_Core.Legacy.Bridge
{
    public class StammdatenResponse
    {
        public string EIID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? FacilityIndicator { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Description { get; set; }
        public List<StammdatenContactResponse> Contacts { get; set; } = [];
        public List<StammdatenLocationResponse> Locations { get; set; } = [];
        public List<StammdatenAttachmentResponse> Attachments { get; set; } = [];
        public string? ImageFileName { get; set; }
    }

    public class StammdatenContactResponse
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class StammdatenLocationResponse
    {
        public int? STID { get; set; }
        public string? STUID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? HouseNumberAddition { get; set; }
        public string? PostalCode { get; set; }
        public string? City { get; set; }
        public int? Capacity { get; set; }
        public bool IsMainLocation { get; set; }
        public bool ShowAddressInWeb { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public string? Fax { get; set; }
        public string? Email { get; set; }
        public string? Internet { get; set; }
        public List<StammdatenContactResponse> Contacts { get; set; } = [];
    }

    public sealed class StammdatenLocationInput
    {
        public string? Name { get; set; }
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? HouseNumberAddition { get; set; }
        public string? PostalCode { get; set; }
        public string? City { get; set; }
        public int? Capacity { get; set; }
        public bool? ShowAddressInWeb { get; set; }
    }

    public class StammdatenAttachmentResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public int Type { get; set; }
        public int Status { get; set; }
    }

    public sealed record StammdatenAnalysisTypeResponse(int Id, string Titel);

    public sealed record StammdatenAnalysisResponse(int Id, string Displayname, int AbfrageTyp, string Kriterium);

    public sealed record StammdatenDashboardResponse(string AbfrageName, string Ueberschrift);

    public sealed record StammdatenAnalysisFiltersResponse(
        IReadOnlyList<StammdatenFacilityTypeResponse> FacilityTypes,
        IReadOnlyList<StammdatenDistrictResponse> Districts);

    public sealed record StammdatenFacilityTypeResponse(int ETID, string Typkennzeichnung);

    public sealed record StammdatenDistrictResponse(int KRID, string Kreis);

    public sealed class StammdatenAnalysisResultResponse
    {
        public IReadOnlyList<string> Structure { get; init; } = [];
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Data { get; init; } = [];
        public int AbfrageTyp { get; init; }
        public string AbfrageTyKriteria { get; init; } = string.Empty;
    }

    public sealed class StammdatenDashboardResultResponse
    {
        public int Id { get; init; }
        public string AbfrageName { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string CssTyp { get; init; } = string.Empty;
        public string Ueberschrift { get; init; } = string.Empty;
        public string Statement { get; init; } = string.Empty;
        public string Openfunc { get; init; } = string.Empty;
        public string ReloadFunc { get; init; } = string.Empty;
        public string DiagrammFarben { get; init; } = string.Empty;
        public string Data { get; init; } = string.Empty;
        public IReadOnlyList<string> Params { get; init; } = [];
        public IReadOnlyList<StammdatenDashboardRestrictionResponse> AvailableRestrictions { get; set; } = [];
        public IReadOnlyList<StammdatenDashboardRestrictionResponse> UserRestrictions { get; set; } = [];
        public int AutoReload { get; init; }
        public int AllowShowMore { get; init; }
        public IReadOnlyList<string> Structure { get; set; } = [];
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; set; } = [];
        public string? ExecutionMessage { get; set; }
        public string? TextValue { get; set; }
        public IReadOnlyList<string> ChartLabels { get; set; } = [];
        public IReadOnlyList<object?> ChartValues { get; set; } = [];
        public string ChartType { get; set; } = string.Empty;
    }

    public sealed record StammdatenDashboardRestrictionResponse(
        int Id,
        int RId,
        string Name,
        string DataType,
        string Column,
        string Table,
        string UserSelection,
        string UserOperator,
        bool Selected);

    public sealed record StammdatenDashboardRestrictionInput(
        string Name,
        string? Selection,
        string? UserOperator);
}
