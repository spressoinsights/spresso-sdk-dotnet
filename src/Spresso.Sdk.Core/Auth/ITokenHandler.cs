using System.Threading.Tasks;

namespace Spresso.Sdk.Core.Auth
{
    public interface ITokenHandler
    {
        Task<TokenResponse> GetTokenAsync();
    }
}