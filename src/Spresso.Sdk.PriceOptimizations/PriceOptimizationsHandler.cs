using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
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
        private readonly string _baseUrl;
        private readonly IDistributedCache? _cache;
        private readonly TimeSpan _cacheDuration;
        private readonly string _cacheNamespace;
        private readonly SpressoHttpClientFactory _httpClientFactory;
        private readonly TimeSpan _httpTimeout;
        private readonly ILogger<IPriceOptimizationHandler> _logger;
        private readonly ITokenHandler _tokenHandler;
        private readonly IAsyncPolicy<GetPriceOptimizationsResponse> _getPriceOptimizationPolicy;
        private readonly string _additionalParameters;

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

            _getPriceOptimizationPolicy = CreateResiliencyPolicy<GetPriceOptimizationsResponse>(options, 
                new FallbackOptions<GetPriceOptimizationsResponse>(
                    r=>!r.IsSuccess,
                    (response, ctx, ct) =>
                    {
                        if (_logger!.IsEnabled(LogLevel.Error))
                        {
                            _logger.LogError("@@{0}.{1}@@ Token request failed.  Error {2}.  Exception (if applicable): {3}", nameof(PriceOptimizationsHandler), nameof(GetPriceOptimizationAsync), response?.Result.Error, response?.Exception?.Message);
                        }
                        if (response!.Exception != null)
                        {
                            if (response.Exception is TimeoutRejectedException)
                            {
                                return Task.FromResult(new GetPriceOptimizationsResponse(PriceOptimizationError.Timeout));
                            }
                            return Task.FromResult(new GetPriceOptimizationsResponse(PriceOptimizationError.Unknown));
                        }

                        return Task.FromResult(response.Result);
                    },
                    (result, context) => Task.CompletedTask
                ));
        }

        //todo: paging

        public async Task<GetPriceOptimizationsResponse> GetPriceOptimizationAsync(GetPriceOptimizationsRequest request,
            CancellationToken cancellationToken = default)
        {
            return await _getPriceOptimizationPolicy.ExecuteAsync(async () =>
            {
                var cacheKey = $"{_cacheNamespace}.{request.DeviceId}.{request.ItemId}";
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
                    return new GetPriceOptimizationsResponse(priceOptimization);
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
                    return new GetPriceOptimizationsResponse(PriceOptimizationError.AuthError);
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
                if (!String.IsNullOrEmpty(_additionalParameters))
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
                        return new GetPriceOptimizationsResponse(priceOptimization);
                    }

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                        case HttpStatusCode.Forbidden:
                            return new GetPriceOptimizationsResponse(PriceOptimizationError.AuthError);
                        case HttpStatusCode.BadRequest:
                            return new GetPriceOptimizationsResponse(PriceOptimizationError.BadRequest);
                        default:
                            return new GetPriceOptimizationsResponse(PriceOptimizationError.Unknown);
                    }
                }
                catch (HttpRequestException e) when (e.Message.Contains("No connection could be made because the target machine actively refused it."))
                {
                    return new GetPriceOptimizationsResponse(PriceOptimizationError.Timeout);
                }
                catch (OperationCanceledException e)
                {
                    return new GetPriceOptimizationsResponse(PriceOptimizationError.Timeout);
                }
                catch (Exception e)
                {
                    return new GetPriceOptimizationsResponse(PriceOptimizationError.Unknown);
                }
            });
        }
        
        private IAsyncPolicy<T> CreateResiliencyPolicy<T>(PriceOptimizationsHandlerOptions options, FallbackOptions<T> fallbackOptions, [CallerMemberName]string caller = default!) where T : IPriceOptimizationResult
        {
            var retryErrors = new[] { PriceOptimizationError.Timeout, PriceOptimizationError.Unknown };
            return ResiliencyPolicyBuilder.BuildPolicy<T>(
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
            var apiResponse = JsonConvert.DeserializeObject<GetPriceOptimizationsApiResponse>(priceOptimizationJson)!;
            return apiResponse.Data;
        }

        private class GetPriceOptimizationsApiResponse
        {
            public PriceOptimization Data { get; set; }
        }
    }
}