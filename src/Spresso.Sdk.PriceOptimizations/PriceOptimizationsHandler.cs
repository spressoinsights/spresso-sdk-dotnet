using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Spresso.Sdk.Core.Auth;
using Spresso.Sdk.Core.Connectivity;

namespace Spresso.Sdk.PriceOptimizations
{
    public class PriceOptimizationsHandler : IPriceOptimizationHandler
    {
        private const string TokenCacheKeyPrefix = "Spresso.PriceOptimizations";
        private readonly IDistributedCache? _cache;
        private readonly SpressoHttpClientFactory _httpClientFactory;
        private readonly ITokenHandler _tokenHandler;
        private readonly ILogger<IPriceOptimizationHandler> _logger;
        private readonly string _cacheNamespace;
        private readonly TimeSpan _httpTimeout;
        private readonly TimeSpan _cacheDuration;
        private readonly string _baseUrl;

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
        }


        //todo: paging
        
        public async Task<GetPriceOptimizationsResponse> GetPriceOptimizationAsync(GetPriceOptimizationsRequest request, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{_cacheNamespace}.{request.DeviceId}.{request.ItemId}";
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("@@PriceOptimizationsHandler.GetPriceOptimizationAsync@@ fetching optimization [device: {0}, item: {1}]", request.DeviceId, request.ItemId);
            }

            var cachedPriceOptimization = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (cachedPriceOptimization != null)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("@@PriceOptimizationsHandler.GetPriceOptimizationAsync@@ cache hit [device: {0}, item: {1}]", request.DeviceId, request.ItemId);
                }

                var priceOptimization = CreatePriceOptimization(cachedPriceOptimization);
                return new GetPriceOptimizationsResponse(priceOptimization);
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("@@PriceOptimizationsHandler.GetPriceOptimizationAsync@@ cache miss, calling api [device: {0}, item: {1}]", request.DeviceId, request.ItemId);
            }

            var tokenResponse = await _tokenHandler.GetTokenAsync(cancellationToken);

            if (!tokenResponse.IsSuccess)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError("@@PriceOptimizationsHandler.GetPriceOptimizationAsync@@ failed to get token");
                }
                return new GetPriceOptimizationsResponse(PriceOptimizationError.AuthError);
            }

            var token = tokenResponse.Token!;
            
            var httpClient = _httpClientFactory.GetClient();
            httpClient.BaseAddress = new Uri(_baseUrl);
            httpClient.Timeout = _httpTimeout;
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            var query = $"/v1/priceOptimizations?deviceId={request.DeviceId}&itemId={request.ItemId}&defaultPrice={request.DefaultPrice}&overrideToDefaultPrice={request.OverrideToDefaultPrice}";
            if (!String.IsNullOrEmpty(request.UserId))
                query += $"&userId={request.UserId}";

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