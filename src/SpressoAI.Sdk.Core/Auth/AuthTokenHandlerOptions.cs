﻿using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SpressoAI.Sdk.Core.Connectivity;
using SpressoAI.Sdk.Core.Shared;

namespace SpressoAI.Sdk.Core.Auth
{
    public class AuthTokenHandlerOptions
    {
        private int _numberOfFailuresBeforeTrippingCircuitBreaker = 10;

        private int _numberOfRetries = 3;
        private TimeSpan _timeout = new TimeSpan(0, 0, 0, seconds: 30);

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
        ///     Additional parameters to be sent to the token endpoint for debug/testing purposes
        /// </summary>
        public string AdditionalParameters { get; set; } = string.Empty;

        /// <summary>
        ///     The number of retries to attempt when a token request fails, via error or timeout.
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
        ///     The base url for the token endpoint.  This is usually https://auth.spresso.com
        /// </summary>
        public string SpressoBaseAuthUrl { get; set; } = Defaults.DefaultSpressoBaseUrl;

        /// <summary>
        ///     The audience for the token endpoint.  This is usually https://spresso-api
        /// </summary>
        public string SpressoAudience { get; set; } = Defaults.DefaultSpressoApiAudience;

        /// <summary>
        ///     The time to wait before failing a token request, including retries. Default is 30 seconds.  Max is 180 seconds.
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
        ///     The number of failures before the circuit breaker trips.  When the circuit breaker is tripped all token requests
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
        public TimeSpan CircuitBreakerBreakDuration { get; set; } = new TimeSpan(0, 0, 0, seconds: 60);

        /// <summary>
        ///     Http timeout.  Default is 1 seconds.
        /// </summary>
        public TimeSpan HttpTimeout { get; set; } = new TimeSpan(0, 0, 0, seconds: 1);

        /// <summary>
        ///     Logger for debug events
        /// </summary>
        public ILogger<IAuthTokenHandler> Logger { get; set; } = new NullLogger<IAuthTokenHandler>();

        /// <summary>
        ///    Throw an exception if the token request fails.  Default is false.
        /// </summary>
        public bool ThrowOnTokenFailure { get; set; } = false;
    }
}