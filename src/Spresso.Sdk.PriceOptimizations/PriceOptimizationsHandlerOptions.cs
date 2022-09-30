using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spresso.Sdk.Core.Connectivity;
using System;

namespace Spresso.Sdk.PriceOptimizations
{
    public class PriceOptimizationsHandlerOptions
    {
        private const string DefaultSpressoBaseUrl = "https://api.spresso.com";
        
        
        /// <summary>
        ///     For caching price optimizations
        /// </summary>
        public IDistributedCache Cache { get; set; } =
            new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

        /// <summary>
        ///     "Namespace" for price optimizations in cache.  Set this if you need to manage multiple groups of price
        ///     optimizations
        /// </summary>
        public string PriceOptimizationsGroup { get; set; } = "default";

        /// <summary>
        ///     Http Client Factory to create http clients
        /// </summary>
        public SpressoHttpClientFactory SpressoHttpClientFactory { get; set; } = SpressoHttpClientFactory.Default;

        /// <summary>
        ///    This is the base URL for all Spresso API calls.  Defaults to https://api.spresso.com
        /// </summary>
        public string SpressoBaseUrl { get; set; } = DefaultSpressoBaseUrl;
        
        /// <summary>
        ///     "Namespace" for price optimizations in cache.  Set this if you have multiple implementations of PriceOptimization using a shared cache
        /// </summary>
        public string TokenGroup { get; set; } = "default";

        /// <summary>
        ///     Logger for debug events
        /// </summary>
        public ILogger<IPriceOptimizationHandler> Logger { get; set; } = new NullLogger<IPriceOptimizationHandler>();

        /// <summary>
        ///     Http timeout.  Default is 10 seconds.
        /// </summary>
        public TimeSpan HttpTimeout { get; set; } = new TimeSpan(0, 0, 0, 10);

        /// <summary>
        ///    Price optimization cache expiration.  Default is 1 hour.
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = new TimeSpan(0, 1, 0, 0, 0);
    }
}