using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TOPqwWeb_Core.Infrastructure.Security
{
    public class CurrentUserAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

        public int? BEID
        {
            get
            {
                var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(value, out var beid) ? beid : null;
            }
        }

        public string? Username => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name);
        public string? Email => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);
        public string? LastPage => _httpContextAccessor.HttpContext?.User.FindFirstValue("LastPage");

        public int UserType => int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("LegacyUserType"), out var userType)
            ? userType
            : LegacyUserTypes.Gast;

        public bool IsAdmin => bool.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("LegacyIsAdmin"), out var isAdmin) && isAdmin;
        public bool IsKosoz => bool.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("LegacyIsKosoz"), out var isKosoz) && isKosoz;

        public bool IsInLegacyUserType(params int[] userTypes) => userTypes.Contains(UserType);

        public static class LegacyUserTypes
        {
            public const int Traeger = 1;
            public const int Einrichtung = 2;
            public const int Einrichtungsverwalter = 3;
            public const int TOPqw = 7;
            public const int Gast = 9;
            public const int Verband = 13;
            public const int VerbandUser = 14;
            public const int Jugendamt = 22;
            public const int Gruppe = 55;
        }
    }
}
