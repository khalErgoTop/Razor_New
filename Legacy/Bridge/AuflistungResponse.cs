namespace TOPqwWeb_Core.Legacy.Bridge
{
    public class AuflistungResponse
    {
        public string Title { get; set; } = string.Empty;
        public List<AuflistungCategoryResponse> Categories { get; set; } = [];
    }

    public class AuflistungCategoryResponse
    {
        public string Name { get; set; } = string.Empty;
        public List<AuflistungGroupResponse> Groups { get; set; } = [];
    }

    public class AuflistungGroupResponse
    {
        public string Name { get; set; } = string.Empty;
        public List<AuflistungFacilityResponse> Facilities { get; set; } = [];
    }

    public class AuflistungFacilityResponse
    {
        public string EIID { get; set; } = string.Empty;
        public string? STUID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? FacilityIndicator { get; set; }
        public string? Address { get; set; }
        public List<AuflistungFacilityResponse> AdditionalLocations { get; set; } = [];
    }
}
