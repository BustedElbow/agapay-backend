using agapay_backend.Entities;

namespace agapay_backend.Services
{
    public interface ITokenService
    {
        string CreateAccessToken(User user, IEnumerable<string> roles);
        string CreateRefreshToken();
    }
}
