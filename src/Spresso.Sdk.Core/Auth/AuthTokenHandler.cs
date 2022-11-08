using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Timeout;
using Spresso.Sdk.Core.Connectivity;
using Spresso.Sdk.Core.Resiliency;

namespace Spresso.Sdk.Core.Auth
{
    public class AuthTokenHandler : IAuthTokenHandler
    {
        private readonly IDistributedCache _cache;
        private readonly SpressoHttpClientFactory _httpClientFactory;
        private readonly TimeSpan _httpTimeout;
        private readonly ILogger<IAuthTokenHandler> _logger;
        private readonly string _spressoBaseAuthUrl;
        private readonly string _tokenCacheKey;
        private readonly string _tokenEndpoint;
        private readonly IAsyncPolicy<AuthTokenResponse> _tokenResiliencyPolicy;
        private readonly string _tokenRequest;


        /// <summary>
        ///     Initializes a new instance of the <see cref="AuthTokenHandler" /> class.
        /// </summary>
        /// <param name="clientId">The client key provided for your application(s)</param>
        /// <param name="clientSecret">The client secret provided for your application(s)</param>
        /// <param name="options">Token handler configuration</param>
        public AuthTokenHandler(string clientId, string clientSecret, AuthTokenHandlerOptions? options = null)
        {
            options ??= new AuthTokenHandlerOptions();
            _logger = options.Logger;
            _httpClientFactory = options.SpressoHttpClientFactory;
            _tokenCacheKey = $"Spresso.Auth.AuthKey.{options.TokenGroup}";
            _spressoBaseAuthUrl = options.SpressoBaseAuthUrl;
            _tokenEndpoint = "v1/public/token";
            if (!string.IsNullOrEmpty(options.AdditionalParameters))
            {
                _tokenEndpoint += "?" + options.AdditionalParameters;
            }
            var spressoAudience = options.SpressoAudience;
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
            _httpTimeout = options.HttpTimeout;

            _tokenResiliencyPolicy = CreateTokenResiliencyPolicy(options);
        }

       /// <inheritdoc cref="IAuthTokenHandler.GetTokenAsync"/>
        public async Task<AuthTokenResponse> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            return await _tokenResiliencyPolicy.ExecuteAsync(async () =>
            {
                var httpClient = _httpClientFactory.GetClient();
                httpClient.BaseAddress = new Uri(_spressoBaseAuthUrl);
                httpClient.Timeout = _httpTimeout;


                _logger.LogDebug("@@{0}@@ Fetching token", nameof(GetTokenAsync));
                
                return await httpClient.ExecutePostApiRequestAsync(_tokenEndpoint, _tokenRequest,
                    onSuccessFunc: async (auth0TokenResponseJson, statusCode) =>
                    {
                        _logger.LogDebug("@@{0}@@ Token status code {1}", nameof(GetTokenAsync), statusCode);
                        var tokenResponse = CreateTokenResponse(auth0TokenResponseJson);
                        await _cache.SetStringAsync(_tokenCacheKey, auth0TokenResponseJson, new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = tokenResponse.ExpiresAt!.Value.Subtract(new TimeSpan(0, 5, 0))
                        }, cancellationToken);
                        return tokenResponse;
                    },
                    onAuthErrorFailure: (statusCode) =>
                    {
                        _logger.LogDebug("@@{0}@@ Token status code {1}", nameof(GetTokenAsync), statusCode);
                        switch (statusCode)
                        {
                            case HttpStatusCode.Unauthorized:
                                return new AuthTokenResponse(AuthError.InvalidCredentials);
                            default:
                                return new AuthTokenResponse(AuthError.InvalidScopes);
                        }

                    },
                    onBadRequestFailure: () =>
                    {
                        _logger.LogError("@@{0}@@ Token status code {1}", nameof(GetTokenAsync), HttpStatusCode.BadRequest);
                        return new AuthTokenResponse(AuthError.Unknown);
                    },
                    onTimeoutFailure: exception =>
                    {
                        _logger.LogError("@@{0}@@ Error getting token.  Exception: {1}", nameof(GetTokenAsync), exception);
                        return new AuthTokenResponse(AuthError.Timeout);
                    },
                    onUnknownFailure: (exception, httpStatusCode) =>
                    {
                        _logger.LogError("@@{0}@@ Error getting token.  HttpStatusCode: {1}, Exception: {2}", nameof(GetTokenAsync), httpStatusCode, exception);
                        return new AuthTokenResponse(AuthError.Unknown);
                    },
                    cancellationToken);
            });
        }

        private IAsyncPolicy<AuthTokenResponse> CreateTokenResiliencyPolicy(AuthTokenHandlerOptions options)
        {
            var retryErrors = new[] { AuthError.Timeout, AuthError.Unknown };

            return ResiliencyPolicyBuilder.BuildPolicy(
                retryOptions: new RetryOptions<AuthTokenResponse>(t => !t.IsSuccess && retryErrors.Contains(t.Error), options.NumberOfRetries),
                new TimeoutOptions(options.Timeout),
                circuitBreakerOptions: new CircuitBreakerOptions<AuthTokenResponse>(t => !t.IsSuccess && retryErrors.Contains(t.Error),
                    options.NumberOfFailuresBeforeTrippingCircuitBreaker,
                    options.CircuitBreakerBreakDuration,
                    onBreakAction: (state, ts, ctx) => { _logger.LogError("Token circuit breaker tripped"); },
                    onResetAction: ctx => { _logger.LogInformation("Token circuit breaker reset"); }),
                        fallbackOptions: new FallbackOptions<AuthTokenResponse>(
                            fallbackPredicate: r => !r.IsSuccess,
                            fallbackAction: (tokenResponse, ctx, cancellationToken) =>
                            {
                                _logger.LogError("Token request failed.  Error {0}.  Exception (if applicable): {1}", tokenResponse?.Result.Error,
                                    tokenResponse?.Exception?.Message);

                                if (tokenResponse.Exception != null)
                                {
                                    if (options.ThrowOnTokenFailure)
                                    {
                                        throw tokenResponse.Exception;
                                    }
                                    
                                    if (tokenResponse.Exception is TimeoutRejectedException)
                                    {
                                        return Task.FromResult(new AuthTokenResponse(AuthError.Timeout));
                                    }
                                    return Task.FromResult(new AuthTokenResponse(AuthError.Unknown));
                                }

                                if (options.ThrowOnTokenFailure)
                                {
                                    throw new Exception($"Token request failed.  Error {tokenResponse?.Result.Error}");
                                }
                                
                                return Task.FromResult(tokenResponse.Result);
                            },
                            onFallback: (result, context) => Task.CompletedTask)
                );
        }

        private AuthTokenResponse CreateTokenResponse(string auth0TokenResponseJson)
        {
            var auth0TokenResponse = JsonConvert.DeserializeObject<Auth0TokenResponse>(auth0TokenResponseJson);
            return new AuthTokenResponse(auth0TokenResponse.access_token!, DateTimeOffset.Now.AddSeconds(auth0TokenResponse.expires_in));
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