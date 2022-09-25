using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HttpClientFactoryLite;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using IHttpClientFactory = HttpClientFactoryLite.IHttpClientFactory;

namespace Spresso.Sdk.Core.Auth
{
    public class TokenHandler : ITokenHandler
    {
        private const string DefaultSpressoBaseAuthUrl = "https://auth.spresso.com";
        private const string DefaultSpressoAudience = "https://spresso-api";
        private readonly IDistributedCache _cache;
        private readonly string _tokenCacheKey;
        private readonly string _tokenRequest;
        private readonly string _spressoBaseAuthUrl;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenHandler"/> class.
        /// </summary>
        /// <param name="clientId">The client key provided for your application(s)</param>
        /// <param name="clientSecret">The client secret provided for your application(s)</param>
        /// <param name="cache">The caching strategy for token storage.  Default is in-memory</param>
        /// <param name="tokenGroup">Only relevant in distributed caching scenarios.  Set this value if you have multiple applications using a shared distributed cache if they have different keys/secrets or if they have different scopes</param>
        /// <param name="scopes">You may set a subset of scopes issued to your client to reduce the security footprint.  Default is all scopes issued for your client</param>
        public TokenHandler(string clientId, string clientSecret, IDistributedCache? cache = null, string tokenGroup = "default", string[]? scopes = null)
        {
            _httpClientFactory = new HttpClientFactory();
            _tokenCacheKey = $"Spresso.Auth.AuthKey.{tokenGroup}";
            _spressoBaseAuthUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL") ?? DefaultSpressoBaseAuthUrl;
            var spressoAudience = Environment.GetEnvironmentVariable("SPRESSO_AUDIENCE") ?? DefaultSpressoAudience;
            var tokenRequestBuilder = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["audience"] = spressoAudience,
                ["grant_type"] = "client_credentials"
            };

            if (scopes != null)
            {
                tokenRequestBuilder.Add("scope", string.Join(" ", scopes));
            }

            _cache = cache ?? new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));
            _tokenRequest = JsonConvert.SerializeObject(tokenRequestBuilder);
        }

        public async Task<TokenResponse> GetTokenAsync()
        {
            var auth0TokenResponseJson = await _cache.GetStringAsync(_tokenCacheKey);
            if (!string.IsNullOrEmpty(auth0TokenResponseJson))
            {
                return CreateTokenResponse(auth0TokenResponseJson);
            }

            var httpClient = _httpClientFactory.CreateClient("spresso");
            httpClient.BaseAddress = new Uri(_spressoBaseAuthUrl);
            httpClient.Timeout = new TimeSpan(0, 0, 30); // todo: timeout should be configurable
            try
            {
                var response = await httpClient.PostAsync("/oauth/token", new StringContent(_tokenRequest, Encoding.UTF8, "application/json"));
                if (response.IsSuccessStatusCode)
                {
                    auth0TokenResponseJson = await response.Content.ReadAsStringAsync();
                    var tokenResponse = CreateTokenResponse(auth0TokenResponseJson);
                    await _cache.SetStringAsync(_tokenCacheKey, auth0TokenResponseJson, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = tokenResponse.ExpiresAt.Value.Subtract(new TimeSpan(0, 5, 0))
                    });
                    return tokenResponse;
                }
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return new TokenResponse(AuthError.InvalidCredentials);
                }
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return new TokenResponse(AuthError.InvalidScopes);
                }
                return new TokenResponse(AuthError.Unknown);
            }
            catch (HttpRequestException e) when (e.Message.Contains("timed out"))
            {
                return new TokenResponse(AuthError.Timeout);
            }
            catch (HttpRequestException e) when (e.Message.Contains("401"))
            {
                return new TokenResponse(AuthError.InvalidCredentials);
            }
            catch (HttpRequestException)
            {
                return new TokenResponse(AuthError.Unknown);
            }
        }

        private TokenResponse CreateTokenResponse(string auth0TokenResponseJson)
        {
            var auth0TokenResponse = JsonConvert.DeserializeObject<Auth0TokenResponse>(auth0TokenResponseJson);
            return new TokenResponse(auth0TokenResponse.access_token!, DateTimeOffset.Now.AddSeconds(auth0TokenResponse.expires_in));
        }

        private class Auth0TokenResponse
        {
            public string? access_token { get; set; }
            public string? token_type { get; set; }
            public int expires_in { get; set; }
            public string scope { get; set; } = string.Empty;
        }
    }
}