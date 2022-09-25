﻿using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Spresso.Sdk.Core.Connectivity;

namespace Spresso.Sdk.Core.Auth
{
    public class TokenHandlerOptions
    {
        /// <summary>
        ///     Caches tokens for faster performance.  Note: tokens are not encrypted
        /// </summary>
        public IDistributedCache Cache { get; set; } =
            new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

        /// <summary>
        ///     "Namespace" for tokens in cache.  Set this if you have multiple clients with different keys/secrets using the
        ///     cache, or if you have multiple clients using different scopes (and using the same cache).
        /// </summary>
        public string TokenGroup { get; set; } = "default";

        /// <summary>
        ///     The subset of scopes assigned to your client (e.g. "view" and "edit").  Default will grant you all the scopes.  Set
        ///     this to reduce the security footprint.
        /// </summary>
        public string[]? Scopes { get; set; } = null;

        /// <summary>
        ///     Http Client Factory to create http clients
        /// </summary>
        public SpressoHttpClientFactory SpressoHttpClientFactory { get; set; } = SpressoHttpClientFactory.Default;

        /// <summary>
        /// Additional parameters to be sent to the token endpoint for debug/testing purposes
        /// </summary>
        public string AdditionalParameters { get; set; } = string.Empty;
    }
}