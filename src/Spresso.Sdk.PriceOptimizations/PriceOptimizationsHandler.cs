using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Timeout;
using Spresso.Sdk.Core.Auth;
using Spresso.Sdk.Core.Connectivity;
using Spresso.Sdk.Core.Resiliency;

namespace Spresso.Sdk.PriceOptimizations
{
    public class PriceOptimizationsHandler : IPriceOptimizationHandler
    {
        private const string TokenCacheKeyPrefix = "Spresso.PriceOptimizations";
        private const int MaxRequestSize = 20;
        private readonly string _additionalParameters;
        private readonly string _baseUrl;
        private readonly TimeSpan _cacheDuration;
        private readonly string _cacheNamespace;
        private readonly IDistributedCache _distributedCache;
        private readonly IAsyncPolicy<GetPriceOptimizationResponse> _getPriceOptimizationPolicy;
        private readonly IAsyncPolicy<GetBatchPriceOptimizationsResponse> _getPriceOptimizationsBatchPolicy;
        private readonly SpressoHttpClientFactory _httpClientFactory;
        private readonly TimeSpan _httpTimeout;
        private readonly IMemoryCache _localCache;
        private readonly ILogger<IPriceOptimizationHandler> _logger;
        private readonly IAuthTokenHandler _authTokenHandler;

        public PriceOptimizationsHandler(IAuthTokenHandler authTokenHandler, PriceOptimizationsHandlerOptions? options = null)
        {
            options ??= new PriceOptimizationsHandlerOptions();
            _logger = options.Logger;
            _authTokenHandler = authTokenHandler;
            _distributedCache = options.DistributedCache;
            _localCache = options.LocalCache;
            _httpClientFactory = options.SpressoHttpClientFactory;
            _baseUrl = options.SpressoBaseUrl;
            _cacheNamespace = $"{TokenCacheKeyPrefix}.{options.TokenGroup}";
            _httpTimeout = options.HttpTimeout;
            _cacheDuration = options.CacheDuration;
            _additionalParameters = options.AdditionalParameters;
            _getPriceOptimizationPolicy = CreatePriceOptimizationResiliencyPolicy(options);
            _getPriceOptimizationsBatchPolicy = GetPriceOptimizationsBatchResiliencyPolicy(options);
        }

        /// <inheritdoc cref="IPriceOptimizationHandler.GetPriceOptimizationAsync"/>
        public async Task<GetPriceOptimizationResponse> GetPriceOptimizationAsync(GetPriceOptimizationRequest request,
            CancellationToken cancellationToken = default)
        {
            const string logNamespace = "@@GetPriceOptimizationAsync@@";

            var executionResult = await _getPriceOptimizationPolicy.ExecuteAsync(async () =>
            {
                var cacheKey = GetPriceOptimizationCacheKey(request);

                _logger.LogDebug("{0} fetching optimization [device: {1}, item: {2}]", logNamespace, request.DeviceId, request.ItemId);


                var cachedPriceOptimization = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
                if (cachedPriceOptimization != null)
                {
                    _logger.LogDebug("{0} cache hit [device: {1}, item: {2}]", logNamespace, request.DeviceId, request.ItemId);

                    var priceOptimization = CreatePriceOptimization(cachedPriceOptimization);
                    return new GetPriceOptimizationResponse(priceOptimization);
                }


                _logger.LogDebug("{0} cache miss, calling api [device: {1}, item: {2}]", logNamespace, request.DeviceId, request.ItemId);


                if (!string.IsNullOrEmpty(request.UserAgent))
                {
                    var userAgentOverridesResponse = await GetPriceOptimizationsUserAgentOverridesAsync(cancellationToken);
                    if (userAgentOverridesResponse.IsSuccess)
                    {
                        if (userAgentOverridesResponse.UserAgentRegexes.Any(regex => regex.IsMatch(request.UserAgent)))
                        {
                            _logger.LogDebug("{0} user agent override [device: {1}, item: {2}, user-agent: {3}].  Proceeding", logNamespace, request.DeviceId,
                                request.ItemId, request.UserAgent);

                            return new GetPriceOptimizationResponse(new PriceOptimization
                            {
                                UserId = request.UserId,
                                DeviceId = request.DeviceId,
                                ItemId = request.ItemId,
                                IsOptimizedPrice = false,
                                Price = request.DefaultPrice
                            });
                        }
                    }
                    else
                    {
                        _logger.LogWarning("{0} failed to get user agent overrides [device: {1}, item: {2}].  Proceeding", logNamespace, request.DeviceId,
                            request.ItemId);
                    }
                }


                var tokenResponse = await GetTokenAsync(logNamespace, e => new GetPriceOptimizationResponse(e), cancellationToken);
                if (!tokenResponse.IsSuccess)
                {
                    return tokenResponse.ErrorResponse;
                }

                var token = tokenResponse.Token!;

                var httpClient = GetHttpClient(token);
                var query =
                    $"/v1/priceOptimizations?deviceId={request.DeviceId}&itemId={request.ItemId}&defaultPrice={request.DefaultPrice}&overrideToDefaultPrice={request.OverrideToDefaultPrice}";

                _logger.LogDebug("{0} querying {1}", logNamespace, query);

                if (!string.IsNullOrEmpty(request.UserId))
                {
                    query += $"&userId={request.UserId}";
                }
                if (!string.IsNullOrEmpty(_additionalParameters))
                {
                    query += $"&{_additionalParameters}";
                }
                return await ExecuteGetApiRequestAsync(httpClient, query, async json =>
                {
                    var priceOptimization = CreatePriceOptimization(json);
                    await _distributedCache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _cacheDuration
                    }, cancellationToken);
                    return new GetPriceOptimizationResponse(priceOptimization);
                }, e => new GetPriceOptimizationResponse(e), cancellationToken);
            });

