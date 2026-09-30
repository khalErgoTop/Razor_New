using System.Threading;
using System.Threading.Tasks;
using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Services.Account
{
    public class LoginPageService
    {
        private readonly AuthAdapter _adapter;

        public LoginPageService(AuthAdapter adapter)
        {
            _adapter = adapter;
        }

        public Task<AuthResponse?> LoginEncAsync(string szName, string szPW, CancellationToken cancellationToken)
        {
            return _adapter.LoginEncAsync(szName, szPW, cancellationToken);
        }

        public Task<PasswordRequestResponse> RequestPasswordAsync(string username, CancellationToken cancellationToken)
        {
            return _adapter.RequestPasswordAsync(username, cancellationToken);
        }

        public Task SendPasswordRequestEmailAsync(string username, PasswordRequestResponse request, string url, CancellationToken cancellationToken)
        {
            return _adapter.SendPasswordRequestEmailAsync(username, request, url, cancellationToken);
        }

        public Task<string?> GetMailToAsync(CancellationToken cancellationToken)
        {
            return _adapter.GetMailToAsync(cancellationToken);
        }
    }
}
