using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using Spresso.Sdk.Core.Connectivity;

namespace Spresso.Sdk.Core.Auth
{
    public class TokenHandler : ITokenHandler
    {
        private const string DefaultSpressoBaseAuthUrl = "https://auth.spresso.com";
        private const string DefaultSpressoAudience = "https://spresso-api";
        private readonly IDistributedCache _cache;
        private readonly SpressoHttpClientFactory _httpClientFactory;
        private readonly string _spressoBaseAuthUrl;
        private readonly string _tokenCacheKey;
        private readonly string _tokenRequest;
        private readonly string _tokenEndpoint;
        private readonly string _additionalParameters;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TokenHandler" /> class.
        /// </summary>
        /// <param name="clientId">The client key provided for your application(s)</param>
        /// <param name="clientSecret">The client secret provided for your application(s)</param>
        /// <param name="options">Token handler configuration</param>
        public TokenHandler(string clientId, string clientSecret, TokenHandlerOptions? options = null)
        {
            options ??= new TokenHandlerOptions();
            _httpClientFactory = options.SpressoHttpClientFactory;
            _tokenCacheKey = $"Spresso.Auth.AuthKey.{options.TokenGroup}";
            _spressoBaseAuthUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL") ?? DefaultSpressoBaseAuthUrl;
            _additionalParameters = options.AdditionalParameters;
            _tokenEndpoint = "oauth/token";
            if (!String.IsNullOrEmpty(_additionalParameters))
            {
                _tokenEndpoint += "?" + _additionalParameters;
            }
            var spressoAudience = Environment.GetEnvironmentVariable("SPRESSO_AUDIENCE") ?? DefaultSpressoAudience;
            var tokenRequestBuilder = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["audience"] = spressoAudience,
                ["grant_type"] = "client_credentials"
            };

            if (options.Scopes != null && options.Scopes.Length > 0)
            {
                tokenRequestBuilder.Add("scope", string.Join(" ", options.Scopes));
            }

            _cache = options.Cache!;
            _tokenRequest = JsonConvert.SerializeObject(tokenRequestBuilder);
        }

        public async Task<TokenResponse> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            var auth0TokenResponseJson = await _cache.GetStringAsync(_tokenCacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(auth0TokenResponseJson))
            {
                return CreateTokenResponse(auth0TokenResponseJson);
            }

            var httpClient = _httpClientFactory.GetClient();
            httpClient.BaseAddress = new Uri(_spressoBaseAuthUrl);
            httpClient.Timeout = new TimeSpan(0, 0, 30); // todo: timeout should be configurable
            try
            {
                var response = await httpClient.PostAsync(_tokenEndpoint, new StringContent(_tokenRequest, Encoding.UTF8, "application/json"), cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    auth0TokenResponseJson = await response.Content.ReadAsStringAsync();
                    var tokenResponse = CreateTokenResponse(auth0TokenResponseJson);
                    await _cache.SetStringAsync(_tokenCacheKey, auth0TokenResponseJson, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = tokenResponse.ExpiresAt.Value.Subtract(new TimeSpan(0, 5, 0))
                    }, token: cancellationToken);
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
            catch (OperationCanceledException e)
            {
                return new TokenResponse(AuthError.Timeout);
            }
            catch (HttpRequestException e) when (e.Message.Contains("401"))
            {
                return new TokenResponse(AuthError.InvalidCredentials);
            }
            catch (Exception e)
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