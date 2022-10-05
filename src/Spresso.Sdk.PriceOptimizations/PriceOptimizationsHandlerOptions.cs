﻿using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spresso.Sdk.Core.Connectivity;

namespace Spresso.Sdk.PriceOptimizations
{
    public class PriceOptimizationsHandlerOptions
    {
        private const string DefaultSpressoBaseUrl = "https://api.spresso.com";
        private int _numberOfRetries;
        private TimeSpan _timeout = new TimeSpan(0, 0, 0, 10);
        private int _numberOfFailuresBeforeTrippingCircuitBreaker = 100;

        /// <summary>
        ///     For caching price optimizations
        /// </summary>
        public IDistributedCache DistributedCache { get; set; } =
            new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

        /// <summary>
        ///     For caching object that must remain local to the server
        /// </summary>
        public IMemoryCache LocalCache { get; set; } = new MemoryCache(new MemoryCacheOptions());

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
        ///     This is the base URL for all Spresso API calls.  Defaults to https://api.spresso.com
        /// </summary>
        public string SpressoBaseUrl { get; set; } = DefaultSpressoBaseUrl;

        /// <summary>
        ///     "Namespace" for price optimizations in cache.  Set this if you have multiple implementations of PriceOptimization
        ///     using a shared cache
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
        ///     Price optimization cache expiration.  Default is 1 hour.
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = new TimeSpan(0, 1, 0, 0, 0);

        /// <summary>
        ///     The number of retries to attempt when request fails, via error or timeout.
        /// </summary>
        public int NumberOfRetries
        {
            get => _numberOfRetries;
            set
            {
                if (value >= 0 && value <= 10)
                {
                    _numberOfRetries = value;
                }
                if (value < 0)
                {
                    _numberOfRetries = 0;
                }
            }
        }

        /// <summary>
        ///     The time to wait before failing a request, including retries. Default is 10 seconds.  Max is 180 seconds.
        /// </summary>
        public TimeSpan Timeout
        {
            get => _timeout;
            set
            {
                if (value.TotalSeconds < 180)
                {
                    _timeout = value;
                }
            }
        }

        /// <summary>
        ///     The number of failures before the circuit breaker trips.  When the circuit breaker is tripped all requests
        ///     for a <see cref="CircuitBreakerBreakDuration" />period of time will fail quickly.
        /// </summary>
        public int NumberOfFailuresBeforeTrippingCircuitBreaker
        {
            get => _numberOfFailuresBeforeTrippingCircuitBreaker;
            set
            {
                if (value < 1)
                {
                    value = 1;
                }
                _numberOfFailuresBeforeTrippingCircuitBreaker = value;
            }
        }


        /// <summary>
        ///     The duration of the circuit breaker break in which all requests will quickly fail. Default is 60 seconds
        /// </summary>
        public TimeSpan CircuitBreakerBreakDuration { get; set; } = new TimeSpan(0, 0, 0, 60);

        /// <summary>
        ///     Additional parameters to be sent to the token endpoint for debug/testing purposes
        /// </summary>
        public string AdditionalParameters { get; set; } = string.Empty;
    }
}