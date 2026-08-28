using System.Collections.Generic;
using System.Security.Claims;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Infrastructure.Security
{
    public class ClaimsPrincipalFactory
    {
        public ClaimsPrincipal Create(AuthResponse user, string? LastPage = null, string? sessionId = null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.Username)
                , new("LegacyUserType", user.UserType.ToString())
                , new("LegacyIsAdmin", user.IsAdmin.ToString())
                , new("LegacyIsKosoz", user.IsKosoz.ToString())
            };

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            if (!string.IsNullOrWhiteSpace(LastPage))
            {
                claims.Add(new Claim("LastPage", LastPage));
            }

            claims.Add(new Claim("SessionId", sessionId ?? Guid.NewGuid().ToString("N")));
            foreach (var module in user.Modules)
            {
                claims.Add(new Claim("LegacyModule", module.ToString()));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "LegacyCookie"));
        }
    }
}
