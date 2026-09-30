using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Services.Account
{
    public class PasswordRequestPageService
    {
        private readonly AuthAdapter _adapter;

        public PasswordRequestPageService(AuthAdapter adapter)
        {
            _adapter = adapter;
        }

        public Task<int> CheckPasswordRequestHashAsync(string hash, CancellationToken cancellationToken) =>
            _adapter.CheckPasswordRequestHashAsync(hash, cancellationToken);

        public Task<string?> GetUsernameAsync(int userId, CancellationToken cancellationToken) =>
            _adapter.GetUsernameAsync(userId, cancellationToken);

        public Task ChangePasswordAsync(int uid, string password, CancellationToken cancellationToken) =>
            _adapter.ChangePasswordAsync(uid, password, cancellationToken);

        public Task<PasswordPolicy> GetPasswordPolicyAsync(CancellationToken cancellationToken) =>
            _adapter.GetPasswordPolicyAsync(cancellationToken);
    }
}
