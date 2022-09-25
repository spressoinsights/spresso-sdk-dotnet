using HttpClientFactoryLite;
using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Spresso.Sdk.Core.Auth;

namespace Spresso.Sdk.PriceOptimizations
{
    public class PriceOptimizationsHandler
    {
        private readonly ITokenHandler _tokenHandler;
        private readonly IDistributedCache? _cache;
        private const string DefaultSpressoBaseAuthUrl = "https://auth.spresso.com";
        private const string DefaultPriceOptimizationsPath = "/v1/price-optimizations";
        private const string TokenCacheKey = "Spresso.PriceOptimizations";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _priceOptimizationsBaseUrl;

        public PriceOptimizationsHandler(ITokenHandler tokenHandler, IDistributedCache? cache = null)
        {
            _tokenHandler = tokenHandler;
            _cache = cache ?? new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));
            var spressoBaseAuthUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL") ?? DefaultSpressoBaseAuthUrl;
            var priceOptimizationsPath = Environment.GetEnvironmentVariable("SPRESSO_PRICE_OPTIMIZATIONS_PATH") ?? DefaultPriceOptimizationsPath;
            _priceOptimizationsBaseUrl = spressoBaseAuthUrl + priceOptimizationsPath;
            _httpClientFactory = new HttpClientFactory();
        }

        //todo: should api include a ttl?
        //discuss ephemeral ports https://blog.cloudflare.com/how-to-stop-running-out-of-ephemeral-ports-and-start-to-love-long-lived-connections/
    }
}