            if (executionResult.IsSuccess)
            {
                _logger.LogDebug("{0} found result [device: {1}, item: {2}]", logNamespace, executionResult.PriceOptimization!.Value.DeviceId,
                    executionResult.PriceOptimization!.Value.ItemId);
                return executionResult;
            }

            // create a price optimization upon failure using the default price
            var defaultPriceOptimization = CreateDefaultPriceOptimization(request);
            _logger.LogDebug("{0} failed getting price optimization. using fallback [device: {1}, item: {2}]", logNamespace, defaultPriceOptimization.DeviceId,
                defaultPriceOptimization.ItemId);
            return new GetPriceOptimizationResponse(executionResult.Error, defaultPriceOptimization);
        }

        /// <inheritdoc cref="IPriceOptimizationHandler.GetBatchPriceOptimizationsAsync"/>
        public async Task<GetBatchPriceOptimizationsResponse> GetBatchPriceOptimizationsAsync(GetBatchPriceOptimizationsRequest request,
            CancellationToken cancellationToken = default)
        {
            const string logNamespace = "@@GetBatchPriceOptimizationsAsync@@";

            var executionResult = await _getPriceOptimizationsBatchPolicy.ExecuteAsync(async () =>
            {
                var poRequests = request.Requests.ToList();
                var requestCount = poRequests.Count;

                if (requestCount > MaxRequestSize)
                {
                    throw new ArgumentException($"Max batch size is {MaxRequestSize} requests");
                }

                if (!string.IsNullOrEmpty(request.UserAgent))
                {
                    var userAgentOverridesResponse = await GetPriceOptimizationsUserAgentOverridesAsync(cancellationToken);
                    if (userAgentOverridesResponse.IsSuccess)
                    {
                        if (userAgentOverridesResponse.UserAgentRegexes.Any(regex => regex.IsMatch(request.UserAgent)))
                        {
                            _logger.LogDebug("{0} user agent override [user-agent: {1}].  Proceeding", logNamespace, request.UserAgent);

                            return new GetBatchPriceOptimizationsResponse(request.Requests.Select(request => new PriceOptimization
                            {
                                UserId = request.UserId,
                                DeviceId = request.DeviceId,
                                ItemId = request.ItemId,
                                IsOptimizedPrice = false,
                                Price = request.DefaultPrice
                            }));
                        }
                    }
                    else
                    {
                        _logger.LogWarning("{0} failed to get user agent overrides", logNamespace);
                    }
                }

                var responses = new PriceOptimization[requestCount];

                var needApiCallIndexes = new List<int>(requestCount);

                for (var i = 0; i < requestCount; i++)
                {
                    var poRequest = poRequests[i];

                    _logger.LogDebug("{0} Checking cache for [device: {1}, item: {2}]", logNamespace, poRequest.DeviceId, poRequest.ItemId);

                    var cacheKey = GetPriceOptimizationCacheKey(poRequest);
                    var cachedPriceOptimization = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
                    if (cachedPriceOptimization != null)
                    {
                        _logger.LogDebug("{0} cache hit [device: {1}, item: {2}]", logNamespace, poRequest.DeviceId, poRequest.ItemId);
                        responses[i] = CreatePriceOptimization(cachedPriceOptimization);
                    }
                    else
                    {
                        _logger.LogDebug("{0} cache miss [device: {1}, item: {2}]", logNamespace, poRequest.DeviceId, poRequest.ItemId);
                        needApiCallIndexes.Add(i);
                    }
                }
                if (needApiCallIndexes.Any())
                {
                    var apiRequests = new List<GetPriceOptimizationRequest>(needApiCallIndexes.Count);

                    needApiCallIndexes.ForEach(i => { apiRequests.Add(poRequests[i]); });

                    var tokenResponse = await GetTokenAsync(logNamespace, e => new GetBatchPriceOptimizationsResponse(e), cancellationToken);

                    if (!tokenResponse.IsSuccess)
                    {
                        return tokenResponse.ErrorResponse;
                    }

                    var token = tokenResponse.Token!;
                    var httpClient = GetHttpClient(token);

                    var requestUri =
                        "/v1/priceOptimizations";
                    if (!string.IsNullOrEmpty(_additionalParameters))
                    {
                        requestUri += $"?{_additionalParameters}";
                    }

                    var batchApiRequest = new
                    {
                        PricingRefs = apiRequests
                    };
                    var requestJson = JsonConvert.SerializeObject(batchApiRequest);

                    return await ExecutePostApiRequestAsync(httpClient, requestUri, requestJson, async responseJson =>
                    {
                        var apiBatchOptimizations = CreatePriceOptimizationArray(responseJson);

                        // assumption for this entire module is that order is preserved between api request and response
                        var apiResponseIndex = 0;
                        foreach (var i in needApiCallIndexes)
                        {
                            responses[i] = apiBatchOptimizations[apiResponseIndex++];
                            var cacheKey = GetPriceOptimizationCacheKey(poRequests[i]);
                            await _distributedCache.SetStringAsync(cacheKey,
                                JsonConvert.SerializeObject(new GetPriceOptimizationApiResponse { Data = responses[i] }),
                                new DistributedCacheEntryOptions
                                {
                                    AbsoluteExpirationRelativeToNow = _cacheDuration
                                }, cancellationToken);
                        }
                        return new GetBatchPriceOptimizationsResponse(responses);
                    }, e => new GetBatchPriceOptimizationsResponse(e), cancellationToken);
                }
                return new GetBatchPriceOptimizationsResponse(responses);
            });

            if (executionResult.IsSuccess)
            {
                _logger.LogDebug("{0} found results", logNamespace);
                return executionResult;
            }

            _logger.LogDebug("{0} failed getting batch prioce optimizations.  using fallback", logNamespace);
            // todo: fallback price not cached, but note error may be because issue with cache.
            return new GetBatchPriceOptimizationsResponse(executionResult.Error, request.Requests.Select(CreateDefaultPriceOptimization));
        }

        public async Task<GetPriceOptimizationsUserAgentOverridesResponse> GetPriceOptimizationsUserAgentOverridesAsync(
            CancellationToken cancellationToken = default)
        {
            const string cacheKey = TokenCacheKeyPrefix + ".UserAgentRegexes";
            const string logNamespace = "@@PriceOptimizationsHandler.GetPriceOptimizationsUserAgentOverridesAsync@@";

            if (_localCache.TryGetValue(cacheKey, out GetPriceOptimizationsUserAgentOverridesResponse response))
            {
                return response;
            }

            var tokenResponse = await GetTokenAsync(logNamespace, e => new GetPriceOptimizationsUserAgentOverridesResponse(e), cancellationToken);
            if (!tokenResponse.IsSuccess)
            {
                return tokenResponse.ErrorResponse;
            }

            var token = tokenResponse.Token!;
            var httpClient = GetHttpClient(token);

            var query = "/v1/userAgents";
            return await ExecuteGetApiRequestAsync(httpClient, query, jsonResponse =>
            {
                var apiResponse = CreateUserAgentRegexes(jsonResponse);
                var compiledRegexes = apiResponse.Data.Select(r => new Regex(r.Regex, RegexOptions.Singleline | RegexOptions.Compiled)).ToArray();

                var getPriceOptimizationsUserAgentOverridesResponse = new GetPriceOptimizationsUserAgentOverridesResponse(compiledRegexes);
                _localCache.Set(cacheKey, getPriceOptimizationsUserAgentOverridesResponse, _cacheDuration);
                return Task.FromResult(getPriceOptimizationsUserAgentOverridesResponse);
            }, e => new GetPriceOptimizationsUserAgentOverridesResponse(e), cancellationToken);
        }

        private IAsyncPolicy<GetBatchPriceOptimizationsResponse> GetPriceOptimizationsBatchResiliencyPolicy(PriceOptimizationsHandlerOptions options)
        {
            return CreateResiliencyPolicy(options,
                new FallbackOptions<GetBatchPriceOptimizationsResponse>(
                    r => !r.IsSuccess,
                    (response, ctx, ct) =>
                    {
                        _logger.LogError("@@{0}@@ Token request failed.  Error {1}.  Exception (if applicable): {2}",
                            nameof(GetBatchPriceOptimizationsAsync), response?.Result.Error, response?.Exception?.Message);

                        if (response!.Exception != null)
                        {
                            if (response.Exception is TimeoutRejectedException)
                            {
                                return Task.FromResult(new GetBatchPriceOptimizationsResponse(PriceOptimizationError.Timeout));
                            }
                            return Task.FromResult(new GetBatchPriceOptimizationsResponse(PriceOptimizationError.Unknown));
                        }

                        return Task.FromResult(response.Result);
                    },
                    (result, context) => Task.CompletedTask
                    ), nameof(GetBatchPriceOptimizationsAsync));
        }

        private IAsyncPolicy<GetPriceOptimizationResponse> CreatePriceOptimizationResiliencyPolicy(PriceOptimizationsHandlerOptions options)
        {
            return CreateResiliencyPolicy(options,
                new FallbackOptions<GetPriceOptimizationResponse>(
                    r => !r.IsSuccess,
                    (response, ctx, ct) =>
                    {
                        _logger.LogError("@@{0}@@ Token request failed.  Error {1}.  Exception (if applicable): {2}",
                            nameof(GetPriceOptimizationAsync), response?.Result.Error, response?.Exception?.Message);

                        if (response!.Exception != null)
                        {
                            if (response.Exception is TimeoutRejectedException)
                            {
                                return Task.FromResult(new GetPriceOptimizationResponse(PriceOptimizationError.Timeout));
                            }
                            return Task.FromResult(new GetPriceOptimizationResponse(PriceOptimizationError.Unknown));
                        }

                        return Task.FromResult(response.Result);
                    },
                    (result, context) => Task.CompletedTask
                    ), nameof(GetPriceOptimizationAsync));
        }

        private GetUserAgentRegexesApiResponse CreateUserAgentRegexes(string jsonResponse)
        {
            return JsonConvert.DeserializeObject<GetUserAgentRegexesApiResponse>(jsonResponse);
        }

        private Task<T> ExecutePostApiRequestAsync<T>(HttpClient httpClient, string requestUri, string requestJson, Func<string, Task<T>> onSuccessFunc,
            Func<PriceOptimizationError, T> onFailureFunc, CancellationToken cancellationToken)
        {
            return httpClient.ExecutePostApiRequestAsync(requestUri, requestJson,
                onSuccessFunc: (apiResponseJson, httpStatus) => onSuccessFunc(apiResponseJson),
                onAuthErrorFailure: statusCode => onFailureFunc(PriceOptimizationError.AuthError),
                onBadRequestFailure: () => onFailureFunc(PriceOptimizationError.BadRequest),
                onTimeoutFailure: exception => onFailureFunc(PriceOptimizationError.Timeout),
                onUnknownFailure: (exception, code) => onFailureFunc(PriceOptimizationError.Unknown),
                cancellationToken);
        }

        private Task<T> ExecuteGetApiRequestAsync<T>(HttpClient httpClient, string requestUri, Func<string, Task<T>> onSuccessFunc,
            Func<PriceOptimizationError, T> onFailureFunc, CancellationToken cancellationToken)
        {
            return httpClient.ExecuteGetApiRequestAsync(requestUri, 
                onSuccessFunc: (apiResponseJson, httpStatus) => onSuccessFunc(apiResponseJson),
                onAuthErrorFailure: statusCode => onFailureFunc(PriceOptimizationError.AuthError),
                onBadRequestFailure: () => onFailureFunc(PriceOptimizationError.BadRequest),
                onTimeoutFailure: exception => onFailureFunc(PriceOptimizationError.Timeout),
                onUnknownFailure: (exception, code) => onFailureFunc(PriceOptimizationError.Unknown),
                cancellationToken);
        }


        private HttpClient GetHttpClient(string token)
        {
            var httpClient = _httpClientFactory.GetClient();
            httpClient.BaseAddress = new Uri(_baseUrl);
            httpClient.Timeout = _httpTimeout;
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            return httpClient;
        }

        private async Task<(bool IsSuccess, string? Token, T ErrorResponse)> GetTokenAsync<T>(string logNamespace,
            Func<PriceOptimizationError, T> failureResponseFunc, CancellationToken cancellationToken)
        {
            var tokenResponse = await _authTokenHandler.GetTokenAsync(cancellationToken);

            if (!tokenResponse.IsSuccess)
            {
                _logger.LogError("{0} failed to get token", logNamespace);

                return (false, null, failureResponseFunc(PriceOptimizationError.AuthError));
            }

            return (true, tokenResponse.Token!, default)!;
        }

        private PriceOptimization CreateDefaultPriceOptimization(GetPriceOptimizationRequest request)
        {
            return new PriceOptimization
            {
                UserId = request.UserId,
                DeviceId = request.DeviceId,
                ItemId = request.ItemId,
                IsOptimizedPrice = false,
                Price = request.DefaultPrice
            };
        }


        private string GetPriceOptimizationCacheKey(GetPriceOptimizationRequest request)
        {
            var cacheKey = $"{_cacheNamespace}.{request.DeviceId}.{request.ItemId}";
            return cacheKey;
        }

        private IAsyncPolicy<T> CreateResiliencyPolicy<T>(PriceOptimizationsHandlerOptions options, FallbackOptions<T> fallbackOptions,
            [CallerMemberName] string caller = default!) where T : IPriceOptimizationResult
        {
            var retryErrors = new[] { PriceOptimizationError.Timeout, PriceOptimizationError.Unknown };
            return ResiliencyPolicyBuilder.BuildPolicy(
                retryOptions: new RetryOptions<T>(r => !r.IsSuccess && retryErrors.Contains(r.Error), options.NumberOfRetries),
                new TimeoutOptions(options.Timeout),
                circuitBreakerOptions: new CircuitBreakerOptions<T>(
                    breakPredicate: r => !r.IsSuccess && retryErrors.Contains(r.Error),
                    options.NumberOfFailuresBeforeTrippingCircuitBreaker,
                    options.CircuitBreakerBreakDuration,
                    onBreakAction: (response, timespan, context) =>
                    {
                        _logger.LogWarning("@@{0}.{1}@@ Circuit breaker tripped for {2}ms due to error {3}.  Exception details (if applicable): {4}: ",
                            nameof(PriceOptimizationsHandler), caller, timespan.TotalMilliseconds,
                            response.Result.Error, response.Exception?.Message);
                    },
                    onResetAction: context => { _logger.LogInformation("@@{0}.{1}@@ Circuit breaker reset", nameof(PriceOptimizationsHandler), caller); }),
                fallbackOptions);
        }

        private PriceOptimization CreatePriceOptimization(string priceOptimizationJson)
        {
            var apiResponse = JsonConvert.DeserializeObject<GetPriceOptimizationApiResponse>(priceOptimizationJson)!;
            return apiResponse.Data;
        }

        private PriceOptimization[] CreatePriceOptimizationArray(string priceOptimizationJson)
        {
            var apiResponse = JsonConvert.DeserializeObject<GetBatchPriceOptimizationsApiResponse>(priceOptimizationJson)!;
            return apiResponse.Data;
        }

        private struct GetPriceOptimizationApiResponse
        {
            public PriceOptimization Data { get; set; }
        }

        private struct GetBatchPriceOptimizationsApiResponse
        {
            public PriceOptimization[] Data { get; set; }
        }

        private struct UserAgentRegex
        {
            public string Name { get; set; }
            public string Regex { get; set; }
        }

        private struct GetUserAgentRegexesApiResponse
        {
            public UserAgentRegex[] Data { get; set; }
        }
    }
}