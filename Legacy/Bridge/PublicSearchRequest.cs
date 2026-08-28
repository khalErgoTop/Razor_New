namespace TOPqwWeb_Core.Legacy.Bridge
{
    public class PublicSearchRequest
    {
        public string SearchValue { get; set; } = string.Empty;
        public int FacilityType { get; set; }
        public string PostalCode { get; set; } = string.Empty;
        public int DistanceKm { get; set; }
        public bool WithFreePlaces { get; set; }
        public bool Search4Facilities { get; set; } = true;
        public bool Search4Institutions { get; set; } = true;
        public List<int> FacilityFeatures { get; set; } = [];
    }
}
