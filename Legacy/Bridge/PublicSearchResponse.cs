namespace TOPqwWeb_Core.Legacy.Bridge
{
    public class PublicSearchResponse
    {
        public List<PublicSearchResultResponse> Results { get; set; } = [];
        public bool HasMoreResults { get; set; }
        public List<PublicSearchTypeResponse> FacilityTypes { get; set; } = [];
        public List<int> Distances { get; set; } = [];
        public List<PublicSearchFeatureResponse> FacilityFeatures { get; set; } = [];
    }

    public class PublicSearchTypeResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
    }

    public class PublicSearchFeatureResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
