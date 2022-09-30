﻿using System;
using System.Collections.Generic;
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
using Polly.Wrap;
using Spresso.Sdk.Core.Connectivity;

namespace Spresso.Sdk.Core.Auth
{
    public class TokenHandler : ITokenHandler
    {
        private readonly IDistributedCache _cache;
        private readonly SpressoHttpClientFactory _httpClientFactory;
        private readonly TimeSpan _httpTimeout;
        private readonly ILogger<ITokenHandler>? _logger;
        private readonly string _spressoBaseAuthUrl;
        private readonly string _tokenCacheKey;
        private readonly string _tokenEndpoint;
        private readonly AsyncPolicyWrap<TokenResponse> _tokenPolicy;
        private readonly string _tokenRequest;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TokenHandler" /> class.
        /// </summary>
        /// <param name="clientId">The client key provided for your application(s)</param>
        /// <param name="clientSecret">The client secret provided for your application(s)</param>
        /// <param name="options">Token handler configuration</param>
        public TokenHandler(string clientId, string clientSecret, TokenHandlerOptions? options = null)
        {
            options ??= new TokenHandlerOptions();
            _logger = options.Logger;
            _httpClientFactory = options.SpressoHttpClientFactory;
            _tokenCacheKey = $"Spresso.Auth.AuthKey.{options.TokenGroup}";
            _spressoBaseAuthUrl = options.SpressoBaseAuthUrl;
            _tokenEndpoint = "oauth/token";
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

            var retryErrors = new[] { AuthError.Timeout, AuthError.Unknown };
            var retryPolicy = Policy.HandleResult<TokenResponse>(t => !t.IsSuccess && retryErrors.Contains(t.Error)).RetryAsync(options.NumberOfRetries);
            var timeoutPolicy = Policy.TimeoutAsync<TokenResponse>(options.Timeout, TimeoutStrategy.Pessimistic);


            var circuitBreakerPolicy = Policy.HandleResult<TokenResponse>(t => !t.IsSuccess)
                .Or<TimeoutRejectedException>()
                .CircuitBreakerAsync(options.NumberOfFailuresBeforeTrippingCircuitBreaker, options.CircuitBreakerBreakDuration,
                    (state, ts, ctx) => { _logger.LogError("@@TokenHandler@@ Token circuit breaker tripped"); },
                    ctx => { _logger.LogDebug("@@TokenHandler@@ Token circuit breaker reset"); });

            var fallbackPolicy = Policy.Handle<Exception>().OrResult<TokenResponse>(r => !r.IsSuccess).FallbackAsync((tokenResponse, ctx, cancellationToken) =>
            {
                if (_logger!.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError("@@TokenHandler@@ Token request failed.  Error {0}", tokenResponse.Result.Error);
                }
                if (tokenResponse.Exception != null)
                {
                    if (tokenResponse.Exception is TimeoutRejectedException)
                    {
                        return Task.FromResult(new TokenResponse(AuthError.Timeout));
                    }
                    return Task.FromResult(new TokenResponse(AuthError.Unknown));
                }

                return Task.FromResult(tokenResponse.Result);
            }, (result, context) => Task.CompletedTask);


            _tokenPolicy = Policy.WrapAsync(fallbackPolicy, circuitBreakerPolicy, timeoutPolicy, retryPolicy);
        }

        /// <summary>
        ///     Gets the access token.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<TokenResponse> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            return await _tokenPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    var auth0TokenResponseJson = await _cache.GetStringAsync(_tokenCacheKey, cancellationToken);
                    if (!string.IsNullOrEmpty(auth0TokenResponseJson))
                    {
                        return CreateTokenResponse(auth0TokenResponseJson);
                    }

                    var httpClient = _httpClientFactory.GetClient();
                    httpClient.BaseAddress = new Uri(_spressoBaseAuthUrl);
                    httpClient.Timeout = _httpTimeout;


                    _logger!.LogDebug("@@TokenHandler.GetTokenAsync@@ Fetching token");
                    var response = await httpClient.PostAsync(_tokenEndpoint, new StringContent(_tokenRequest, Encoding.UTF8, "application/json"),
                        cancellationToken);
                    if (_logger!.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("@@TokenHandler.GetTokenAsync@@ Token status code {0}", response.StatusCode);
                    }
                    if (response.IsSuccessStatusCode)
                    {
                        auth0TokenResponseJson = await response.Content.ReadAsStringAsync();
                        var tokenResponse = CreateTokenResponse(auth0TokenResponseJson);
                        await _cache.SetStringAsync(_tokenCacheKey, auth0TokenResponseJson, new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = tokenResponse.ExpiresAt!.Value.Subtract(new TimeSpan(0, 5, 0))
                        }, cancellationToken);
                        return tokenResponse;
                    }
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                            return new TokenResponse(AuthError.InvalidCredentials);
                        case HttpStatusCode.Forbidden:
                            return new TokenResponse(AuthError.InvalidScopes);
                        default:
                            return new TokenResponse(AuthError.Unknown);
                    }
                }
                catch (HttpRequestException e) when (e.Message.Contains("No connection could be made because the target machine actively refused it."))
                {
                    return new TokenResponse(AuthError.Timeout);
                }
                catch (OperationCanceledException e)
                {
                    return new TokenResponse(AuthError.Timeout);
                }
                catch (Exception e)
                {
                    return new TokenResponse(AuthError.Unknown);
                }
            });
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