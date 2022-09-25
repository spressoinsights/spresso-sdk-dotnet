using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Spresso.Sdk.Core.Connectivity;

namespace Spresso.Sdk.PriceOptimizations
{
    public class PriceOptimizationsHandlerOptions
    {
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
    }
}