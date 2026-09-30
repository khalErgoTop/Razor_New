using TOPqwWeb_Core.Legacy.Bridge;
using System.Text.Json;

namespace TOPqwWeb_Core.Infrastructure.Session
{
    public sealed class SearchStateStore
    {
        private const string SearchStateKey = "Suche.Userselection";
        private const string SearchResultsKey = "Stammdaten.LastSearchResult";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SearchStateStore(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public PublicSearchRequest? Request
        {
            get => Get<PublicSearchRequest>(SearchStateKey);
            set => Set(SearchStateKey, value);
        }

        public PublicSearchResponse? Results
        {
            get => Get<PublicSearchResponse>(SearchResultsKey);
            set => Set(SearchResultsKey, value);
        }

        private ISession Session => _httpContextAccessor.HttpContext?.Session
            ?? throw new InvalidOperationException("Es ist keine HTTP-Session verfügbar.");

        private T? Get<T>(string key)
        {
            var value = Session.GetString(key);
            return string.IsNullOrWhiteSpace(value) ? default : JsonSerializer.Deserialize<T>(value);
        }

        private void Set<T>(string key, T? value)
        {
            if (value is null)
            {
                Session.Remove(key);
                return;
            }

            Session.SetString(key, JsonSerializer.Serialize(value));
        }
    }
}
