namespace TOPqwWeb_Core.Legacy.Bridge
{
    public class PublicSearchResultResponse
    {
        public string EIID { get; set; } = string.Empty;
        public string? STUID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? FacilityType { get; set; }
        public string? FacilityIndicator { get; set; }
        public string? Address { get; set; }
    }
}
