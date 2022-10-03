using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private readonly IDistributedCache? _cache;
        private readonly TimeSpan _cacheDuration;
        private readonly string _cacheNamespace;
        private readonly IAsyncPolicy<GetPriceOptimizationResponse> _getPriceOptimizationPolicy;
        private readonly IAsyncPolicy<GetBatchPriceOptimizationsResponse> _getPriceOptimizationsBatchPolicy;
        private readonly SpressoHttpClientFactory _httpClientFactory;
        private readonly TimeSpan _httpTimeout;
        private readonly ILogger<IPriceOptimizationHandler> _logger;
        private readonly ITokenHandler _tokenHandler;

        public PriceOptimizationsHandler(ITokenHandler tokenHandler, PriceOptimizationsHandlerOptions? options = null)
        {
            options ??= new PriceOptimizationsHandlerOptions();
            _logger = options.Logger;
            _tokenHandler = tokenHandler;
            _cache = options.Cache;
            _httpClientFactory = options.SpressoHttpClientFactory;
            _baseUrl = options.SpressoBaseUrl;
            _cacheNamespace = $"{TokenCacheKeyPrefix}.{options.TokenGroup}";
            _httpTimeout = options.HttpTimeout;
            _cacheDuration = options.CacheDuration;
            _additionalParameters = options.AdditionalParameters;

            _getPriceOptimizationPolicy = CreateResiliencyPolicy(options,
                new FallbackOptions<GetPriceOptimizationResponse>(
                    r => !r.IsSuccess,
                    (response, ctx, ct) =>
                    {
                        if (_logger!.IsEnabled(LogLevel.Error))
                        {
                            _logger.LogError("@@{0}.{1}@@ Token request failed.  Error {2}.  Exception (if applicable): {3}", nameof(PriceOptimizationsHandler),
                                nameof(GetPriceOptimizationAsync), response?.Result.Error, response?.Exception?.Message);
                        }
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
                    ));

            _getPriceOptimizationsBatchPolicy = CreateResiliencyPolicy(options,
                new FallbackOptions<GetBatchPriceOptimizationsResponse>(
                    r => !r.IsSuccess,
                    (response, ctx, ct) =>
                    {
                        if (_logger!.IsEnabled(LogLevel.Error))
                        {
                            _logger.LogError("@@{0}.{1}@@ Token request failed.  Error {2}.  Exception (if applicable): {3}", nameof(PriceOptimizationsHandler),
                                nameof(GetBatchPriceOptimizationsAsync), response?.Result.Error, response?.Exception?.Message);
                        }
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
                    ));
        }

        //todo: paging

        public async Task<GetPriceOptimizationResponse> GetPriceOptimizationAsync(GetPriceOptimizationRequest request,
            CancellationToken cancellationToken = default)
        {
            var executionResult = await _getPriceOptimizationPolicy.ExecuteAsync(async () =>
            {
                var cacheKey = GetPriceOptimizationCacheKey(request);
                const string logNamespace = "@@PriceOptimizationsHandler.GetPriceOptimizationAsync@@";

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("{0} fetching optimization [device: {1}, item: {2}]", logNamespace, request.DeviceId, request.ItemId);
                }

                var cachedPriceOptimization = await _cache.GetStringAsync(cacheKey, cancellationToken);
                if (cachedPriceOptimization != null)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("{0} cache hit [device: {1}, item: {2}]", logNamespace, request.DeviceId, request.ItemId);
                    }

                    var priceOptimization = CreatePriceOptimization(cachedPriceOptimization);
                    return new GetPriceOptimizationResponse(priceOptimization);
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("{0} cache miss, calling api [device: {1}, item: {2}]", logNamespace, request.DeviceId, request.ItemId);
                }

                var tokenResponse = await _tokenHandler.GetTokenAsync(cancellationToken);

                if (!tokenResponse.IsSuccess)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError("{0} failed to get token", logNamespace);
                    }
                    return new GetPriceOptimizationResponse(PriceOptimizationError.AuthError);
                }

                var token = tokenResponse.Token!;

                var httpClient = _httpClientFactory.GetClient();
                httpClient.BaseAddress = new Uri(_baseUrl);
                httpClient.Timeout = _httpTimeout;
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                var query =
                    $"/v1/priceOptimizations?deviceId={request.DeviceId}&itemId={request.ItemId}&defaultPrice={request.DefaultPrice}&overrideToDefaultPrice={request.OverrideToDefaultPrice}";
                if (!string.IsNullOrEmpty(request.UserId))
                {
                    query += $"&userId={request.UserId}";
                }
                if (!string.IsNullOrEmpty(_additionalParameters))
                {
                    query += $"&{_additionalParameters}";
                }

                try
                {
                    var response = await httpClient.GetAsync(query, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var priceOptimizationJson = await response.Content.ReadAsStringAsync();
                        var priceOptimization = CreatePriceOptimization(priceOptimizationJson);
                        await _cache.SetStringAsync(cacheKey, priceOptimizationJson, new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = _cacheDuration
                        }, cancellationToken);
                        return new GetPriceOptimizationResponse(priceOptimization);
                    }

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                        case HttpStatusCode.Forbidden:
                            return new GetPriceOptimizationResponse(PriceOptimizationError.AuthError);
                        case HttpStatusCode.BadRequest:
                            return new GetPriceOptimizationResponse(PriceOptimizationError.BadRequest);
                        default:
                            return new GetPriceOptimizationResponse(PriceOptimizationError.Unknown);
                    }
                }
                catch (HttpRequestException e) when (e.Message.Contains("No connection could be made because the target machine actively refused it."))
                {
                    return new GetPriceOptimizationResponse(PriceOptimizationError.Timeout);
                }
                catch (OperationCanceledException e)
                {
                    return new GetPriceOptimizationResponse(PriceOptimizationError.Timeout);
                }
                catch (Exception e)
                {
                    return new GetPriceOptimizationResponse(PriceOptimizationError.Unknown);
                }
            });

            if (executionResult.IsSuccess)
            {
                return executionResult;
            }

            // create a price optimization upon failure using the default price
            return new GetPriceOptimizationResponse(executionResult.Error, CreateDefaultPriceOptimization(request));
        }

        public async Task<GetBatchPriceOptimizationsResponse> GetBatchPriceOptimizationsAsync(GetBatchPriceOptimizationsRequest request,
            CancellationToken cancellationToken = default)
        {
            var executionResult = await _getPriceOptimizationsBatchPolicy.ExecuteAsync(async () =>
            {

                var poRequests = request.Requests.ToList();
                var requestCount = poRequests.Count;

                if (requestCount > MaxRequestSize)
                {
                    throw new ArgumentException($"Max batch size is {MaxRequestSize} requests");
                }

                const string logNamespace = "@@PriceOptimizationsHandler.GetBatchPriceOptimizationsResponse@@";

                var responses = new PriceOptimization[requestCount];

                var needApiCallIndexes = new List<int>(requestCount);

                for (int i = 0; i < requestCount; i++)
                {
                    var poRequest = poRequests[i];

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("{0} Checking cache for [device: {1}, item: {2}]", logNamespace, poRequest.DeviceId, poRequest.ItemId);
                    }
                    var cacheKey = GetPriceOptimizationCacheKey(poRequest);
                    var cachedPriceOptimization = await _cache.GetStringAsync(cacheKey, cancellationToken);
                    if (cachedPriceOptimization != null)
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("{0} cache hit [device: {1}, item: {2}]", logNamespace, poRequest.DeviceId, poRequest.ItemId);
                        }

                        responses[i] = CreatePriceOptimization(cachedPriceOptimization);
                    }
                    else
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("{0} cache miss [device: {1}, item: {2}]", logNamespace, poRequest.DeviceId, poRequest.ItemId);
                        }

                        needApiCallIndexes.Add(i);
                    }
                }
                if (needApiCallIndexes.Any())
                {
                    var apiRequests = new List<GetPriceOptimizationRequest>(needApiCallIndexes.Count);

                    needApiCallIndexes.ForEach(i =>
                    {
                        apiRequests.Add(poRequests[i]);
                    });


                    var tokenResponse = await _tokenHandler.GetTokenAsync(cancellationToken);

                    if (!tokenResponse.IsSuccess)
                    {
                        if (_logger.IsEnabled(LogLevel.Error))
                        {
                            _logger.LogError("{0} failed to get token", logNamespace);
                        }
                        return new GetBatchPriceOptimizationsResponse(PriceOptimizationError.AuthError);
                    }

                    var token = tokenResponse.Token!;
                    var httpClient = _httpClientFactory.GetClient();
                    httpClient.BaseAddress = new Uri(_baseUrl);
                    httpClient.Timeout = _httpTimeout;
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                    var requestUri =
                        $"/v1/priceOptimizations";
                    if (!String.IsNullOrEmpty(_additionalParameters))
                    {
                        requestUri += $"?{_additionalParameters}";
                    }

                    var batchApiRequest = new
                    {
                        PricingRefs = apiRequests
                    };
                    var requestJson = JsonConvert.SerializeObject(batchApiRequest);

                    try
                    {
                        var apiResponse = await httpClient.PostAsync(requestUri, new StringContent(requestJson, Encoding.UTF8, "application/json"),
                            cancellationToken);

                        if (apiResponse.IsSuccessStatusCode)
                        {
                            var apiBatchOptimizationsJson = await apiResponse.Content.ReadAsStringAsync();
                            var apiBatchOptimizations = CreatePriceOptimizationArray(apiBatchOptimizationsJson);

                            // assumption for this entire module is that order is preserved between api request and response
                            int apiResponseIndex = 0;
                            foreach (var i in needApiCallIndexes)
                            {
                                responses[i] = apiBatchOptimizations[apiResponseIndex++];
                                var cacheKey = GetPriceOptimizationCacheKey(poRequests[i]);
                                await _cache.SetStringAsync(cacheKey,
                                    JsonConvert.SerializeObject(new GetPriceOptimizationApiResponse() { Data = responses[i] }),
                                    new DistributedCacheEntryOptions
                                    {
                                        AbsoluteExpirationRelativeToNow = _cacheDuration
                                    }, cancellationToken);
                            }
                        }
                        else
                        {
                            switch (apiResponse.StatusCode)
                            {
                                case HttpStatusCode.Unauthorized:
                                case HttpStatusCode.Forbidden:
                                    return new GetBatchPriceOptimizationsResponse(PriceOptimizationError.AuthError);
                                case HttpStatusCode.BadRequest:
                                    return new GetBatchPriceOptimizationsResponse(PriceOptimizationError.BadRequest);
                                default:
                                    return new GetBatchPriceOptimizationsResponse(PriceOptimizationError.Unknown);
                            }
                        }


                    }
                    catch (HttpRequestException e) when (e.Message.Contains("No connection could be made because the target machine actively refused it."))
                    {
                        return new GetBatchPriceOptimizationsResponse(PriceOptimizationError.Timeout);
                    }
                    catch (OperationCanceledException e)
                    {
                        return new GetBatchPriceOptimizationsResponse(PriceOptimizationError.Timeout);
                    }
                    catch (Exception e)
                    {
                        return new GetBatchPriceOptimizationsResponse(PriceOptimizationError.Unknown);
                    }

                }
                return new GetBatchPriceOptimizationsResponse(responses);
            });

            if (executionResult.IsSuccess)
            {
                return executionResult;
            }
            
            // todo: fallback price not cached, but note error may be because issue with cache.
            return new GetBatchPriceOptimizationsResponse(executionResult.Error, request.Requests.Select(CreateDefaultPriceOptimization));

        }
        
        private PriceOptimization CreateDefaultPriceOptimization(GetPriceOptimizationRequest request) => new PriceOptimization
        {
            UserId = request.UserId,
            DeviceId = request.DeviceId,
            ItemId = request.ItemId,
            IsOptimizedPrice = false,
            Price = request.DefaultPrice
        };
        
        
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
                new RetryOptions<T>(r => !r.IsSuccess && retryErrors.Contains(r.Error), options.NumberOfRetries),
                new TimeoutOptions(options.Timeout),
                new CircuitBreakerOptions<T>(
                    r => !r.IsSuccess && retryErrors.Contains(r.Error),
                    options.NumberOfFailuresBeforeTrippingCircuitBreaker,
                    options.CircuitBreakerBreakDuration,
                    (response, timespan, context) =>
                    {
                        _logger.LogWarning("@@{0}.{1}@@ Circuit breaker tripped for {2}ms due to error {3}.  Exception details (if applicable): {4}: ",
                            nameof(PriceOptimizationsHandler), caller, timespan.TotalMilliseconds,
                            response.Result.Error, response.Exception?.Message);
                    },
                    context => { _logger.LogInformation("\"@@{0}.{1}@@ Circuit breaker reset", nameof(PriceOptimizationsHandler), caller); }),
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

        private class GetPriceOptimizationApiResponse
        {
            public PriceOptimization Data { get; set; }
        }

        private class GetBatchPriceOptimizationsApiResponse
        {
            public PriceOptimization[] Data { get; set; }
        }
    }
}