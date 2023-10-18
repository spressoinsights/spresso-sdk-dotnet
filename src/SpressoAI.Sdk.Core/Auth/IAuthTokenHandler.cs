using System.Threading;
using System.Threading.Tasks;

namespace SpressoAI.Sdk.Core.Auth
{
    public interface IAuthTokenHandler
    {
        /// <summary>
        ///     Gets a token for the given scopes.  If the token is not cached, it will be fetched from the server.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<AuthTokenResponse> GetTokenAsync(CancellationToken cancellationToken = default);
    }
}