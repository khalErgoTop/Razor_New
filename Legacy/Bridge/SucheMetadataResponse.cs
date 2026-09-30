namespace TOPqwWeb_Core.Legacy.Bridge
{
    public class SucheMetadataResponse
    {
        public List<PublicSearchTypeResponse> FacilityTypes { get; set; } = [];
        public List<int> Distances { get; set; } = [];
        public List<PublicSearchFeatureResponse> FacilityFeatures { get; set; } = [];
        public bool ShowFreePlaces { get; set; }
    }
}
