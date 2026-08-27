namespace TOPqwWeb_Core.Legacy.Bridge
{
    public class PublicSearchRequest
    {
        public string SearchValue { get; set; } = string.Empty;
        public int FacilityType { get; set; }
        public string PostalCode { get; set; } = string.Empty;
        public int DistanceKm { get; set; }
        public bool WithFreePlaces { get; set; }
        public List<int> FacilityFeatures { get; set; } = [];
    }
}
