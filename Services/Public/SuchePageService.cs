using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Services.Public
{
    public class SuchePageService
    {
        private readonly PublicSearchAdapter _adapter;

        public SuchePageService(PublicSearchAdapter adapter)
        {
            _adapter = adapter;
        }

        public Task<PublicSearchResponse> SearchAsync(PublicSearchRequest request, CancellationToken cancellationToken) =>
            _adapter.SearchAsync(request, cancellationToken);
    }
}
