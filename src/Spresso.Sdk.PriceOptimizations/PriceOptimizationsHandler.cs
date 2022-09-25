using System;
using Microsoft.Extensions.Caching.Distributed;
using Spresso.Sdk.Core.Auth;
using Spresso.Sdk.Core.Connectivity;

namespace Spresso.Sdk.PriceOptimizations
{
    public class PriceOptimizationsHandler
    {
        private const string DefaultSpressoBaseAuthUrl = "https://auth.spresso.com";
        private const string DefaultPriceOptimizationsPath = "/v1/price-optimizations";
        private const string TokenCacheKey = "Spresso.PriceOptimizations";
        private readonly IDistributedCache? _cache;
        private readonly SpressoHttpClientFactory _httpClientFactory;
        private readonly string _priceOptimizationsBaseUrl;
        private readonly ITokenHandler _tokenHandler;

        public PriceOptimizationsHandler(ITokenHandler tokenHandler, PriceOptimizationsHandlerOptions? options = null)
        {
            _tokenHandler = tokenHandler;
            options ??= new PriceOptimizationsHandlerOptions();
            _cache = options.Cache;
            _httpClientFactory = options.SpressoHttpClientFactory;
            var spressoBaseAuthUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL") ?? DefaultSpressoBaseAuthUrl;
            var priceOptimizationsPath = Environment.GetEnvironmentVariable("SPRESSO_PRICE_OPTIMIZATIONS_PATH") ?? DefaultPriceOptimizationsPath;
            _priceOptimizationsBaseUrl = spressoBaseAuthUrl + priceOptimizationsPath;
        }

        //todo: should api include a ttl?
        //discuss ephemeral ports https://blog.cloudflare.com/how-to-stop-running-out-of-ephemeral-ports-and-start-to-love-long-lived-connections/

        //todo: logging
    }
}