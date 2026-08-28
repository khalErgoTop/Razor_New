namespace TOPqwWeb_Core.Legacy.Bridge
{
    public class SucheResultsResponse
    {
        public List<PublicSearchResultResponse> Results { get; set; } = [];
        public bool HasMoreResults { get; set; }
    }
}
