using System.Threading;
using System.Threading.Tasks;

namespace Spresso.Sdk.Core.Auth
{
    public interface ITokenHandler
    {
        /// <summary>
        ///     Gets a token for the given scopes.  If the token is not cached, it will be fetched from the server.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TokenResponse> GetTokenAsync(CancellationToken cancellationToken = default);
    }
}