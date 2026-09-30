using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Services.Public
{
    public class AuflistungPageService
    {
        private readonly PublicSearchAdapter _searchAdapter;

        public AuflistungPageService(PublicSearchAdapter searchAdapter)
        {
            _searchAdapter = searchAdapter;
        }

        public Task<AuflistungResponse> GetAsync(string type, string? typeId, string? districtId, CancellationToken cancellationToken)
        {
            if (!string.Equals(type, "Typ", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(type, "Kreis", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentOutOfRangeException(nameof(type), "Für die Auflistung ist nur Typ oder Kreis zulässig.");
            }

            return _searchAdapter.GetAuflistungAsync(type, typeId, districtId, cancellationToken);
        }
    }
}
